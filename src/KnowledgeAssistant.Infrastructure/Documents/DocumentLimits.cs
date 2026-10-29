namespace KnowledgeAssistant.Infrastructure.Documents;

// Shared by the DB schema (AppDbContext) and request validation, so they can't drift apart.
public static class DocumentLimits
{
    public const int TitleMaxLength = 200;
}
