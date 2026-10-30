using KnowledgeAssistant.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace KnowledgeAssistant.Api.Tests;

// Boots the API in memory against throwaway PostgreSQL and RabbitMQ containers (started via Testcontainers).
// Shared by all test classes in the "Api" collection, so the containers start once per test run.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string RabbitMqUsername = "test";
    private const string RabbitMqPassword = "test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4")
        .WithUsername(RabbitMqUsername)
        .WithPassword(RabbitMqPassword)
        .Build();

    // Same settings the API uses, so tests can open their own connection to the broker.
    public RabbitMqOptions RabbitMqOptions => new()
    {
        Host = _rabbitMq.Hostname,
        Port = _rabbitMq.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort),
        Username = RabbitMqUsername,
        Password = RabbitMqPassword,
    };

    public async Task InitializeAsync() => await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());

        var rabbitMq = RabbitMqOptions;
        builder.UseSetting("RabbitMq:Host", rabbitMq.Host);
        builder.UseSetting("RabbitMq:Port", rabbitMq.Port.ToString());
        builder.UseSetting("RabbitMq:Username", rabbitMq.Username);
        builder.UseSetting("RabbitMq:Password", rabbitMq.Password);
    }

    // WebApplicationFactory already has a public ValueTask DisposeAsync(), so xUnit's
    // Task-returning version is implemented explicitly to avoid a name clash.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
