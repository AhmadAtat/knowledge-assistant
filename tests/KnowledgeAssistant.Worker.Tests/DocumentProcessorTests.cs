using KnowledgeAssistant.Infrastructure.Data;
using KnowledgeAssistant.Infrastructure.Documents;
using KnowledgeAssistant.Worker.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeAssistant.Worker.Tests;

// Calls DocumentProcessor directly (no broker involved) against the real database.
[Collection(WorkerCollection.Name)]
public class DocumentProcessorTests(WorkerFixture fixture)
{
    [Fact]
    public async Task ProcessAsync_UnprocessedDocument_SetsProcessedAt()
    {
        var documentId = await SeedDocumentAsync(processedAt: null);

        await ProcessAsync(documentId);

        var processedAt = await GetProcessedAtAsync(documentId);
        Assert.NotNull(processedAt);
        Assert.InRange(processedAt.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task ProcessAsync_AlreadyProcessed_LeavesProcessedAtUnchanged()
    {
        var originalProcessedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var documentId = await SeedDocumentAsync(originalProcessedAt);

        await ProcessAsync(documentId);

        Assert.Equal(originalProcessedAt, await GetProcessedAtAsync(documentId));
    }

    [Fact]
    public async Task ProcessAsync_UnknownDocument_DoesNotThrow()
    {
        await ProcessAsync(Guid.NewGuid());
    }

    private async Task ProcessAsync(Guid documentId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<DocumentProcessor>();
        await processor.ProcessAsync(documentId, CancellationToken.None);
    }

    private async Task<Guid> SeedDocumentAsync(DateTimeOffset? processedAt)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = new Document
        {
            Id = Guid.CreateVersion7(),
            Title = $"Test document {Guid.NewGuid()}",
            Content = "one two three",
            CreatedAt = DateTimeOffset.UtcNow,
            ProcessedAt = processedAt,
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
