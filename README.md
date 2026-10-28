# Knowledge Assistant

[![CI](https://github.com/AhmadAtat/knowledge-assistant/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/AhmadAtat/knowledge-assistant/actions/workflows/ci.yml)

A .NET 10 API that will store documents and answer questions about them using the Claude API (RAG). Built step by step with [Claude Code](https://claude.com/claude-code).

> 🚧 **Work in progress.** The project is under active development and is not feature-complete.

## Tech stack

- **.NET 10** minimal API
- **PostgreSQL 17** with **EF Core 10** (Npgsql) and migrations
- **xUnit** integration tests (`WebApplicationFactory` + **Testcontainers** for a real PostgreSQL)
- **Docker** multi-stage build
- **Docker Compose** for local orchestration

## Project structure

```
src/KnowledgeAssistant.Api/          Minimal API (host setup in Program.cs)
  Data/                              AppDbContext and EF Core migrations
  Documents/                         Document entity, request/response contracts, /documents endpoints
tests/KnowledgeAssistant.Api.Tests/  xUnit integration tests that boot the API in memory against PostgreSQL
Dockerfile                           Multi-stage build: SDK image → ASP.NET runtime image, non-root user
docker-compose.yml                   API + PostgreSQL 17 with healthcheck and persistent volume
CLAUDE.md                            Working rules and project context for Claude Code
```

## Getting started

### Prerequisites

- [Docker](https://www.docker.com/) with Docker Compose
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (only needed to run the tests or the API outside Docker)

### 1. Create the `.env` file

```bash
cp .env.example .env              # PowerShell: Copy-Item .env.example .env
```

Then set your own `POSTGRES_PASSWORD` in `.env`. The file is git-ignored.

### 2. Start the stack

```bash
docker compose up --build
```

### 3. Check the health endpoint

```bash
curl http://localhost:8080/health
# PowerShell: curl.exe http://localhost:8080/health
# (in PowerShell, curl is an alias for Invoke-WebRequest)
```

Expected response:

```json
{"status":"Healthy"}
```

The database schema is created automatically: the API applies pending EF Core migrations on startup.

### 4. Run the tests

Docker must be running: the tests start a throwaway PostgreSQL container via [Testcontainers](https://dotnet.testcontainers.org/).

```bash
dotnet test
```

## Running the API from Visual Studio / `dotnet run`

Start only the database with `docker compose up db`. Then store the connection string in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), which live outside the repository and are loaded only in the `Development` environment:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5432;Database=knowledge_assistant;Username=knowledge_assistant;Password=<your POSTGRES_PASSWORD>" --project src/KnowledgeAssistant.Api
```

Use `127.0.0.1` rather than `localhost`: Compose publishes PostgreSQL on IPv4 only. Then start the API from Visual Studio or with `dotnet run --project src/KnowledgeAssistant.Api` (http://localhost:5208).

## API

| Method | Route             | Description                                       | Responses          |
|--------|-------------------|---------------------------------------------------|--------------------|
| GET    | `/health`         | Health check                                      | 200                |
| POST   | `/documents`      | Create a document (`title` ≤ 200 chars, `content`) | 201, 400           |
| GET    | `/documents/{id}` | Get a document by id                              | 200, 404           |
| GET    | `/documents`      | List all documents, newest first                  | 200                |

```bash
curl -X POST http://localhost:8080/documents -H "Content-Type: application/json" -d '{"title":"Hello","content":"World"}'
# PowerShell: curl.exe -X POST http://localhost:8080/documents -H "Content-Type: application/json" -d '{\"title\":\"Hello\",\"content\":\"World\"}'
```

## Database migrations

The EF Core CLI is pinned as a local tool in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/KnowledgeAssistant.Api --output-dir Data/Migrations
```

## Roadmap

- [x] CI with GitHub Actions
- [x] Documents API with EF Core and PostgreSQL
- [ ] Messaging with RabbitMQ
- [ ] Deployment to Kubernetes
- [ ] Claude API integration
- [ ] Retrieval-augmented generation (RAG) over documents
