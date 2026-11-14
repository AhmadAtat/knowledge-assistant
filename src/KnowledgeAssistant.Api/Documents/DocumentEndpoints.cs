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
        group.MapPost("/{id:guid}/reprocess", ReprocessDocument);

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

        // A failed publish still returns 201: a client retry would create a duplicate document.
        await TryPublishDocumentCreatedAsync(
            publisher, loggerFactory.CreateLogger(typeof(DocumentEndpoints)), document.Id);

        return TypedResults.Created($"/documents/{document.Id}", DocumentResponse.From(document));
    }

    // Never throws: the document is already committed, so a broker problem only leaves it
    // unprocessed (ProcessedAt = null). Returns whether the publish succeeded; each caller decides
    // what that means for the response. Closing this "dual-write" gap reliably needs a transactional outbox.
    private static async Task<bool> TryPublishDocumentCreatedAsync(
        IDocumentEventPublisher publisher, ILogger logger, Guid documentId)
    {
        // Own timeout instead of the request token: a client disconnecting after the save
        // must not cancel the publish, and a dead broker must not hang the request.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await publisher.PublishDocumentCreatedAsync(documentId, timeout.Token);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish DocumentCreated for document {DocumentId}; it stays unprocessed", documentId);
            return false;
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

    // Sends the document to the Worker again. Resetting ProcessedAt first means the Worker's
    // idempotency check ("already processed; skipping") lets it through, without the Worker
    // needing to know about reprocessing at all.
    private static async Task<Results<Accepted, NotFound, ProblemHttpResult>> ReprocessDocument(
        Guid id, AppDbContext db, IDocumentEventPublisher publisher,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        // One UPDATE statement, no entity loaded; 0 affected rows means the document doesn't exist.
        var updated = await db.Documents
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ProcessedAt, (DateTimeOffset?)null), cancellationToken);
        if (updated == 0)
        {
            return TypedResults.NotFound();
        }

        // Unlike CreateDocument, a failed publish is reported: retrying a reprocess creates no duplicate.
        var published = await TryPublishDocumentCreatedAsync(
            publisher, loggerFactory.CreateLogger(typeof(DocumentEndpoints)), id);

        return published
            ? TypedResults.Accepted($"/documents/{id}")
            : TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The document could not be queued for processing. Try again later.");
    }
}
