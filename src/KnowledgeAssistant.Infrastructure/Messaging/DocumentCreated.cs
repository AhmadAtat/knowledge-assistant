namespace KnowledgeAssistant.Infrastructure.Messaging;

// Published by the API after a document is saved, and again by POST /documents/{id}/reprocess
// (after resetting ProcessedAt), so in practice it means "process this document". Carries only
// the id: the consumer loads the current state from the database, so the message stays small
// and never goes stale.
public sealed record DocumentCreated(Guid DocumentId);
