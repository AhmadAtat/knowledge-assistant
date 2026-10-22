using System.ComponentModel.DataAnnotations;

namespace KnowledgeAssistant.Api.Documents;

public record CreateDocumentRequest(
    [Required, MaxLength(DocumentLimits.TitleMaxLength)] string Title,
    [Required] string Content);

public record DocumentResponse(Guid Id, string Title, string Content, DateTimeOffset CreatedAt)
{
    public static DocumentResponse From(Document document) =>
        new(document.Id, document.Title, document.Content, document.CreatedAt);
}
