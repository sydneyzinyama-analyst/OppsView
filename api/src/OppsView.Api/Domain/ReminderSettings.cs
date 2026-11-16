namespace OppsView.Api.Domain;

/// <summary>
/// A user's choices for follow-up reminders. Changes are announced on
/// RabbitMQ (via the outbox) so the notifier re-plans with them. 0 days
/// switches that reminder off.
/// </summary>
public class ReminderSettings
{
    public const int MaxDays = 90;

    public Guid UserId { get; private set; }
    public bool Enabled { get; private set; }
    public int FollowUpDays { get; private set; }
    public int MoveOnDays { get; private set; }
    public int AfterInterviewDays { get; private set; }
    public int OfferReplyDays { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ReminderSettings() { }

    /// <summary>The settings everyone starts with (the same as the notifier's defaults).</summary>
    public static ReminderSettings Default(Guid userId, DateTime nowUtc) => new()
    {
        UserId = userId,
        Enabled = true,
        FollowUpDays = 7,
        MoveOnDays = 21,
        AfterInterviewDays = 5,
        OfferReplyDays = 2,
        UpdatedAt = nowUtc,
    };

    public void Update(bool enabled, int followUpDays, int moveOnDays, int afterInterviewDays, int offerReplyDays, DateTime nowUtc)
    {
        Check(nameof(FollowUpDays), followUpDays);
        Check(nameof(MoveOnDays), moveOnDays);
        Check(nameof(AfterInterviewDays), afterInterviewDays);
        Check(nameof(OfferReplyDays), offerReplyDays);
        if (followUpDays > 0 && moveOnDays > 0 && moveOnDays <= followUpDays)
            throw new DomainException(nameof(MoveOnDays), "\"Move on\" should come after \"follow up\".");

        Enabled = enabled;
        FollowUpDays = followUpDays;
        MoveOnDays = moveOnDays;
        AfterInterviewDays = afterInterviewDays;
        OfferReplyDays = offerReplyDays;
        UpdatedAt = nowUtc;
    }

    private static void Check(string field, int days)
    {
        if (days is < 0 or > MaxDays) throw new DomainException(field, $"Choose 0 (off) to {MaxDays} days.");
    }
}
