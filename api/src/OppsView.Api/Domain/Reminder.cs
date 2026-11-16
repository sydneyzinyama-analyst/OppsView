namespace OppsView.Api.Domain;

public enum ReminderState
{
    Open,
    Done,
    Dismissed,
}

/// <summary>
/// A follow-up reminder for the user, planned and sent by the Java notifier
/// ("reminder.due" on RabbitMQ) and shown under the bell.
/// </summary>
public class Reminder
{
    public const int MessageMaxLength = 1000;
    public const int KindMaxLength = 32;

    /// <summary>The notifier's reminder id, so a message delivered twice is stored once.</summary>
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }
    public Guid ApplicationId { get; private set; }

    /// <summary>FOLLOW_UP, MOVE_ON, AFTER_INTERVIEW or REPLY_TO_OFFER.</summary>
    public string Kind { get; private set; } = null!;

    public string Message { get; private set; } = null!;
    public DateTime DueAt { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public ReminderState State { get; private set; }

    /// <summary>Hidden until then ("remind me later").</summary>
    public DateTime? SnoozedUntil { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    private Reminder() { }

    public static Reminder Receive(Guid id, Guid userId, Guid applicationId, string kind, string message, DateTime dueAt, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Kind is required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is required.", nameof(message));
        return new Reminder
        {
            Id = id,
            UserId = userId,
            ApplicationId = applicationId,
            Kind = kind.Length <= KindMaxLength ? kind : kind[..KindMaxLength],
            Message = message.Length <= MessageMaxLength ? message : message[..MessageMaxLength],
            DueAt = dueAt,
            ReceivedAt = nowUtc,
            State = ReminderState.Open,
        };
    }

    public bool IsVisible(DateTime nowUtc) => State == ReminderState.Open && (SnoozedUntil is null || SnoozedUntil <= nowUtc);

    /// <summary>
    /// A reminder only makes sense while the application is still where it
    /// was: "follow up" is moot once there's an interview, and nothing
    /// applies after a rejection.
    /// </summary>
    public static bool IsRelevantFor(string kind, ApplicationStatus status) => kind switch
    {
        "FOLLOW_UP" or "MOVE_ON" => status is ApplicationStatus.Applied or ApplicationStatus.Viewed,
        "AFTER_INTERVIEW" => status == ApplicationStatus.Interview,
        "REPLY_TO_OFFER" => status == ApplicationStatus.Offer,
        _ => status is not (ApplicationStatus.Rejected),
    };

    public void MarkDone(DateTime nowUtc) => Close(ReminderState.Done, nowUtc);

    public void Dismiss(DateTime nowUtc) => Close(ReminderState.Dismissed, nowUtc);

    public void Snooze(int days, DateTime nowUtc)
    {
        if (days is < 1 or > 30) throw new DomainException("Days", "Snooze for 1 to 30 days.");
        EnsureOpen();
        SnoozedUntil = nowUtc.AddDays(days);
    }

    private void Close(ReminderState state, DateTime nowUtc)
    {
        EnsureOpen();
        State = state;
        ClosedAt = nowUtc;
    }

    private void EnsureOpen()
    {
        if (State != ReminderState.Open) throw new DomainException(nameof(State), "This reminder has already been dealt with.");
    }
}
