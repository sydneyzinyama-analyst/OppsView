namespace OppsView.Api.Domain;

/// <summary>
/// One email OppsView has processed, and what it did with it.
///
/// It serves three purposes:
///   1. never process the same email twice (unique per user + message id)
///   2. the "Needs review" list: emails OppsView wasn't sure about
///   3. an audit trail: which email created or changed which application
///
/// Privacy: only the sender, subject and the details parsed out of the email
/// are kept. The email body is never stored.
/// </summary>
public class ImportedEmail
{
    public const int MessageIdMaxLength = 256;
    public const int SenderMaxLength = 320;
    public const int SubjectMaxLength = 500;
    public const int ReasonMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The mail provider's id for the message (Gmail message id).</summary>
    public string MessageId { get; private set; } = null!;

    public DateTime ReceivedAt { get; private set; }
    public string Sender { get; private set; } = null!;
    public string Subject { get; private set; } = null!;

    public EmailKind Kind { get; private set; }
    public JobPlatform? Platform { get; private set; }
    public string? Company { get; private set; }
    public string? Role { get; private set; }

    public ImportOutcome Outcome { get; private set; }

    /// <summary>Why the email needs review (or why it caused no change), in plain words.</summary>
    public string? Reason { get; private set; }

    /// <summary>The application this email created or updated, if any.</summary>
    public Guid? ApplicationId { get; private set; }

    public DateTime ProcessedAt { get; private set; }

    private ImportedEmail() { }

    public static ImportedEmail Record(
        Guid userId,
        IncomingEmail email,
        ParsedEmail parsed,
        ImportOutcome outcome,
        Guid? applicationId,
        string? reason,
        DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        MessageId = email.MessageId,
        ReceivedAt = email.ReceivedAt,
        Sender = Truncate(email.Sender, SenderMaxLength),
        Subject = Truncate(email.Subject, SubjectMaxLength),
        Kind = parsed.Kind,
        Platform = parsed.Platform,
        Company = parsed.Company is null ? null : Truncate(parsed.Company, JobApplication.CompanyMaxLength),
        Role = parsed.Role is null ? null : Truncate(parsed.Role, JobApplication.RoleMaxLength),
        Outcome = outcome,
        ApplicationId = applicationId,
        Reason = reason is null ? null : Truncate(reason, ReasonMaxLength),
        ProcessedAt = nowUtc,
    };

    /// <summary>The user reviewed this email and created an application from it.</summary>
    public void ResolveWith(Guid applicationId, ImportOutcome outcome, DateTime nowUtc)
    {
        EnsureNeedsReview();
        ApplicationId = applicationId;
        Outcome = outcome;
        ProcessedAt = nowUtc;
    }

    /// <summary>The user reviewed this email and decided it isn't relevant.</summary>
    public void Dismiss(DateTime nowUtc)
    {
        EnsureNeedsReview();
        Outcome = ImportOutcome.Dismissed;
        ProcessedAt = nowUtc;
    }

    private void EnsureNeedsReview()
    {
        if (Outcome != ImportOutcome.NeedsReview)
            throw new DomainException(nameof(Outcome), "This email has already been dealt with.");
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
