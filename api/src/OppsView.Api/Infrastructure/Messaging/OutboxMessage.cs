namespace OppsView.Api.Infrastructure.Messaging;

/// <summary>
/// An event waiting to be sent to RabbitMQ (the "transactional outbox").
///
/// Events are written to this table in the same database transaction as the
/// change they describe, then sent by <see cref="OutboxDispatcher"/>. So an
/// event can never be lost (it's saved with the change), and never sent for
/// a change that didn't happen (it's rolled back with it). If RabbitMQ is
/// down, events simply wait here.
/// </summary>
public class OutboxMessage
{
    public const int TypeMaxLength = 100;
    public const int ErrorMaxLength = 1000;

    public Guid Id { get; private set; }

    /// <summary>The event name, also the RabbitMQ routing key ("application.changed").</summary>
    public string Type { get; private set; } = null!;

    /// <summary>The event as JSON.</summary>
    public string Payload { get; private set; } = null!;

    public DateTime OccurredAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, string payload, DateTime occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Payload = payload,
        OccurredAt = occurredAt,
    };

    public void MarkPublished(DateTime nowUtc)
    {
        PublishedAt = nowUtc;
        Attempts++;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length <= ErrorMaxLength ? error : error[..ErrorMaxLength];
    }
}
