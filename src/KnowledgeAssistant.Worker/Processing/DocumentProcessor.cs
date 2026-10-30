using KnowledgeAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeAssistant.Worker.Processing;

// The actual work for one DocumentCreated message. Scoped, like the DbContext it uses:
// the consumer creates a new DI scope per message.
public sealed class DocumentProcessor(AppDbContext db, ILogger<DocumentProcessor> logger)
{
    public async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
        {
            // Retrying won't make it appear, so this counts as handled (the message gets acked).
            logger.LogWarning("Document {DocumentId} not found; skipping", documentId);
            return;
        }

        // RabbitMQ delivers at least once, so the same message can arrive twice. Processing must be idempotent.
        if (document.ProcessedAt is not null)
        {
            logger.LogInformation(
                "Document {DocumentId} was already processed at {ProcessedAt}; skipping", documentId, document.ProcessedAt);
            return;
        }

        var wordCount = WordCounter.Count(document.Content);
        logger.LogInformation(
            "Document {DocumentId} \"{Title}\" has {WordCount} words", documentId, document.Title, wordCount);

        // PostgreSQL stores timestamps with microsecond precision, .NET with 100 ns ticks.
        var now = DateTimeOffset.UtcNow;
        document.ProcessedAt = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        await db.SaveChangesAsync(cancellationToken);
    }
}
