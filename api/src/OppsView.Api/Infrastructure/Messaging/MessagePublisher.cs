using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OppsView.Api.Infrastructure.Messaging;

/// <summary>Settings for RabbitMQ, from the "RabbitMq" config section.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// amqps://user:password@host/vhost (CloudAMQP gives you this). A secret:
    /// user-secrets locally, an environment variable in the cloud. Empty means
    /// "not set up": events then wait in the outbox.
    /// </summary>
    public string? Url { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url);
}

public interface IMessagePublisher
{
    bool IsConfigured { get; }

    /// <summary>Sends one event; returns only once RabbitMQ has confirmed it has it.</summary>
    Task PublishAsync(OutboxMessage message, CancellationToken ct);
}

/// <summary>
/// Publishes to RabbitMQ (RabbitMQ.Client 7, fully async). One connection
/// for the whole app, opened on first use and reopened if it drops.
/// </summary>
public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        var channel = await GetChannelAsync(ct);
        var properties = new BasicProperties
        {
            MessageId = message.Id.ToString(), // lets subscribers ignore a duplicate delivery
            Type = message.Type,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent, // survives a RabbitMQ restart
            Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt, TimeSpan.Zero).ToUnixTimeSeconds()),
        };
        // With publisher confirms on, this waits until RabbitMQ has the
        // message (and throws if it refuses), so "published" really means it.
        await channel.BasicPublishAsync(EventTypes.Exchange, message.Type, mandatory: false, properties,
            Encoding.UTF8.GetBytes(message.Payload), ct);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        await _gate.WaitAsync(ct);
        try
        {
            if (_channel is { IsOpen: true }) return _channel;
            await DisposeConnectionAsync();

            var factory = new ConnectionFactory { Uri = new Uri(options.Value.Url!), ClientProvidedName = "oppsview-api" };
            _connection = await factory.CreateConnectionAsync(ct);
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                ct);
            // A "topic" exchange routes by the event name, so subscribers can
            // pick "application.*" or just one event. Durable: survives restarts.
            await _channel.ExchangeDeclareAsync(EventTypes.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
            logger.LogInformation("Connected to RabbitMQ");
            return _channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task DisposeConnectionAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _channel = null;
        _connection = null;
    }

    public async ValueTask DisposeAsync() => await DisposeConnectionAsync();
}
