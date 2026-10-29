using System.Text.Json;
using KnowledgeAssistant.Infrastructure.Messaging;
using KnowledgeAssistant.Worker.Processing;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace KnowledgeAssistant.Worker.Messaging;

// Long-running background service: subscribes to the document-processing queue and handles
// each DocumentCreated message. Messages are acked only after processing succeeded.
public sealed class DocumentCreatedConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentCreatedConsumer> logger) : BackgroundService
{
    // How many unacked messages the broker may hand us at once.
    private const ushort PrefetchCount = 5;
    private const int MaxConnectAttempts = 10;
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = await ConnectAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await RabbitMqTopology.DeclareAsync(_channel, stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(_channel, delivery, stoppingToken);

        // autoAck: false = we confirm each message ourselves (BasicAck/BasicNack below).
        await _channel.BasicConsumeAsync(
            RabbitMqTopology.DocumentProcessingQueue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Consuming messages from queue {Queue}", RabbitMqTopology.DocumentProcessingQueue);

        // Deliveries arrive through the event handler; just stay alive until the host shuts down.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        // The body is only valid inside this handler, so deserialize it right away.
        var message = TryDeserialize(delivery.Body);
        if (message is null)
        {
            // Retrying can never fix a malformed message: reject it without requeue → dead-letter queue.
            logger.LogError(
                "Rejecting malformed message {MessageId}; it goes to the dead-letter queue", delivery.BasicProperties.MessageId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<DocumentProcessor>();
            await processor.ProcessAsync(message.DocumentId, stoppingToken);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down mid-message: don't ack. The broker redelivers unacked messages
            // once this channel closes, so nothing is lost.
        }
        catch (Exception ex)
        {
            // Retry once (requeue), then give up (dead-letter) so a failing message can't loop forever.
            var requeue = !delivery.Redelivered;
            logger.LogError(
                ex, "Processing document {DocumentId} failed; {Outcome}",
                message.DocumentId, requeue ? "requeueing for one retry" : "moving it to the dead-letter queue");
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue, stoppingToken);
        }
    }

    private static DocumentCreated? TryDeserialize(ReadOnlyMemory<byte> body)
    {
        try
        {
            var message = MessageSerializer.Deserialize<DocumentCreated>(body.Span);
            return message is null || message.DocumentId == Guid.Empty ? null : message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // In Docker Compose RabbitMQ is healthy before the Worker starts, but when run locally
    // (or after a broker restart) it may not be reachable yet, so retry for a while before giving up.
    private async Task<IConnection> ConnectAsync(CancellationToken stoppingToken)
    {
        var factory = options.Value.CreateConnectionFactory("knowledge-assistant-worker");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await factory.CreateConnectionAsync(stoppingToken);
            }
            catch (BrokerUnreachableException ex) when (attempt < MaxConnectAttempts)
            {
                logger.LogWarning(
                    ex, "RabbitMQ not reachable (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}",
                    attempt, MaxConnectAttempts, ConnectRetryDelay);
                await Task.Delay(ConnectRetryDelay, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Cancels ExecuteAsync and waits for it, then closes the channel and connection cleanly.
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
