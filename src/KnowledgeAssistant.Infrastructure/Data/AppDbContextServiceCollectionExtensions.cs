using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeAssistant.Infrastructure.Data;

public static class AppDbContextServiceCollectionExtensions
{
    // Shared by the API and the Worker so both connect to PostgreSQL the same way.
    public static IServiceCollection AddAppDbContext(this IServiceCollection services)
    {
        // Read the connection string lazily (when the first DbContext is created) so that
        // configuration overrides from tests (WebApplicationFactory, test hosts) are always respected.
        return services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default");
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
    }
}
