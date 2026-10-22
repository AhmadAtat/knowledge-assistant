using KnowledgeAssistant.Api.Data;
using KnowledgeAssistant.Api.Documents;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Read the connection string lazily (when the first DbContext is created) so that
// configuration overrides from tests (WebApplicationFactory) are always respected.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Default");
    if (connectionString is null)
    {
        // `dotnet ef migrations add` only builds the model and needs no database connection.
        if (EF.IsDesignTime)
        {
            options.UseNpgsql();
            return;
        }

        throw new InvalidOperationException(
            "Connection string 'ConnectionStrings:Default' is not configured. " +
            "Use 'dotnet user-secrets' for local development or the ConnectionStrings__Default environment variable.");
    }

    options.UseNpgsql(connectionString);
});

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
