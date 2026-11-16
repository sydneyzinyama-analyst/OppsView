using OppsView.Api.Infrastructure.Messaging;

namespace OppsView.Api.Tests.Api;

/// <summary>Stands in for RabbitMQ: records what would have been published.</summary>
public sealed class FakePublisher : IMessagePublisher
{
    public bool IsConfigured { get; set; } = true;
    public bool Down { get; set; }
    public List<OutboxMessage> Published { get; } = [];

    public Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        if (Down) throw new InvalidOperationException("RabbitMQ unreachable");
        Published.Add(message);
        return Task.CompletedTask;
    }
}
