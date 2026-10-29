using System.Net;
using System.Net.Http.Json;

namespace KnowledgeAssistant.Api.Tests;

// All tests share one database, so each test creates its own uniquely titled documents
// and never asserts on exact counts.
[Collection(ApiCollection.Name)]
public class DocumentEndpointsTests(PostgresApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostDocument_ValidRequest_Returns201WithLocationAndBody()
    {
        var request = new CreateDocumentRequest(UniqueTitle(), "Some content");

        var response = await _client.PostAsJsonAsync("/documents", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal(request.Title, body.Title);
        Assert.Equal(request.Content, body.Content);
        Assert.InRange(body.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Null(body.ProcessedAt);
        Assert.Equal($"/documents/{body.Id}", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("""{ "content": "Some content" }""")]
    [InlineData("""{ "title": "", "content": "Some content" }""")]
    [InlineData("""{ "title": "A title" }""")]
    public async Task PostDocument_InvalidRequest_Returns400(string json)
    {
        var response = await _client.PostAsync(
            "/documents", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostDocument_TitleTooLong_Returns400()
    {
        var request = new CreateDocumentRequest(new string('a', 201), "Some content");

        var response = await _client.PostAsJsonAsync("/documents", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDocumentById_Existing_ReturnsDocument()
    {
        var created = await CreateDocumentAsync(UniqueTitle());

        var response = await _client.GetAsync($"/documents/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.Equal(created, body);
    }

    [Fact]
    public async Task GetDocumentById_Unknown_Returns404()
    {
        var response = await _client.GetAsync($"/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDocuments_ReturnsCreatedDocumentsNewestFirst()
    {
        var first = await CreateDocumentAsync(UniqueTitle());
        var second = await CreateDocumentAsync(UniqueTitle());

        var documents = await _client.GetFromJsonAsync<List<DocumentResponse>>("/documents");

        Assert.NotNull(documents);
        var firstIndex = documents.FindIndex(d => d.Id == first.Id);
        var secondIndex = documents.FindIndex(d => d.Id == second.Id);
        Assert.True(firstIndex >= 0, "First document missing from the list.");
        Assert.True(secondIndex >= 0, "Second document missing from the list.");
        Assert.True(secondIndex < firstIndex, "Newer document should come first.");
    }

    private async Task<DocumentResponse> CreateDocumentAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/documents", new CreateDocumentRequest(title, "Some content"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentResponse>())!;
    }

    private static string UniqueTitle() => $"Test document {Guid.NewGuid()}";

    private sealed record CreateDocumentRequest(string Title, string Content);

    private sealed record DocumentResponse(
        Guid Id, string Title, string Content, DateTimeOffset CreatedAt, DateTimeOffset? ProcessedAt);
}
