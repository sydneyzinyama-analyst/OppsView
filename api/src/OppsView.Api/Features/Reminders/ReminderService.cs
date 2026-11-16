using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;
using OppsView.Api.Infrastructure;

namespace OppsView.Api.Features.Reminders;

/// <summary>The signed-in user's reminders (the bell) and their reminder settings.</summary>
public sealed class ReminderService(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;

    /// <summary>Open, not snoozed, and still relevant to the application's current status.</summary>
    public async Task<IReadOnlyList<ReminderResponse>> ListAsync(CancellationToken ct)
    {
        var now = Now;
        var rows = await (
                from r in db.Reminders.AsNoTracking()
                join a in db.Applications.AsNoTracking() on r.ApplicationId equals a.Id
                where r.UserId == currentUser.Id && r.State == ReminderState.Open
                      && (r.SnoozedUntil == null || r.SnoozedUntil <= now)
                select new { r, a.Company, a.Role, a.Status })
            .ToListAsync(ct);

        return rows
            .Where(x => Reminder.IsRelevantFor(x.r.Kind, x.Status))
            .OrderByDescending(x => x.r.DueAt)
            .Select(x => new ReminderResponse(x.r.Id, x.r.ApplicationId, x.Company, x.Role, x.r.Kind, x.r.Message,
                DateTime.SpecifyKind(x.r.DueAt, DateTimeKind.Utc)))
            .ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct) => (await ListAsync(ct)).Count;

    /// <returns>false if there's no such reminder for this user.</returns>
    public Task<bool> MarkDoneAsync(Guid id, CancellationToken ct) => UpdateAsync(id, r => r.MarkDone(Now), ct);

    public Task<bool> DismissAsync(Guid id, CancellationToken ct) => UpdateAsync(id, r => r.Dismiss(Now), ct);

    public Task<bool> SnoozeAsync(Guid id, int days, CancellationToken ct) => UpdateAsync(id, r => r.Snooze(days, Now), ct);

    private async Task<bool> UpdateAsync(Guid id, Action<Reminder> change, CancellationToken ct)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(r => r.Id == id && r.UserId == currentUser.Id, ct);
        if (reminder is null) return false;
        change(reminder);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ReminderSettingsResponse> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await db.ReminderSettings.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == currentUser.Id, ct);
        return ReminderSettingsResponse.From(settings ?? Domain.ReminderSettings.Default(currentUser.Id, Now));
    }

    /// <summary>Saving announces the change (via the outbox), and the notifier re-plans.</summary>
    public async Task<ReminderSettingsResponse> UpdateSettingsAsync(ReminderSettingsRequest r, CancellationToken ct)
    {
        var settings = await db.ReminderSettings.SingleOrDefaultAsync(s => s.UserId == currentUser.Id, ct);
        if (settings is null)
        {
            settings = Domain.ReminderSettings.Default(currentUser.Id, Now);
            db.ReminderSettings.Add(settings);
        }
        settings.Update(r.Enabled, r.FollowUpDays, r.MoveOnDays, r.AfterInterviewDays, r.OfferReplyDays, Now);
        await db.SaveChangesAsync(ct);
        return ReminderSettingsResponse.From(settings);
    }
}
