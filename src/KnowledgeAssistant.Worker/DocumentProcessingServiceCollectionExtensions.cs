using KnowledgeAssistant.Infrastructure.Data;
using KnowledgeAssistant.Infrastructure.Messaging;
using KnowledgeAssistant.Worker.Messaging;
using KnowledgeAssistant.Worker.Processing;

namespace KnowledgeAssistant.Worker;

public static class DocumentProcessingServiceCollectionExtensions
{
    // Everything the Worker needs, in one call, so Program.cs and the tests build the same host.
    public static IServiceCollection AddDocumentProcessing(this IServiceCollection services)
    {
        services.AddAppDbContext();
        services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName);
        services.AddScoped<DocumentProcessor>();
        services.AddHostedService<DocumentCreatedConsumer>();
        return services;
    }
}
