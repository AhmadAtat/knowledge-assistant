using KnowledgeAssistant.Api.Data;
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
        CreateDocumentRequest request, AppDbContext db, CancellationToken cancellationToken)
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

        return TypedResults.Created($"/documents/{document.Id}", DocumentResponse.From(document));
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
            .Select(d => new DocumentResponse(d.Id, d.Title, d.Content, d.CreatedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(documents);
    }
}
