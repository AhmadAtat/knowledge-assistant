using System.Net.Http.Json;
using KnowledgeAssistant.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace KnowledgeAssistant.Api.Tests;

// Listens on its own exclusive, server-named queue bound to the "documents" exchange. A direct
// exchange delivers a copy to every queue bound with the routing key, so this test sees the
// messages without consuming from the real "document-processing" queue, and the queue is
// deleted automatically when the test's connection closes.
[Collection(ApiCollection.Name)]
public class DocumentPublishingTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    private IConnection _connection = null!;
    private IChannel _channel = null!;
    private string _queue = null!;

    public async Task InitializeAsync()
    {
        _connection = await factory.RabbitMqOptions.CreateConnectionFactory("api-tests").CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        // The API declares the topology on its first publish; declare it here too so the exchange
        // exists before we bind to it, whatever order the tests run in.
        await RabbitMqTopology.DeclareAsync(_channel);

        // Empty name = let the server pick one. The defaults are exclusive and auto-delete.
        var queue = await _channel.QueueDeclareAsync();
        _queue = queue.QueueName;
        await _channel.QueueBindAsync(_queue, RabbitMqTopology.DocumentsExchange, RabbitMqTopology.DocumentCreatedRoutingKey);
    }

    public async Task DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task PostDocument_PublishesDocumentCreatedMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/documents", new { Title = $"Test document {Guid.NewGuid()}", Content = "Some content" });
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreatedDocument>())!;

        var message = await WaitForMessageAsync(created.Id, TimeSpan.FromSeconds(10));

        Assert.NotNull(message);
        Assert.True(message.Persistent);
        Assert.Equal(MessageSerializer.ContentType, message.ContentType);
        Assert.Equal(nameof(DocumentCreated), message.Type);
        Assert.False(string.IsNullOrEmpty(message.MessageId));
    }

    // Pulls messages one by one (BasicGet) until the one for this document shows up.
    private async Task<ReceivedMessage?> WaitForMessageAsync(Guid documentId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await _channel.BasicGetAsync(_queue, autoAck: true);
            if (result is null)
            {
                await Task.Delay(100);
                continue;
            }

            var payload = MessageSerializer.Deserialize<DocumentCreated>(result.Body.Span);
            if (payload?.DocumentId == documentId)
            {
                var properties = result.BasicProperties;
                return new ReceivedMessage(properties.Persistent, properties.ContentType, properties.Type, properties.MessageId);
            }
        }

        return null;
    }

    private sealed record CreatedDocument(Guid Id);

    private sealed record ReceivedMessage(bool Persistent, string? ContentType, string? Type, string? MessageId);
}
