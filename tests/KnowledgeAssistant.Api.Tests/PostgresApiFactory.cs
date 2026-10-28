using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace KnowledgeAssistant.Api.Tests;

// Boots the API in memory against a throwaway PostgreSQL container (started via Testcontainers).
// Shared by all test classes in the "Api" collection, so the container starts once per test run.
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());

    // WebApplicationFactory already has a public ValueTask DisposeAsync(), so xUnit's
    // Task-returning version is implemented explicitly to avoid a name clash.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PostgresApiFactory>
{
    public const string Name = "Api";
}
