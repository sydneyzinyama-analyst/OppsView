using System.Text.Json;
using Microsoft.Extensions.Options;
using OppsView.Api.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OppsView.Api.Features.Reminders;

/// <summary>
/// Background listener: takes "reminder.due" messages off the API's own
/// queue on RabbitMQ and hands each one to <see cref="ReminderInbox"/>.
///
/// <code>
/// oppsview.events (topic) --reminder.due--> queue "api.reminders" --> here
/// failures (twice)        --> api.dead-letter --> queue "api.reminders.dlq"
/// </code>
///
/// A message is acknowledged only after it's saved, so nothing is lost if
/// the API stops halfway. Without RabbitMQ configured, this does nothing.
/// </summary>
public sealed class ReminderConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopes,
    ILogger<ReminderConsumer> logger) : BackgroundService
{
    public const string Queue = "api.reminders";
    public const string DeadLetterExchange = "api.dead-letter";
    public const string DeadLetterQueue = "api.reminders.dlq";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured) return;

        // Keep trying to connect: RabbitMQ may not be reachable yet.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Can't listen for reminders yet ({Message}); retrying in 30s", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        // Automatic recovery (on by default) reconnects and resumes consuming
        // if the connection drops later.
        var factory = new ConnectionFactory { Uri = new Uri(options.Value.Url!), ClientProvidedName = "oppsview-api-reminders" };
        var connection = await factory.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(EventTypes.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, routingKey: "", cancellationToken: ct);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange }, cancellationToken: ct);
        await channel.QueueBindAsync(Queue, EventTypes.Exchange, EventTypes.ReminderDue, cancellationToken: ct);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ReminderInbox>().HandleAsync(delivery.Body, ct);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
            }
            catch (JsonException ex)
            {
                // Unreadable: retrying won't help. Straight to the dead-letter queue.
                logger.LogWarning(ex, "Unreadable reminder message; sent to {Dlq}", DeadLetterQueue);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Probably temporary (database busy): try once more, then give up to the dead-letter queue.
                logger.LogWarning(ex, "Could not store reminder; {Action}", delivery.Redelivered ? "sent to dead-letter queue" : "will retry");
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: !delivery.Redelivered, ct);
            }
        };

        await channel.BasicConsumeAsync(Queue, autoAck: false, consumer, ct);
        logger.LogInformation("Listening for reminders on {Queue}", Queue);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        finally
        {
            await channel.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
