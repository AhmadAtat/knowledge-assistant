using KnowledgeAssistant.Infrastructure.Data;
using KnowledgeAssistant.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace KnowledgeAssistant.Worker.Tests;

// Runs the real Worker host (same AddDocumentProcessing() as Program.cs) against throwaway
// PostgreSQL and RabbitMQ containers. Shared by all test classes in the "Worker" collection.
public sealed class WorkerFixture : IAsyncLifetime
{
    private const string RabbitMqUsername = "test";
    private const string RabbitMqPassword = "test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4")
        .WithUsername(RabbitMqUsername)
        .WithPassword(RabbitMqPassword)
        .Build();

    private IHost _host = null!;

    public IServiceProvider Services => _host.Services;

    public RabbitMqOptions RabbitMqOptions => new()
    {
        Host = _rabbitMq.Hostname,
        Port = _rabbitMq.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort),
        Username = RabbitMqUsername,
        Password = RabbitMqPassword,
    };

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        var builder = Host.CreateApplicationBuilder();
        var rabbitMq = RabbitMqOptions;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
            ["RabbitMq:Host"] = rabbitMq.Host,
            ["RabbitMq:Port"] = rabbitMq.Port.ToString(),
            ["RabbitMq:Username"] = rabbitMq.Username,
            ["RabbitMq:Password"] = rabbitMq.Password,
        });
        builder.Services.AddDocumentProcessing();
        _host = builder.Build();

        // In production the API applies migrations; there is no API here, so do it ourselves.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        await _host.StartAsync();
    }

    // Publishes raw bytes to the documents exchange, like the API does (or a buggy publisher might).
    public async Task PublishAsync(byte[] body, string messageId)
    {
        await using var connection = await RabbitMqOptions.CreateConnectionFactory("worker-tests").CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        // The Worker declares the topology when it starts, but that runs in the background:
        // declare it here too so the exchange and queue exist before we publish.
        await RabbitMqTopology.DeclareAsync(channel);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = MessageSerializer.ContentType,
            MessageId = messageId,
        };
        await channel.BasicPublishAsync(
            RabbitMqTopology.DocumentsExchange, RabbitMqTopology.DocumentCreatedRoutingKey,
            mandatory: true, properties, body);
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class WorkerCollection : ICollectionFixture<WorkerFixture>
{
    public const string Name = "Worker";
}
