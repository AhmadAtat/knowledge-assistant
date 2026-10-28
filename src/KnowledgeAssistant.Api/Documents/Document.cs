namespace KnowledgeAssistant.Api.Documents;

public class Document
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
