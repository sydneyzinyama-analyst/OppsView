using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;

namespace OppsView.Api.Infrastructure.Messaging;

/// <summary>
/// Sends waiting outbox events to RabbitMQ, oldest first, and tidies up old
/// ones. Run every few seconds by <see cref="OutboxPublisherService"/>.
/// </summary>
public sealed class OutboxDispatcher(AppDbContext db, IMessagePublisher publisher, TimeProvider time, ILogger<OutboxDispatcher> logger)
{
    public const int BatchSize = 100;
    private static readonly TimeSpan KeepPublished = TimeSpan.FromDays(7);
    private static readonly TimeSpan KeepUnpublished = TimeSpan.FromDays(30);

    /// <returns>How many events were sent.</returns>
    public async Task<int> DispatchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await PruneAsync(now, ct);
        if (!publisher.IsConfigured) return 0;

        var pending = await db.Set<OutboxMessage>()
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message, ct);
                message.MarkPublished(time.GetUtcNow().UtcDateTime);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Stop at the first failure so events stay in order; this
                // one is retried on the next run.
                message.MarkFailed(ex.Message);
                logger.LogWarning(ex, "Could not publish outbox event {Id} ({Type}); will retry", message.Id, message.Type);
                break;
            }
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }

    private async Task PruneAsync(DateTime now, CancellationToken ct)
    {
        var publishedBefore = now - KeepPublished;
        var abandonedBefore = now - KeepUnpublished;
        await db.Set<OutboxMessage>()
            .Where(m => m.PublishedAt != null && m.PublishedAt < publishedBefore
                        || m.PublishedAt == null && m.OccurredAt < abandonedBefore)
            .ExecuteDeleteAsync(ct);
    }
}

/// <summary>Background job: runs the dispatcher every few seconds while the API is up.</summary>
public sealed class OutboxPublisherService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IConfiguration config,
    IMessagePublisher publisher,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = config.GetValue("Outbox:PollSeconds", 5);
        if (seconds <= 0) return; // switched off (the tests run the dispatcher themselves)
        if (!publisher.IsConfigured)
            logger.LogInformation("RabbitMQ isn't set up (RabbitMq:Url); events will wait in the outbox until it is");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds), time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Outbox dispatch failed; will retry");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
