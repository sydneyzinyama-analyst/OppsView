using System.ComponentModel.DataAnnotations;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.Reminders;

public sealed record ReminderResponse(
    Guid Id,
    Guid ApplicationId,
    string Company,
    string Role,
    string Kind,
    string Message,
    DateTime DueAt);

public sealed record ReminderCountResponse(int Open);

public sealed class SnoozeRequest
{
    [Range(1, 30)] public int Days { get; init; } = 3;
}

public sealed class ReminderSettingsRequest
{
    public bool Enabled { get; init; } = true;
    [Range(0, ReminderSettings.MaxDays)] public int FollowUpDays { get; init; }
    [Range(0, ReminderSettings.MaxDays)] public int MoveOnDays { get; init; }
    [Range(0, ReminderSettings.MaxDays)] public int AfterInterviewDays { get; init; }
    [Range(0, ReminderSettings.MaxDays)] public int OfferReplyDays { get; init; }
}

public sealed record ReminderSettingsResponse(
    bool Enabled, int FollowUpDays, int MoveOnDays, int AfterInterviewDays, int OfferReplyDays)
{
    public static ReminderSettingsResponse From(ReminderSettings s) =>
        new(s.Enabled, s.FollowUpDays, s.MoveOnDays, s.AfterInterviewDays, s.OfferReplyDays);
}
