using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;
using OppsView.Api.Features.Applications;

namespace OppsView.Api.Features.EmailImport;

/// <summary>The "Needs review" list: emails OppsView wasn't sure about, and the user's decisions.</summary>
public sealed class ReviewService(AppDbContext db, TimeProvider time)
{
    public async Task<IReadOnlyList<ReviewItemResponse>> ListAsync(Guid userId, CancellationToken ct)
    {
        var items = await db.ImportedEmails.AsNoTracking()
            .Where(e => e.UserId == userId && e.Outcome == ImportOutcome.NeedsReview)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync(ct);
        return items.Select(ReviewItemResponse.From).ToList();
    }

    public async Task<bool> DismissAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var email = await db.ImportedEmails.SingleOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (email is null) return false;
        email.Dismiss(time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Creates an application from a reviewed email, using the details the
    /// user confirmed. If the email was a rejection (or interview, offer...),
    /// the new application gets that status straight away, dated by the email.
    /// </summary>
    public async Task<ApplicationResponse?> CreateApplicationAsync(
        Guid userId, Guid id, SaveApplicationRequest r, CancellationToken ct)
    {
        var email = await db.ImportedEmails.SingleOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (email is null) return null;

        var now = time.GetUtcNow().UtcDateTime;
        var application = JobApplication.Create(
            userId, r.Company, r.Role, r.Platform!.Value, r.AppliedOn!.Value, r.JobUrl, r.Notes, now, ApplicationSource.Email);

        var status = email.Kind switch
        {
            EmailKind.ApplicationViewed => ApplicationStatus.Viewed,
            EmailKind.InterviewInvite => ApplicationStatus.Interview,
            EmailKind.Offer => ApplicationStatus.Offer,
            EmailKind.Rejection => ApplicationStatus.Rejected,
            _ => (ApplicationStatus?)null,
        };
        if (status is { } s) application.ChangeStatus(s, email.ReceivedAt, ApplicationSource.Email);

        email.ResolveWith(application.Id, ImportOutcome.Created, now);
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        return ApplicationResponse.From(application);
    }
}
