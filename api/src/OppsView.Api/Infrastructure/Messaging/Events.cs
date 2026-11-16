using OppsView.Api.Domain;

namespace OppsView.Api.Infrastructure.Messaging;

/// <summary>
/// The events OppsView announces on RabbitMQ (exchange "oppsview.events").
/// Other services, like the Java notifier, subscribe to them.
///
/// Each event carries the application's whole current state ("event-carried
/// state transfer"), so a subscriber can keep its own copy up to date
/// without ever calling back into this API.
/// </summary>
public static class EventTypes
{
    public const string Exchange = "oppsview.events";
    public const string ApplicationChanged = "application.changed";
    public const string ApplicationDeleted = "application.deleted";
    public const string ReminderSettingsChanged = "user.reminder-settings-changed";

    /// <summary>Sent by the notifier; the API listens for it.</summary>
    public const string ReminderDue = "reminder.due";
}

/// <summary>Sent when an application is created, or its status or main details change.</summary>
public sealed record ApplicationChangedEvent(
    Guid ApplicationId,
    Guid UserId,
    string Company,
    string Role,
    JobPlatform Platform,
    ApplicationStatus Status,
    ApplicationStatus? PreviousStatus,
    DateOnly AppliedOn,
    DateTime StatusChangedAt,
    DateTime OccurredAt,
    int Version = 1);

public sealed record ApplicationDeletedEvent(Guid ApplicationId, Guid UserId, DateTime OccurredAt, int Version = 1);

/// <summary>Sent when a user changes their reminder settings, so the notifier re-plans.</summary>
public sealed record ReminderSettingsChangedEvent(
    Guid UserId,
    bool Enabled,
    int FollowUpDays,
    int MoveOnDays,
    int AfterInterviewDays,
    int OfferReplyDays,
    DateTime OccurredAt,
    int Version = 1);

/// <summary>"reminder.due" from the notifier (incoming).</summary>
public sealed record ReminderDueEvent(
    Guid ReminderId,
    Guid ApplicationId,
    Guid UserId,
    string Kind,
    string Company,
    string Role,
    string Message,
    DateTime DueAt,
    int Version);
