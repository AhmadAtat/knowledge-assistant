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
dotnet tool restore                            # restore the pinned local dotnet-ef tool (dotnet-tools.json)
dotnet ef migrations add <Name> --project src/KnowledgeAssistant.Api --output-dir Data/Migrations
```

Tests need Docker running (Testcontainers starts a `postgres:17` container).

No linter or formatter is configured beyond the compiler (`Nullable` and `ImplicitUsings` are enabled in both projects).

## Architecture

- `src/KnowledgeAssistant.Api` — ASP.NET Core **minimal API** (no controllers). Host setup lives in `Program.cs` (top-level statements). Features live in folders (e.g. `Documents/`) and register routes via an extension method (`app.MapDocumentEndpoints()`) using route groups and `TypedResults`.
  - Persistence: EF Core 10 + Npgsql. `Data/AppDbContext.cs`; migrations in `Data/Migrations` (generated, don't hand-edit). Pending migrations are applied on startup in `Program.cs`.
  - Connection string key: `ConnectionStrings:Default` — from the `ConnectionStrings__Default` env var in Docker Compose, from user secrets (`Host=127.0.0.1`) when running locally. It is read lazily inside `AddDbContext` so test overrides apply.
  - Request validation: .NET 10 built-in minimal API validation (`AddValidation()` + DataAnnotations on request records) → automatic 400 ProblemDetails.
- `tests/KnowledgeAssistant.Api.Tests` — xUnit **integration tests** that boot the real API in memory and call it via `HttpClient`. `PostgresApiFactory` (a `WebApplicationFactory<Program>`) starts a PostgreSQL container with Testcontainers and overrides the connection string. It is shared as a collection fixture: test classes use `[Collection(ApiCollection.Name)]` and primary-constructor injection of `PostgresApiFactory`. The database is shared across tests, so tests create their own data and must not assume an empty table or exact counts.
- `Program` is referenced from tests without a `public partial class Program;` declaration — .NET 10 generates it automatically for top-level-statement web apps. Don't add one unless the build requires it.
- New production projects go under `src/`, test projects under `tests/` (named `<Project>.Tests`), and must be added to the solution with `dotnet sln add`.
