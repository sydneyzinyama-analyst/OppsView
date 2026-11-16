using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OppsView.Api.Domain;

namespace OppsView.Api.Infrastructure.Messaging;

/// <summary>
/// Hooks into every SaveChanges (an EF Core "interceptor") and writes an
/// outbox event for each application that was added, changed or deleted,
/// and for each change to a user's reminder settings.
///
/// Doing it here, rather than in each service, means no code path can forget:
/// manual edits, status changes, email import and "no reply" rejections all
/// produce events, and always in the same transaction as the change.
/// </summary>
public sealed class OutboxInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Changes to these are worth announcing; editing notes or the job link isn't.
    private static readonly string[] Announced =
    [
        nameof(JobApplication.Company), nameof(JobApplication.Role), nameof(JobApplication.Platform),
        nameof(JobApplication.Status), nameof(JobApplication.AppliedOn),
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddOutboxMessages(DbContext? context)
    {
        if (context is null) return;
        context.ChangeTracker.DetectChanges();
        var now = time.GetUtcNow().UtcDateTime;

        var messages = new List<OutboxMessage>();
        foreach (var entry in context.ChangeTracker.Entries<JobApplication>())
        {
            var a = entry.Entity;
            switch (entry.State)
            {
                case EntityState.Added:
                    messages.Add(Changed(a, previous: null, now));
                    break;
                case EntityState.Modified when Announced.Any(p => entry.Property(p).IsModified):
                    messages.Add(Changed(a, PreviousStatus(entry), now));
                    break;
                case EntityState.Deleted:
                    messages.Add(OutboxMessage.Create(EventTypes.ApplicationDeleted,
                        JsonSerializer.Serialize(new ApplicationDeletedEvent(a.Id, a.UserId, now), Json), now));
                    break;
            }
        }
        foreach (var entry in context.ChangeTracker.Entries<ReminderSettings>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var r = entry.Entity;
            messages.Add(OutboxMessage.Create(EventTypes.ReminderSettingsChanged, JsonSerializer.Serialize(
                new ReminderSettingsChangedEvent(r.UserId, r.Enabled, r.FollowUpDays, r.MoveOnDays, r.AfterInterviewDays,
                    r.OfferReplyDays, now), Json), now));
        }

        context.Set<OutboxMessage>().AddRange(messages);
    }

    private static ApplicationStatus? PreviousStatus(EntityEntry<JobApplication> entry)
    {
        var status = entry.Property(e => e.Status);
        return status.IsModified ? status.OriginalValue : null;
    }

    private static OutboxMessage Changed(JobApplication a, ApplicationStatus? previous, DateTime now) =>
        OutboxMessage.Create(EventTypes.ApplicationChanged, JsonSerializer.Serialize(new ApplicationChangedEvent(
            a.Id, a.UserId, a.Company, a.Role, a.Platform, a.Status, previous, a.AppliedOn,
            DateTime.SpecifyKind(a.StatusChangedAt, DateTimeKind.Utc), now), Json), now);
}
