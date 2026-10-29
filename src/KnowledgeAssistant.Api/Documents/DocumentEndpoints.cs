using KnowledgeAssistant.Api.Messaging;
using KnowledgeAssistant.Infrastructure.Data;
using KnowledgeAssistant.Infrastructure.Documents;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeAssistant.Api.Documents;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/documents");

        group.MapPost("/", CreateDocument);
        group.MapGet("/{id:guid}", GetDocumentById);
        group.MapGet("/", GetDocuments);

        return app;
    }

    private static async Task<Created<DocumentResponse>> CreateDocument(
        CreateDocumentRequest request, AppDbContext db, IDocumentEventPublisher publisher,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        // PostgreSQL stores timestamps with microsecond precision, .NET with 100 ns ticks.
        // Truncate up front so the POST response matches what a later GET returns.
        var now = DateTimeOffset.UtcNow;
        var document = new Document
        {
            Id = Guid.CreateVersion7(),
            Title = request.Title,
            Content = request.Content,
            CreatedAt = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)),
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        await TryPublishDocumentCreatedAsync(
            publisher, loggerFactory.CreateLogger(typeof(DocumentEndpoints)), document.Id);

        return TypedResults.Created($"/documents/{document.Id}", DocumentResponse.From(document));
    }

    // The document is already committed, so a broker problem must not turn into an error response
    // (a client retry would create a duplicate). The document then simply stays unprocessed
    // (ProcessedAt = null). Closing this "dual-write" gap reliably needs a transactional outbox.
    private static async Task TryPublishDocumentCreatedAsync(
        IDocumentEventPublisher publisher, ILogger logger, Guid documentId)
    {
        // Own timeout instead of the request token: a client disconnecting after the save
        // must not cancel the publish, and a dead broker must not hang the request.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await publisher.PublishDocumentCreatedAsync(documentId, timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish DocumentCreated for document {DocumentId}; it stays unprocessed", documentId);
        }
    }

    private static async Task<Results<Ok<DocumentResponse>, NotFound>> GetDocumentById(
        Guid id, AppDbContext db, CancellationToken cancellationToken)
    {
        var document = await db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return document is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(DocumentResponse.From(document));
    }

    // Returns all documents, newest first. Paging comes later once the list can grow large.
    private static async Task<Ok<List<DocumentResponse>>> GetDocuments(
        AppDbContext db, CancellationToken cancellationToken)
    {
        var documents = await db.Documents.AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentResponse(d.Id, d.Title, d.Content, d.CreatedAt, d.ProcessedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(documents);
    }
}
