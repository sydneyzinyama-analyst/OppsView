using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;
using OppsView.Api.Infrastructure.Messaging;

namespace OppsView.Api.Features.Reminders;

/// <summary>
/// Stores a "reminder.due" message from the notifier. Kept apart from the
/// RabbitMQ plumbing (<see cref="ReminderConsumer"/>) so it's easy to test.
/// </summary>
public sealed class ReminderInbox(AppDbContext db, TimeProvider time, ILogger<ReminderInbox> logger)
{
    public enum Result { Stored, Duplicate, Ignored }

    /// <exception cref="JsonException">The message isn't valid JSON for a reminder (it will never succeed).</exception>
    public async Task<Result> HandleAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<ReminderDueEvent>(body.Span, OutboxInterceptor.Json)
            ?? throw new JsonException("Empty reminder message.");

        // RabbitMQ delivers "at least once": the same reminder can arrive twice.
        if (await db.Reminders.AnyAsync(r => r.Id == e.ReminderId, ct)) return Result.Duplicate;

        // The application may have been deleted after the notifier planned this.
        var exists = await db.Applications.AnyAsync(a => a.Id == e.ApplicationId && a.UserId == e.UserId, ct);
        if (!exists)
        {
            logger.LogInformation("Ignoring reminder {Id}: application {App} no longer exists", e.ReminderId, e.ApplicationId);
            return Result.Ignored;
        }

        db.Reminders.Add(Reminder.Receive(e.ReminderId, e.UserId, e.ApplicationId, e.Kind, e.Message,
            DateTime.SpecifyKind(e.DueAt.ToUniversalTime(), DateTimeKind.Utc), time.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync(ct);
        return Result.Stored;
    }
}
