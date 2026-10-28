using System.Net;
using System.Net.Http.Json;

namespace KnowledgeAssistant.Api.Tests;

[Collection(ApiCollection.Name)]
public class HealthEndpointTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyStatus()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", body?.Status);
    }

    private sealed record HealthResponse(string Status);
}
