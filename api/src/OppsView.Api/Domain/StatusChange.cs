namespace OppsView.Api.Domain;

/// <summary>
/// One row of an application's status history. Only created through
/// <see cref="JobApplication.ChangeStatus"/>, so history can't drift from
/// the application's current status.
/// </summary>
public class StatusChange
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public ApplicationStatus FromStatus { get; private set; }
    public ApplicationStatus ToStatus { get; private set; }
    public DateTime ChangedAt { get; private set; }

    // EF Core needs a parameterless constructor to materialise rows.
    private StatusChange() { }

    internal StatusChange(Guid applicationId, ApplicationStatus from, ApplicationStatus to, DateTime changedAt)
    {
        Id = Guid.NewGuid();
        ApplicationId = applicationId;
        FromStatus = from;
        ToStatus = to;
        ChangedAt = changedAt;
    }
}
