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
```

No linter or formatter is configured beyond the compiler (`Nullable` and `ImplicitUsings` are enabled in both projects).

## Architecture

- `src/KnowledgeAssistant.Api` — ASP.NET Core **minimal API** (no controllers). All host setup and route mapping live in `Program.cs` using top-level statements.
- `tests/KnowledgeAssistant.Api.Tests` — xUnit **integration tests**. They use `WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing`) to boot the real API in memory and call it via `HttpClient`, so tests exercise the actual routing pipeline. Tests use primary-constructor injection of the factory through `IClassFixture<WebApplicationFactory<Program>>`.
- `Program` is referenced from tests without a `public partial class Program;` declaration — .NET 10 generates it automatically for top-level-statement web apps. Don't add one unless the build requires it.
- New production projects go under `src/`, test projects under `tests/` (named `<Project>.Tests`), and must be added to the solution with `dotnet sln add`.
