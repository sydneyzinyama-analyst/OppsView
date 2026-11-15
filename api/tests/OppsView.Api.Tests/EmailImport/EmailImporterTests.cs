using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using OppsView.Api.Data;
using OppsView.Api.Features.EmailImport;

namespace OppsView.Api.Tests.EmailImport;

/// <summary>
/// The importer's decisions, tested with a stub parser so each test states
/// exactly what the "email" says, on a real (SQLite) database.
/// </summary>
public sealed class EmailImporterTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly StubParser _parser = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Guid _userId;
    private int _nextId;

    public EmailImporterTests()
    {
        _connection.Open();
        using var db = NewDb();
        db.Database.EnsureCreated();
        var user = User.Create("me@example.com", Now);
        user.SetPasswordHash("hash");
        db.Users.Add(user);
        db.SaveChanges();
        _userId = user.Id;
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    /// <summary>An email received <paramref name="daysAgo"/> days ago that the stub parser reads as <paramref name="parsed"/>.</summary>
    private IncomingEmail Email(ParsedEmail parsed, int daysAgo = 0, string? messageId = null)
    {
        var id = messageId ?? $"msg-{++_nextId}";
        _parser.Results[id] = parsed;
        return new IncomingEmail(id, "jobs@example.com", $"Subject {id}", "body", Now.AddDays(-daysAgo));
    }

    private static ParsedEmail Received(string? company = "Acme", string? role = "Developer", JobPlatform platform = JobPlatform.Pnet) =>
        new(EmailKind.ApplicationReceived, platform, company, role);

    private static ParsedEmail Says(EmailKind kind, string? company = "Acme", string? role = null) =>
        new(kind, JobPlatform.Pnet, company, role);

    private async Task<ImportSummary> ImportAsync(params IncomingEmail[] emails) => await RecheckAsync([], emails);

    private async Task<ImportSummary> RecheckAsync(HashSet<string> recheck, params IncomingEmail[] emails)
    {
        await using var db = NewDb();
        return await new EmailImporter(db, _parser, _time).ImportAsync(_userId, emails, CancellationToken.None, recheck);
    }

    private async Task<JobApplication> AddManualAsync(string company, string role = "Developer", int daysAgo = 3,
        ApplicationStatus? status = null)
    {
        await using var db = NewDb();
        var app = JobApplication.Create(_userId, company, role, JobPlatform.Pnet,
            DateOnly.FromDateTime(Now.AddDays(-daysAgo)), null, null, Now.AddDays(-daysAgo));
        if (status is { } s) app.ChangeStatus(s, Now.AddDays(-daysAgo + 1));
        db.Applications.Add(app);
        await db.SaveChangesAsync();
        return app;
    }

    private List<JobApplication> Applications()
    {
        using var db = NewDb();
        return db.Applications.Include(a => a.History).AsNoTracking().ToList();
    }

    private ImportedEmail Imported(string messageId)
    {
        using var db = NewDb();
        return db.ImportedEmails.AsNoTracking().Single(e => e.MessageId == messageId);
    }

    // ---- New applications ----------------------------------------------

    [Fact]
    public async Task A_confirmation_email_creates_an_application_dated_when_the_email_arrived()
    {
        var summary = await ImportAsync(Email(Received(platform: JobPlatform.Indeed), daysAgo: 2, messageId: "m1"));

        Assert.Equal(1, summary.Created);
        var app = Assert.Single(Applications());
        Assert.Equal(("Acme", "Developer", JobPlatform.Indeed), (app.Company, app.Role, app.Platform));
        Assert.Equal(DateOnly.FromDateTime(Now.AddDays(-2)), app.AppliedOn);
        Assert.Equal(ApplicationStatus.Applied, app.Status);
        Assert.Equal(ApplicationSource.Email, app.Source);
        Assert.Equal(app.Id, Imported("m1").ApplicationId);
    }

    [Fact]
    public async Task An_unknown_platform_is_recorded_as_Other()
    {
        await ImportAsync(Email(new ParsedEmail(EmailKind.ApplicationReceived, null, "Acme", "Dev")));
        Assert.Equal(JobPlatform.Other, Assert.Single(Applications()).Platform);
    }

    [Fact]
    public async Task A_duplicate_confirmation_does_not_create_a_second_application()
    {
        await AddManualAsync("Acme (Pty) Ltd", "developer");

        var summary = await ImportAsync(Email(Received(company: "ACME", role: "Developer")));

        Assert.Equal((0, 1), (summary.Created, summary.NoChange));
        Assert.Single(Applications());
    }

    [Fact]
    public async Task Reapplying_to_the_same_job_months_later_is_a_new_application()
    {
        await AddManualAsync("Acme", "Developer", daysAgo: 120);

        var summary = await ImportAsync(Email(Received()));

        Assert.Equal(1, summary.Created);
        Assert.Equal(2, Applications().Count);
    }

    [Fact]
    public async Task A_confirmation_with_neither_company_nor_role_needs_review()
    {
        var summary = await ImportAsync(Email(Received(company: null, role: null), messageId: "m1"));

        Assert.Equal(1, summary.NeedsReview);
        Assert.Empty(Applications());
        Assert.NotNull(Imported("m1").Reason);
    }

    [Theory]
    [InlineData(null, "Developer", "Unknown company", "Developer", "company")]
    [InlineData("  ", "Developer", "Unknown company", "Developer", "company")]
    [InlineData("Acme", null, "Acme", "Unknown role", "job title")]
    public async Task A_confirmation_missing_one_detail_is_still_tracked_with_a_placeholder(
        string? company, string? role, string expectedCompany, string expectedRole, string missing)
    {
        var summary = await ImportAsync(Email(Received(company, role)));

        Assert.Equal(1, summary.Created);
        var app = Applications().Single();
        Assert.Equal((expectedCompany, expectedRole), (app.Company, app.Role));
        Assert.Contains(missing, app.Notes);
    }

    [Fact]
    public async Task A_later_email_from_the_company_fills_in_an_unknown_company_by_job_title()
    {
        // Pnet confirmation: no company. Then Cartrack rejects that job title.
        await ImportAsync(Email(Received(company: null, role: "IT Support Technician - Tier 1"), daysAgo: 5));

        var summary = await ImportAsync(Email(Says(EmailKind.Rejection, company: "Cartrack", role: "it support technician - tier 1")));

        Assert.Equal(1, summary.Updated);
        var app = Applications().Single();
        Assert.Equal(("Cartrack", ApplicationStatus.Rejected), (app.Company, app.Status));
        Assert.Null(app.Notes); // the "didn't say the company" note is gone
    }

    [Fact]
    public async Task An_application_named_after_the_job_site_gets_the_real_employer_later()
    {
        // The parser gives "Pnet" when Pnet's confirmation names no employer.
        await ImportAsync(Email(Received(company: "Pnet", role: "Industrial Software Developer"), daysAgo: 5));
        var created = Applications().Single();
        Assert.Contains("came from Pnet", created.Notes);

        var summary = await ImportAsync(Email(Says(EmailKind.InterviewInvite, company: "Bosch", role: "Industrial Software Developer")));

        Assert.Equal(1, summary.Updated);
        var app = Applications().Single();
        Assert.Equal(("Bosch", ApplicationStatus.Interview, JobPlatform.Pnet), (app.Company, app.Status, app.Platform));
        Assert.Null(app.Notes);
    }

    [Fact]
    public async Task A_status_email_from_the_job_site_itself_does_not_rename_anything()
    {
        await ImportAsync(Email(Received(company: "Pnet", role: "Developer"), daysAgo: 5));

        var summary = await ImportAsync(Email(Says(EmailKind.ApplicationViewed, company: "Pnet", role: "Developer")));

        Assert.Equal(1, summary.Updated);
        Assert.Equal(("Pnet", ApplicationStatus.Viewed), (Applications().Single().Company, Applications().Single().Status));
    }

    [Fact]
    public async Task Two_unnamed_applications_with_the_same_title_are_not_guessed_between()
    {
        await ImportAsync(Email(Received(company: null, role: "Developer"), daysAgo: 100));
        await ImportAsync(Email(Received(company: null, role: "Developer"), daysAgo: 1));

        var summary = await ImportAsync(Email(Says(EmailKind.Rejection, company: "Acme", role: "Developer")));

        Assert.Equal(1, summary.NeedsReview);
        Assert.All(Applications(), a => Assert.Equal(EmailImporter.UnknownCompany, a.Company));
    }

    [Fact]
    public async Task A_repeat_confirmation_with_no_company_is_not_a_second_application()
    {
        await ImportAsync(Email(Received(company: null, role: "Developer"), daysAgo: 2));
        var summary = await ImportAsync(Email(Received(company: null, role: "Developer")));
        Assert.Equal(1, summary.NoChange);
    }

    // ---- Status changes -------------------------------------------------

    [Fact]
    public async Task A_rejection_moves_the_matching_application_to_Rejected_dated_by_the_email()
    {
        var app = await AddManualAsync("Acme");

        var summary = await ImportAsync(Email(Says(EmailKind.Rejection, company: "Acme Ltd"), daysAgo: 1, messageId: "m1"));

        Assert.Equal(1, summary.Updated);
        var updated = Applications().Single();
        Assert.Equal(ApplicationStatus.Rejected, updated.Status);
        var change = Assert.Single(updated.History);
        Assert.Equal(ApplicationSource.Email, change.Source);
        Assert.Equal(Now.AddDays(-1), change.ChangedAt);
        Assert.Equal(app.Id, Imported("m1").ApplicationId);
    }

    [Theory]
    [InlineData(EmailKind.ApplicationViewed, ApplicationStatus.Viewed)]
    [InlineData(EmailKind.InterviewInvite, ApplicationStatus.Interview)]
    [InlineData(EmailKind.Offer, ApplicationStatus.Offer)]
    public async Task Other_status_emails_move_the_application_forward(EmailKind kind, ApplicationStatus expected)
    {
        await AddManualAsync("Acme");
        await ImportAsync(Email(Says(kind)));
        Assert.Equal(expected, Applications().Single().Status);
    }

    [Fact]
    public async Task Confirmation_and_rejection_in_one_sync_are_applied_in_date_order()
    {
        // Passed newest first on purpose.
        var summary = await ImportAsync(
            Email(Says(EmailKind.Rejection), daysAgo: 1),
            Email(Received(), daysAgo: 10));

        Assert.Equal((1, 1), (summary.Created, summary.Updated));
        Assert.Equal(ApplicationStatus.Rejected, Applications().Single().Status);
    }

    [Fact]
    public async Task A_shorter_or_longer_company_name_matches_when_it_is_the_only_candidate()
    {
        await AddManualAsync("BBD Software");

        var summary = await ImportAsync(Email(Says(EmailKind.Rejection, company: "BBD")));

        Assert.Equal(1, summary.Updated);
        Assert.Equal(ApplicationStatus.Rejected, Applications().Single().Status);
    }

    [Fact]
    public async Task A_loose_company_match_with_several_candidates_needs_review()
    {
        await AddManualAsync("Discovery Health");
        await AddManualAsync("Discovery Bank");

        var summary = await ImportAsync(Email(Says(EmailKind.Rejection, company: "Discovery")));

        Assert.Equal(1, summary.NeedsReview);
    }

    [Fact]
    public async Task A_status_email_for_an_unknown_company_needs_review()
    {
        var summary = await ImportAsync(Email(Says(EmailKind.Rejection, company: "Globex"), messageId: "m1"));

        Assert.Equal(1, summary.NeedsReview);
        Assert.Contains("Globex", Imported("m1").Reason);
    }

    [Fact]
    public async Task Two_open_applications_to_the_same_company_need_review_unless_the_role_tells_them_apart()
    {
        await AddManualAsync("Acme", "Backend Developer");
        await AddManualAsync("Acme", "Data Analyst");

        var vague = await ImportAsync(Email(Says(EmailKind.InterviewInvite)));
        var specific = await ImportAsync(Email(Says(EmailKind.InterviewInvite, role: "data analyst")));

        Assert.Equal(1, vague.NeedsReview);
        Assert.Equal(1, specific.Updated);
        Assert.Equal(ApplicationStatus.Interview, Applications().Single(a => a.Role == "Data Analyst").Status);
        Assert.Equal(ApplicationStatus.Applied, Applications().Single(a => a.Role == "Backend Developer").Status);
    }

    [Fact]
    public async Task A_closed_application_is_skipped_when_another_one_to_the_company_is_still_open()
    {
        await AddManualAsync("Acme", "Old role", daysAgo: 200, status: ApplicationStatus.Rejected);
        await AddManualAsync("Acme", "New role");

        var summary = await ImportAsync(Email(Says(EmailKind.InterviewInvite)));

        Assert.Equal(1, summary.Updated);
        Assert.Equal(ApplicationStatus.Interview, Applications().Single(a => a.Role == "New role").Status);
    }

    [Fact]
    public async Task An_email_that_repeats_the_current_status_changes_nothing()
    {
        await AddManualAsync("Acme", status: ApplicationStatus.Interview);
        var summary = await ImportAsync(Email(Says(EmailKind.InterviewInvite)));
        Assert.Equal(1, summary.NoChange);
    }

    [Fact]
    public async Task An_email_behind_the_current_status_changes_nothing()
    {
        await AddManualAsync("Acme", status: ApplicationStatus.Interview);

        var summary = await ImportAsync(Email(Says(EmailKind.ApplicationViewed)));

        Assert.Equal(1, summary.NoChange);
        Assert.Equal(ApplicationStatus.Interview, Applications().Single().Status);
    }

    [Fact]
    public async Task A_rejection_for_an_application_with_an_offer_needs_review_and_changes_nothing()
    {
        await AddManualAsync("Acme", status: ApplicationStatus.Offer);

        var summary = await ImportAsync(Email(Says(EmailKind.Rejection), messageId: "m1"));

        Assert.Equal(1, summary.NeedsReview);
        Assert.Equal(ApplicationStatus.Offer, Applications().Single().Status);
        Assert.NotNull(Imported("m1").ApplicationId); // linked, so the review screen can show which one
    }

    // ---- Other emails and repeat syncs --------------------------------

    [Fact]
    public async Task Job_alerts_are_ignored_and_unclear_emails_need_review()
    {
        var summary = await ImportAsync(
            Email(new ParsedEmail(EmailKind.NotRelevant)),
            Email(ParsedEmail.Unknown));

        Assert.Equal((1, 1), (summary.Ignored, summary.NeedsReview));
        Assert.Empty(Applications());
    }

    [Fact]
    public async Task Syncing_the_same_emails_again_does_nothing()
    {
        var email = Email(Received());
        await ImportAsync(email);

        var second = await ImportAsync(email, email);

        Assert.Equal(ImportSummary.Empty with { AlreadyProcessed = 2 }, second);
        Assert.Single(Applications());
    }

    [Fact]
    public async Task Another_users_applications_are_never_matched()
    {
        await using (var db = NewDb())
        {
            var other = User.Create("other@example.com", Now);
            other.SetPasswordHash("hash");
            db.Users.Add(other);
            db.Applications.Add(JobApplication.Create(other.Id, "Acme", "Developer", JobPlatform.Pnet,
                DateOnly.FromDateTime(Now), null, null, Now));
            await db.SaveChangesAsync();
        }

        var summary = await ImportAsync(Email(Says(EmailKind.Rejection)));

        Assert.Equal(1, summary.NeedsReview);
        Assert.All(Applications(), a => Assert.Equal(ApplicationStatus.Applied, a.Status));
    }

    [Fact]
    public async Task Deleting_an_application_keeps_the_email_record_so_it_is_not_reimported()
    {
        var email = Email(Received(), messageId: "m1");
        await ImportAsync(email);
        await using (var db = NewDb())
        {
            await db.Applications.ExecuteDeleteAsync();
        }

        var again = await ImportAsync(email);

        Assert.Equal(1, again.AlreadyProcessed);
        Assert.Empty(Applications());
        Assert.Null(Imported("m1").ApplicationId);
    }

    // ---- Re-checking review items ------------------------------------------

    [Fact]
    public async Task A_review_item_that_the_parser_now_understands_is_resolved_on_recheck()
    {
        var email = Email(Received(company: null, role: null), messageId: "m1");
        await ImportAsync(email);
        Assert.Equal(ImportOutcome.NeedsReview, Imported("m1").Outcome);

        // The parser improved: it now finds the company and role.
        _parser.Results["m1"] = Received(company: "Entelect", role: "Developer");
        var summary = await RecheckAsync(["m1"], email);

        Assert.Equal(1, summary.Created);
        Assert.Equal(ImportOutcome.Created, Imported("m1").Outcome);
        Assert.Equal("Entelect", Applications().Single().Company);
    }

    [Fact]
    public async Task A_review_item_that_is_still_unclear_is_not_reported_again()
    {
        var email = Email(ParsedEmail.Unknown, messageId: "m1");
        await ImportAsync(email);

        var summary = await RecheckAsync(["m1"], email);

        Assert.Equal(ImportSummary.Empty, summary);
        Assert.Equal(ImportOutcome.NeedsReview, Imported("m1").Outcome); // still there, once
    }

    [Fact]
    public async Task Recheck_never_touches_emails_that_were_already_handled()
    {
        var email = Email(Received(), messageId: "m1");
        await ImportAsync(email);
        _parser.Results["m1"] = Says(EmailKind.Rejection);

        var summary = await RecheckAsync(["m1"], email);

        Assert.Equal(1, summary.AlreadyProcessed);
        Assert.Equal(ApplicationStatus.Applied, Applications().Single().Status);
    }

    private sealed class StubParser : IEmailParser
    {
        public Dictionary<string, ParsedEmail> Results { get; } = [];
        public ParsedEmail Parse(IncomingEmail email) => Results[email.MessageId];
    }
}
