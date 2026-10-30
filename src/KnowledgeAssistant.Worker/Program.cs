using KnowledgeAssistant.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDocumentProcessing();

var host = builder.Build();
host.Run();
