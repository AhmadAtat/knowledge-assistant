using RabbitMQ.Client;

namespace KnowledgeAssistant.Infrastructure.Messaging;

// Bound from the "RabbitMq" configuration section: RabbitMq__Host etc. in Docker Compose,
// user secrets when running locally.
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    // 127.0.0.1 rather than localhost: Compose publishes RabbitMQ on IPv4 only.
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5672;
    public string? Username { get; set; }
    public string? Password { get; set; }

    // clientName shows up in the management UI (Connections tab), so you can tell the API from the Worker.
    public ConnectionFactory CreateConnectionFactory(string clientName)
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
        {
            throw new InvalidOperationException(
                "RabbitMQ credentials 'RabbitMq:Username' and 'RabbitMq:Password' are not configured. " +
                "Use 'dotnet user-secrets' for local development or the RabbitMq__Username/RabbitMq__Password environment variables.");
        }

        return new ConnectionFactory
        {
            HostName = Host,
            Port = Port,
            UserName = Username,
            Password = Password,
            ClientProvidedName = clientName,
        };
    }
}
