using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;
using OppsView.Api.Features.EmailImport.Parsing;

namespace OppsView.Api.Features.EmailImport;

/// <summary>
/// Turns parsed emails into changes to the user's applications.
///
/// Guiding rule: when in doubt, don't guess. Anything ambiguous (no company
/// found, no matching application, two possible matches, a status that
/// contradicts what the user already recorded) goes to the "Needs review"
/// list instead of silently changing data.
///
/// Every change goes through the same domain methods the API uses
/// (JobApplication.Create / ChangeStatus), so imported data obeys the same
/// rules and status changes appear in the history like any other.
/// </summary>
public sealed class EmailImporter(AppDbContext db, IEmailParser parser, TimeProvider time)
{
    /// <summary>
    /// A second "thanks for applying" for the same company and role within this
    /// window is the same application (duplicate emails, or one from the job
    /// site and one from the employer). After it, it's a fresh application.
    /// </summary>
    public static readonly TimeSpan SameApplicationWindow = TimeSpan.FromDays(60);

    /// <param name="recheck">
    /// Message ids of emails that were left for review earlier and should be
    /// read again (the parser may have improved since). Their old review
    /// record is replaced. If they still need review they aren't counted
    /// again, so the sync message only reports what's new.
    /// </param>
    public async Task<ImportSummary> ImportAsync(
        Guid userId, IReadOnlyCollection<IncomingEmail> emails, CancellationToken ct, IReadOnlySet<string>? recheck = null)
    {
        if (emails.Count == 0) return ImportSummary.Empty;
        recheck ??= new HashSet<string>();

        var ids = emails.Select(e => e.MessageId).ToList();
        var existing = await db.ImportedEmails
            .Where(e => e.UserId == userId && ids.Contains(e.MessageId))
            .ToListAsync(ct);

        // Old review records for re-checked emails are replaced below, in the
        // same SaveChanges (so a failed sync leaves them untouched).
        var toRecheck = existing
            .Where(e => recheck.Contains(e.MessageId) && e.Outcome == ImportOutcome.NeedsReview && e.ApplicationId is null)
            .ToList();
        db.ImportedEmails.RemoveRange(toRecheck);
        var rechecking = toRecheck.Select(e => e.MessageId).ToHashSet();
        var alreadyProcessed = existing.Select(e => e.MessageId).Where(id => !rechecking.Contains(id)).ToHashSet();

        // A personal tracker holds hundreds of applications at most, so
        // loading them once and matching in memory is simpler and faster than
        // a query per email. Tracked, so status changes are saved below.
        var applications = await db.Applications.Where(a => a.UserId == userId).ToListAsync(ct);

        var now = time.GetUtcNow().UtcDateTime;
        var counts = new Dictionary<ImportOutcome, int>();
        var seen = new HashSet<string>();

        // Oldest first, so "thanks for applying" is handled before the
        // rejection that follows it, even when both arrive in one sync.
        foreach (var email in emails.OrderBy(e => e.ReceivedAt))
        {
            if (alreadyProcessed.Contains(email.MessageId) || !seen.Add(email.MessageId)) continue;

            var parsed = Clean(parser.Parse(email));
            var (outcome, application, reason) = Apply(userId, email, parsed, applications, now);

            db.ImportedEmails.Add(ImportedEmail.Record(userId, email, parsed, outcome, application?.Id, reason, now));
            var stillUnclear = rechecking.Contains(email.MessageId) && outcome == ImportOutcome.NeedsReview;
            if (!stillUnclear) counts[outcome] = counts.GetValueOrDefault(outcome) + 1;
        }

        // One SaveChanges = one transaction: a sync is applied completely or
        // not at all.
        await db.SaveChangesAsync(ct);

        return new ImportSummary(
            counts.GetValueOrDefault(ImportOutcome.Created),
            counts.GetValueOrDefault(ImportOutcome.Updated),
            counts.GetValueOrDefault(ImportOutcome.NoChange),
            counts.GetValueOrDefault(ImportOutcome.Ignored),
            counts.GetValueOrDefault(ImportOutcome.NeedsReview),
            emails.Count - seen.Count);
    }

    private (ImportOutcome, JobApplication?, string?) Apply(
        Guid userId, IncomingEmail email, ParsedEmail parsed, List<JobApplication> applications, DateTime now)
    {
        switch (parsed.Kind)
        {
            case EmailKind.NotRelevant:
                return (ImportOutcome.Ignored, null, null);
            case EmailKind.Unknown:
                return Review("OppsView couldn't tell what this email is about.");
            case EmailKind.ApplicationReceived:
                return ApplyNewApplication(userId, email, parsed, applications, now);
            default:
                return ApplyStatusChange(email, parsed, applications);
        }
    }

    /// <summary>
    /// Stand-ins when a confirmation leaves something out (Pnet's never name
    /// the company; some employers never name the role). The application is
    /// still created, so it's tracked, and a later email or the user fills
    /// in the gap.
    /// </summary>
    public const string UnknownCompany = "Unknown company";
    public const string UnknownRole = "Unknown role";

    public static bool IsPlaceholder(string value) => value is UnknownCompany or UnknownRole;

    /// <summary>Start of the note on applications created with a missing detail.</summary>
    private const string ProvisionalNotePrefix = "The email didn't";

    /// <summary>
    /// The company isn't really known yet: a placeholder, or the job site /
    /// hiring system the email came from ("Pnet").
    /// </summary>
    private static bool IsProvisionalCompany(string company) =>
        company == UnknownCompany || SenderDomain.IsIntermediaryName(company);

    private (ImportOutcome, JobApplication?, string?) ApplyNewApplication(
        Guid userId, IncomingEmail email, ParsedEmail parsed, List<JobApplication> applications, DateTime now)
    {
        if (parsed.Company is null && parsed.Role is null)
            return Review("Couldn't find the company or the job title in this email.");

        var company = parsed.Company ?? UnknownCompany;
        var role = parsed.Role ?? UnknownRole;
        var appliedOn = DateOnly.FromDateTime(email.ReceivedAt);

        var existing = applications.FirstOrDefault(a =>
            (CompanyName.AreSame(a.Company, company) || a.Company == UnknownCompany && parsed.Company is null)
            && (SameRole(a.Role, role) || a.Role == UnknownRole && parsed.Role is null)
            && Math.Abs(a.AppliedOn.DayNumber - appliedOn.DayNumber) <= SameApplicationWindow.TotalDays);
        if (existing is not null)
            return (ImportOutcome.NoChange, existing, "This application is already in OppsView.");

        var notes =
            parsed.Company is null ? $"{ProvisionalNotePrefix} say the company. Click Edit to fill it in."
            : SenderDomain.IsIntermediaryName(company) ? $"{ProvisionalNotePrefix} name the employer (it came from {company}). Click Edit to fill it in."
            : parsed.Role is null ? $"{ProvisionalNotePrefix} say the job title. Click Edit to fill it in."
            : null;

        try
        {
            var application = JobApplication.Create(
                userId, company, role, parsed.Platform ?? JobPlatform.Other, appliedOn,
                jobUrl: null, notes, now, ApplicationSource.Email);
            db.Applications.Add(application);
            applications.Add(application);
            return (ImportOutcome.Created, application, null);
        }
        catch (DomainException ex)
        {
            return Review(ex.Message);
        }
    }

    private static (ImportOutcome, JobApplication?, string?) ApplyStatusChange(
        IncomingEmail email, ParsedEmail parsed, List<JobApplication> applications)
    {
        var target = parsed.Kind switch
        {
            EmailKind.ApplicationViewed => ApplicationStatus.Viewed,
            EmailKind.InterviewInvite => ApplicationStatus.Interview,
            EmailKind.Offer => ApplicationStatus.Offer,
            EmailKind.Rejection => ApplicationStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(parsed), parsed.Kind, null),
        };
        var label = target.ToString().ToLowerInvariant();

        if (parsed.Company is null) return Review($"Looks like a {label} email, but the company name wasn't found.");

        var candidates = applications.Where(a => CompanyName.AreSame(a.Company, parsed.Company)).ToList();
        if (candidates.Count == 0)
        {
            // "BBD" in the email, "BBD Software" typed by the user. Accepted
            // only if it points at a single application (checked below).
            candidates = applications.Where(a => CompanyName.StartsWithSameWords(a.Company, parsed.Company)).ToList();
        }
        if (parsed.Role is not null && candidates.Count > 1)
        {
            var sameRole = candidates.Where(a => SameRole(a.Role, parsed.Role)).ToList();
            if (sameRole.Count > 0) candidates = sameRole;
        }
        // If several remain, prefer the ones the email can still move forward.
        if (candidates.Count > 1)
        {
            var open = candidates.Where(a => a.Status == target || StatusTransitions.IsAllowed(a.Status, target)).ToList();
            if (open.Count > 0) candidates = open;
        }

        if (candidates.Count == 0 && parsed.Role is not null && !IsProvisionalCompany(parsed.Company))
        {
            // E.g. a Pnet confirmation created "Pnet - IT Support Technician";
            // now Cartrack writes about that job title. If exactly one such
            // application exists, it's this one: put in the real employer.
            var provisional = applications
                .Where(a => IsProvisionalCompany(a.Company) && SameRole(a.Role, parsed.Role))
                .ToList();
            if (provisional.Count == 1)
            {
                var a = provisional[0];
                a.UpdateDetails(parsed.Company, a.Role, a.Platform, a.AppliedOn, a.JobUrl,
                    a.Notes?.StartsWith(ProvisionalNotePrefix) == true ? null : a.Notes, email.ReceivedAt);
                candidates = provisional;
            }
        }

        if (candidates.Count == 0)
            return Review($"Looks like a {label} email from {parsed.Company}, but there's no application to them in OppsView.");
        if (candidates.Count > 1)
            return Review($"You have {candidates.Count} applications to {parsed.Company}; OppsView couldn't tell which one this email is about.");

        var application = candidates[0];
        if (application.Status == target)
            return (ImportOutcome.NoChange, application, $"Already marked as {target}.");

        if (StatusTransitions.IsAllowed(application.Status, target))
        {
            // Dated when the email arrived, not when we synced: the history
            // should show when things actually happened.
            application.ChangeStatus(target, email.ReceivedAt, ApplicationSource.Email);
            return (ImportOutcome.Updated, application, null);
        }

        // Final states (Offer, Rejected) contradicting the email need a human.
        if (application.Status is ApplicationStatus.Offer or ApplicationStatus.Rejected)
            return Review($"This {label} email doesn't match the application's current status ({application.Status}).", application);

        // Otherwise the email is behind the application (e.g. "viewed" after
        // an interview was already recorded): nothing new to learn.
        return (ImportOutcome.NoChange, application, $"Application is already past {target}.");
    }

    private static (ImportOutcome, JobApplication?, string?) Review(string reason, JobApplication? application = null) =>
        (ImportOutcome.NeedsReview, application, reason);

    private static bool SameRole(string a, string b) =>
        string.Equals(Collapse(a), Collapse(b), StringComparison.OrdinalIgnoreCase);

    private static string Collapse(string s) => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static ParsedEmail Clean(ParsedEmail p) => p with
    {
        Company = string.IsNullOrWhiteSpace(p.Company) ? null : p.Company.Trim(),
        Role = string.IsNullOrWhiteSpace(p.Role) ? null : p.Role.Trim(),
    };
}
