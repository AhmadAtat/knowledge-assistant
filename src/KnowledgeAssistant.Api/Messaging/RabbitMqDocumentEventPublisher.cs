using KnowledgeAssistant.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace KnowledgeAssistant.Api.Messaging;

// Registered as a singleton: one TCP connection for the whole app (connections are expensive),
// and one channel on top of it. A channel must not be used by two threads at once, so
// publishes are serialized with a semaphore.
public sealed class RabbitMqDocumentEventPublisher(IOptions<RabbitMqOptions> options)
    : IDocumentEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishDocumentCreatedAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true, // written to disk by the broker, survives a RabbitMQ restart
            ContentType = MessageSerializer.ContentType,
            MessageId = Guid.NewGuid().ToString(),
            Type = nameof(DocumentCreated),
        };
        var body = MessageSerializer.Serialize(new DocumentCreated(documentId));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);

            // With publisher confirms enabled this completes only after the broker has taken
            // responsibility for the message. mandatory: true makes an unroutable message
            // (no matching queue) an error instead of being silently dropped.
            await channel.BasicPublishAsync(
                RabbitMqTopology.DocumentsExchange, RabbitMqTopology.DocumentCreatedRoutingKey,
                mandatory: true, properties, body, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    // Connects lazily on first publish, so the API starts even while RabbitMQ is down,
    // and reconnects on the next publish if the channel was closed.
    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await CloseAsync();

        _connection = await options.Value
            .CreateConnectionFactory("knowledge-assistant-api")
            .CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await RabbitMqTopology.DeclareAsync(_channel, cancellationToken);
        return _channel;
    }

    private async Task CloseAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _lock.Dispose();
    }
}
