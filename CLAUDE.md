# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Working rules

- Explain the plan before changing code.
- Work in small steps; run `dotnet build` and `dotnet test` after each change.
- **Never put secrets or API keys in code or `appsettings*.json`.** Use user secrets (`dotnet user-secrets`) or environment variables instead.
- Don't commit unless explicitly asked.

## Commands

Run from the repo root (targets `KnowledgeAssistant.slnx`, .NET 10 SDK):

```
dotnet build                                   # build everything
dotnet test                                    # run all tests
dotnet test --filter "FullyQualifiedName~HealthEndpointTests"          # one test class
dotnet test --filter "FullyQualifiedName~GetHealth_ReturnsOkWithHealthyStatus"  # one test
dotnet run --project src/KnowledgeAssistant.Api   # run the API (ports in Properties/launchSettings.json)
dotnet run --project src/KnowledgeAssistant.Worker   # run the Worker
dotnet tool restore                            # restore the pinned local dotnet-ef tool (dotnet-tools.json)
dotnet ef migrations add <Name> --project src/KnowledgeAssistant.Infrastructure --startup-project src/KnowledgeAssistant.Api --output-dir Data/Migrations
```

Tests need Docker running (Testcontainers starts `postgres:17` and `rabbitmq:4` containers).

No linter or formatter is configured beyond the compiler (`Nullable` and `ImplicitUsings` are enabled in both projects).

## Architecture

- `src/KnowledgeAssistant.Infrastructure` — class library shared by the API and the Worker.
  - Persistence: EF Core 10 + Npgsql. `Documents/Document.cs`, `Data/AppDbContext.cs`; migrations in `Data/Migrations` (generated, don't hand-edit). `AddAppDbContext()` registers the DbContext and reads `ConnectionStrings:Default` lazily so test overrides apply.
  - Messaging: `Messaging/` holds the `DocumentCreated` contract, `MessageSerializer` (JSON wire format), `RabbitMqOptions` (config section `RabbitMq`), and `RabbitMqTopology`. **All exchange/queue declarations live in `RabbitMqTopology.DeclareAsync`**; both apps call it. Re-declaring with different arguments fails with PRECONDITION_FAILED.
- `src/KnowledgeAssistant.Api` — ASP.NET Core **minimal API** (no controllers). Host setup lives in `Program.cs` (top-level statements). Features live in folders (e.g. `Documents/`) and register routes via an extension method (`app.MapDocumentEndpoints()`) using route groups and `TypedResults`.
  - Pending migrations are applied on startup in `Program.cs` (the Worker does not migrate).
  - `POST /documents` publishes `DocumentCreated` via `Messaging/RabbitMqDocumentEventPublisher` (singleton, publisher confirms). A publish failure is logged and the request still returns 201 (known dual-write gap; no outbox yet).
  - Request validation: .NET 10 built-in minimal API validation (`AddValidation()` + DataAnnotations on request records) → automatic 400 ProblemDetails.
- `src/KnowledgeAssistant.Worker` — .NET Worker Service (Generic Host). `DocumentCreatedConsumer` (`BackgroundService`) consumes `document-processing` with manual acks; `DocumentProcessor` (scoped, one DI scope per message) does the work and must stay idempotent (at-least-once delivery). Everything is registered by `AddDocumentProcessing()`, which the tests reuse.
- Configuration: `ConnectionStrings:Default` and `RabbitMq:Host/Port/Username/Password` — from env vars (`ConnectionStrings__Default`, `RabbitMq__*`) in Docker Compose, from user secrets (`127.0.0.1`) when running locally. Each app has its own user secrets.
- `tests/KnowledgeAssistant.Api.Tests` — xUnit **integration tests** that boot the real API in memory and call it via `HttpClient`. `ApiFactory` (a `WebApplicationFactory<Program>`) starts PostgreSQL and RabbitMQ containers with Testcontainers and overrides the settings. It is shared as a collection fixture: test classes use `[Collection(ApiCollection.Name)]` and primary-constructor injection of `ApiFactory`. Messaging tests bind their own exclusive, server-named queue to the exchange instead of reading the real queue.
- `tests/KnowledgeAssistant.Worker.Tests` — runs the real Worker host (`WorkerFixture`, collection `WorkerCollection.Name`) against its own containers; it applies migrations itself. Async outcomes are polled with `Eventually.TrueWithinAsync`.
- In both test projects the database and broker are shared across tests, so tests create their own data and must not assume an empty table, an empty queue or exact counts.
- `Program` is referenced from tests without a `public partial class Program;` declaration — .NET 10 generates it automatically for top-level-statement web apps. Don't add one unless the build requires it.
- Docker: `Dockerfile` (API) and `Dockerfile.worker` (Worker) each copy the csproj files they restore — add Infrastructure-like dependencies to both. CI builds both images via a matrix.
- New production projects go under `src/`, test projects under `tests/` (named `<Project>.Tests`), and must be added to the solution with `dotnet sln add`.
