namespace KnowledgeAssistant.Api.Messaging;

public interface IDocumentEventPublisher
{
    Task PublishDocumentCreatedAsync(Guid documentId, CancellationToken cancellationToken);
}
