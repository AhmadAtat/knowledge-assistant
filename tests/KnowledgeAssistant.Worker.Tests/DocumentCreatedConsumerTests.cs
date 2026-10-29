using System.Text;
using KnowledgeAssistant.Infrastructure.Data;
using KnowledgeAssistant.Infrastructure.Documents;
using KnowledgeAssistant.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace KnowledgeAssistant.Worker.Tests;

// End-to-end through the broker: publish a message, let the running Worker host consume it,
// then check the database or the dead-letter queue.
[Collection(WorkerCollection.Name)]
public class DocumentCreatedConsumerTests(WorkerFixture fixture)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task DocumentCreated_ExistingDocument_SetsProcessedAt()
    {
        var documentId = await SeedDocumentAsync();

        await fixture.PublishAsync(MessageSerializer.Serialize(new DocumentCreated(documentId)), Guid.NewGuid().ToString());

        var processed = await Eventually.TrueWithinAsync(
            async () => await GetProcessedAtAsync(documentId) is not null, Timeout);
        Assert.True(processed, "Worker did not set ProcessedAt in time.");
    }

    [Fact]
    public async Task MalformedMessage_IsDeadLettered()
    {
        var messageId = Guid.NewGuid().ToString();

        await fixture.PublishAsync(Encoding.UTF8.GetBytes("this is not json"), messageId);

        await using var connection = await fixture.RabbitMqOptions.CreateConnectionFactory("worker-tests").CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var deadLettered = await Eventually.TrueWithinAsync(async () =>
        {
            var result = await channel.BasicGetAsync(RabbitMqTopology.DocumentProcessingDeadLetterQueue, autoAck: true);
            return result?.BasicProperties.MessageId == messageId;
        }, Timeout);
        Assert.True(deadLettered, "Malformed message did not reach the dead-letter queue in time.");
    }

    private async Task<Guid> SeedDocumentAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = new Document
        {
            Id = Guid.CreateVersion7(),
            Title = $"Test document {Guid.NewGuid()}",
            Content = "one two three",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    private async Task<DateTimeOffset?> GetProcessedAtAsync(Guid documentId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => d.ProcessedAt)
            .SingleAsync();
    }
}
