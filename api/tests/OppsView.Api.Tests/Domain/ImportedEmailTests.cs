namespace OppsView.Api.Tests.Domain;

public class ImportedEmailTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

    private static ImportedEmail NeedsReview() => ImportedEmail.Record(
        Guid.NewGuid(),
        new IncomingEmail("m1", "Jobs <noreply@example.com>", "About your application", "body", Now),
        ParsedEmail.Unknown,
        ImportOutcome.NeedsReview,
        applicationId: null,
        reason: "Couldn't tell what this email is about.",
        Now);

    [Fact]
    public void Record_keeps_sender_and_subject_but_no_body()
    {
        var email = NeedsReview();

        Assert.Equal("m1", email.MessageId);
        Assert.Equal("About your application", email.Subject);
        Assert.Equal(ImportOutcome.NeedsReview, email.Outcome);
        // There is no property that could hold the body: nothing to assert
        // beyond the type itself, which is the point.
        Assert.DoesNotContain(typeof(ImportedEmail).GetProperties(), p => p.Name.Contains("Body"));
    }

    [Fact]
    public void Record_truncates_very_long_subjects()
    {
        var email = ImportedEmail.Record(Guid.NewGuid(),
            new IncomingEmail("m2", "x@example.com", new string('s', 2000), "", Now),
            ParsedEmail.Unknown, ImportOutcome.Ignored, null, null, Now);

        Assert.Equal(ImportedEmail.SubjectMaxLength, email.Subject.Length);
    }

    [Fact]
    public void A_review_can_be_resolved_once()
    {
        var email = NeedsReview();
        var applicationId = Guid.NewGuid();

        email.ResolveWith(applicationId, ImportOutcome.Created, Now.AddHours(1));

        Assert.Equal(ImportOutcome.Created, email.Outcome);
        Assert.Equal(applicationId, email.ApplicationId);
        Assert.Throws<DomainException>(() => email.Dismiss(Now.AddHours(2)));
    }

    [Fact]
    public void A_review_can_be_dismissed()
    {
        var email = NeedsReview();
        email.Dismiss(Now.AddHours(1));
        Assert.Equal(ImportOutcome.Dismissed, email.Outcome);
    }
}
