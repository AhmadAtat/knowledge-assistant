namespace KnowledgeAssistant.Infrastructure.Documents;

public class Document
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Set by the Worker once background processing has finished; null means "not processed yet".
    public DateTimeOffset? ProcessedAt { get; set; }
}
