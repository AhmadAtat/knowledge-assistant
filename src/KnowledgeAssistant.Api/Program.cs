using KnowledgeAssistant.Api.Documents;
using KnowledgeAssistant.Api.Messaging;
using KnowledgeAssistant.Infrastructure.Data;
using KnowledgeAssistant.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDbContext();

// Bound lazily (on first use), so configuration overrides from tests are respected.
builder.Services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName);
builder.Services.AddSingleton<IDocumentEventPublisher, RabbitMqDocumentEventPublisher>();

// Validates DataAnnotations on endpoint parameters (e.g. CreateDocumentRequest) and returns 400 on failure.
builder.Services.AddValidation();

var app = builder.Build();

// Apply pending EF Core migrations on startup so every environment (compose, IDE, tests) gets the schema.
// Fine for a single instance; with multiple replicas this should become a separate migration step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapDocumentEndpoints();

app.Run();
