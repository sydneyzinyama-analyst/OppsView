namespace OppsView.Api.Domain;

/// <summary>
/// A single job application. All changes go through methods on this class
/// (private setters), so the rules below are enforced in one place no matter
/// which endpoint or service touches it:
///   - company and role are required (and length-limited)
///   - AppliedOn can't be in the future (allowing for time zones)
///   - new applications start as Applied
///   - status changes follow <see cref="StatusTransitions"/> and are recorded in History
/// </summary>
public class JobApplication
{
    public const int CompanyMaxLength = 200;
    public const int RoleMaxLength = 200;
    public const int JobUrlMaxLength = 2048;
    public const int NotesMaxLength = 4000;

    private readonly List<StatusChange> _history = [];

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Company { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public JobPlatform Platform { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public DateOnly AppliedOn { get; private set; }
    public string? JobUrl { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Read-only view; EF Core fills the private list behind it.</summary>
    public IReadOnlyList<StatusChange> History => _history;

    private JobApplication() { }

    public static JobApplication Create(
        Guid userId,
        string company,
        string role,
        JobPlatform platform,
        DateOnly appliedOn,
        string? jobUrl,
        string? notes,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A user is required.", nameof(userId));

        var application = new JobApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = ApplicationStatus.Applied,
            CreatedAt = nowUtc,
        };
        application.ApplyDetails(company, role, platform, appliedOn, jobUrl, notes, nowUtc);
        return application;
    }

    /// <summary>
    /// Edits the descriptive fields. Status is deliberately not editable here;
    /// use <see cref="ChangeStatus"/> so the change is validated and recorded.
    /// </summary>
    public void UpdateDetails(
        string company,
        string role,
        JobPlatform platform,
        DateOnly appliedOn,
        string? jobUrl,
        string? notes,
        DateTime nowUtc)
    {
        ApplyDetails(company, role, platform, appliedOn, jobUrl, notes, nowUtc);
    }

    public StatusChange ChangeStatus(ApplicationStatus newStatus, DateTime nowUtc)
    {
        if (!Enum.IsDefined(newStatus))
            throw new DomainException(nameof(Status), "Unknown status.");
        if (newStatus == Status)
            throw new DomainException(nameof(Status), $"Application is already {Status}.");
        if (!StatusTransitions.IsAllowed(Status, newStatus))
            throw new DomainException(nameof(Status), $"Can't move an application from {Status} to {newStatus}.");

        var change = new StatusChange(Id, Status, newStatus, nowUtc);
        _history.Add(change);
        Status = newStatus;
        UpdatedAt = nowUtc;
        return change;
    }

    private void ApplyDetails(
        string company,
        string role,
        JobPlatform platform,
        DateOnly appliedOn,
        string? jobUrl,
        string? notes,
        DateTime nowUtc)
    {
        company = (company ?? "").Trim();
        role = (role ?? "").Trim();
        jobUrl = string.IsNullOrWhiteSpace(jobUrl) ? null : jobUrl.Trim();
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (company.Length == 0)
            throw new DomainException(nameof(Company), "Company is required.");
        if (company.Length > CompanyMaxLength)
            throw new DomainException(nameof(Company), $"Company must be at most {CompanyMaxLength} characters.");
        if (role.Length == 0)
            throw new DomainException(nameof(Role), "Role is required.");
        if (role.Length > RoleMaxLength)
            throw new DomainException(nameof(Role), $"Role must be at most {RoleMaxLength} characters.");
        if (!Enum.IsDefined(platform))
            throw new DomainException(nameof(Platform), "Unknown platform.");
        // The client sends the user's *local* date, which can be a day ahead of
        // the UTC date (e.g. 01:00 in Johannesburg is 23:00 UTC the day before).
        // Allowing "UTC today + 1" accepts every time zone without accepting
        // genuinely future dates.
        if (appliedOn > DateOnly.FromDateTime(nowUtc).AddDays(1))
            throw new DomainException(nameof(AppliedOn), "Applied date can't be in the future.");
        if (jobUrl is not null && (jobUrl.Length > JobUrlMaxLength || !IsHttpUrl(jobUrl)))
            throw new DomainException(nameof(JobUrl), "Job URL must be a valid http(s) link.");
        if (notes is not null && notes.Length > NotesMaxLength)
            throw new DomainException(nameof(Notes), $"Notes must be at most {NotesMaxLength} characters.");

        Company = company;
        Role = role;
        Platform = platform;
        AppliedOn = appliedOn;
        JobUrl = jobUrl;
        Notes = notes;
        UpdatedAt = nowUtc;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
