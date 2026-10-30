using RabbitMQ.Client;

namespace KnowledgeAssistant.Infrastructure.Messaging;

// The exchanges, queues and bindings for document processing:
//
//   publisher ──"document.created"──▶ [documents] (direct) ──▶ (document-processing) ──▶ Worker
//                                                                   │ rejected messages
//                                                                   ▼
//                              [documents.dead-letter] (fanout) ──▶ (document-processing.dead-letter)
//
// Both the API and the Worker declare it. Declaring is idempotent, so whichever starts first
// creates everything and messages are never dropped for lack of a queue. Re-declaring with
// *different* settings fails (PRECONDITION_FAILED), so all declarations live here.
public static class RabbitMqTopology
{
    public const string DocumentsExchange = "documents";
    public const string DocumentCreatedRoutingKey = "document.created";
    public const string DocumentProcessingQueue = "document-processing";

    public const string DeadLetterExchange = "documents.dead-letter";
    public const string DocumentProcessingDeadLetterQueue = "document-processing.dead-letter";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            DocumentProcessingDeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            DocumentProcessingDeadLetterQueue, DeadLetterExchange, routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            DocumentsExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            DocumentProcessingQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            DocumentProcessingQueue, DocumentsExchange, DocumentCreatedRoutingKey,
            cancellationToken: cancellationToken);
    }
}
