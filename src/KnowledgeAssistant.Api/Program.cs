using KnowledgeAssistant.Api.Documents;
using KnowledgeAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDbContext();

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
