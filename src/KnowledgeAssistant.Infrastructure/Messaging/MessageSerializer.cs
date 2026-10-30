using System.Text.Json;

namespace KnowledgeAssistant.Infrastructure.Messaging;

// One place for the wire format, so publisher and consumer can't disagree on JSON settings.
public static class MessageSerializer
{
    public const string ContentType = "application/json";

    public static byte[] Serialize<T>(T message) => JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web);

    public static T? Deserialize<T>(ReadOnlySpan<byte> body) => JsonSerializer.Deserialize<T>(body, JsonSerializerOptions.Web);
}
