# Knowledge Assistant

[![CI](https://github.com/AhmadAtat/knowledge-assistant/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/AhmadAtat/knowledge-assistant/actions/workflows/ci.yml)

A .NET 10 API that will store documents and answer questions about them using the Claude API (RAG). Built step by step with [Claude Code](https://claude.com/claude-code).

> 🚧 **Work in progress.** The project is under active development and is not feature-complete.

## Tech stack

- **.NET 10** minimal API and a **.NET Worker Service**
- **PostgreSQL 17** with **EF Core 10** (Npgsql) and migrations
- **RabbitMQ 4** with the official **RabbitMQ.Client** library
- **xUnit** integration tests (`WebApplicationFactory` and a real Worker host + **Testcontainers** for PostgreSQL and RabbitMQ)
- **Docker** multi-stage builds
- **Docker Compose** for local orchestration

## How it works

```
POST /documents ──▶ API ──save──▶ PostgreSQL
                     │                ▲
                     │ DocumentCreated│ load document, log word count, set ProcessedAt
                     ▼                │
                 RabbitMQ ──────▶ Worker
```

1. The API saves the document and publishes a `DocumentCreated` message (just the document id) to the `documents` exchange. The routing key is `document.created`.
2. RabbitMQ routes the message to the durable `document-processing` queue.
3. The Worker consumes the queue. For each message it loads the document, logs its word count and sets `ProcessedAt`. It acks the message only after that succeeded.
4. Failures are handled like this:
   - A malformed message goes straight to `document-processing.dead-letter`.
   - A processing error is retried once, then dead-lettered.
   - If publishing fails after the save, the API logs it and still returns 201. The document stays unprocessed (`processedAt: null`). A transactional outbox would close this gap; that comes later.

## Project structure

```
src/KnowledgeAssistant.Api/              Minimal API (host setup in Program.cs)
  Documents/                             Request/response contracts, /documents endpoints
  Messaging/                             Publishes DocumentCreated to RabbitMQ
src/KnowledgeAssistant.Infrastructure/   Shared by API and Worker
  Data/                                  AppDbContext and EF Core migrations
  Documents/                             Document entity
  Messaging/                             Message contract, RabbitMQ options and topology (exchanges, queues)
src/KnowledgeAssistant.Worker/           Worker Service that consumes document-processing
tests/KnowledgeAssistant.Api.Tests/      Integration tests that boot the API in memory against PostgreSQL + RabbitMQ
tests/KnowledgeAssistant.Worker.Tests/   Integration tests that run the Worker host against PostgreSQL + RabbitMQ
Dockerfile                               API image: SDK image → ASP.NET runtime image, non-root user
Dockerfile.worker                        Worker image: SDK image → .NET runtime image, non-root user
docker-compose.yml                       API, Worker, PostgreSQL 17 and RabbitMQ with healthchecks and persistent volumes
CLAUDE.md                                Working rules and project context for Claude Code
```

## Getting started

### Prerequisites

- [Docker](https://www.docker.com/) with Docker Compose
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (only needed to run the tests or the apps outside Docker)

### 1. Create the `.env` file

```bash
cp .env.example .env              # PowerShell: Copy-Item .env.example .env
```

Then set your own `POSTGRES_PASSWORD` and `RABBITMQ_PASSWORD` in `.env`. The file is git-ignored.

Set the RabbitMQ credentials **before the first start**. RabbitMQ creates its user only when its data volume is empty. To change the credentials later, reset the volume with `docker compose down`, then `docker volume rm knowledge-assistant_rabbitmqdata`.

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

### 4. Create a document and watch it get processed

```bash
curl -X POST http://localhost:8080/documents -H "Content-Type: application/json" -d '{"title":"Hello","content":"The quick brown fox"}'
```

PowerShell:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:8080/documents -ContentType "application/json" -Body '{"title":"Hello","content":"The quick brown fox"}'
```

The response has `"processedAt": null`. Shortly afterwards:

- The Worker logs the word count (`docker compose logs worker`).
- `GET /documents/{id}` shows a `processedAt` timestamp.

### 5. RabbitMQ management UI

Open http://127.0.0.1:15672 and log in with `RABBITMQ_USER` / `RABBITMQ_PASSWORD` from `.env`. Under **Queues and Streams** you can see `document-processing` and its dead-letter queue.

Stop the Worker (`docker compose stop worker`) and create a document: the message waits in the queue. It is processed as soon as the Worker starts again.

### 6. Run the tests

Docker must be running. The tests start throwaway PostgreSQL and RabbitMQ containers via [Testcontainers](https://dotnet.testcontainers.org/).

```bash
dotnet test
```

## Running the API and Worker from Visual Studio / `dotnet run`

Start only the infrastructure with `docker compose up db rabbitmq`. Then store the settings in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets). User secrets live outside the repository and are loaded only in the `Development` environment. Each project has its own, so run these commands once per project: `--project src/KnowledgeAssistant.Api` and again with `--project src/KnowledgeAssistant.Worker`.

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5432;Database=knowledge_assistant;Username=knowledge_assistant;Password=<your POSTGRES_PASSWORD>" --project src/KnowledgeAssistant.Api
dotnet user-secrets set "RabbitMq:Username" "<your RABBITMQ_USER>" --project src/KnowledgeAssistant.Api
dotnet user-secrets set "RabbitMq:Password" "<your RABBITMQ_PASSWORD>" --project src/KnowledgeAssistant.Api
```

Use `127.0.0.1` rather than `localhost`, because Compose publishes PostgreSQL and RabbitMQ on IPv4 only. `RabbitMq:Host` already defaults to `127.0.0.1`.

Then start the apps from Visual Studio or the command line:

- API: `dotnet run --project src/KnowledgeAssistant.Api`, then open http://localhost:5208.
- Worker: `dotnet run --project src/KnowledgeAssistant.Worker`.

## API

| Method | Route             | Description                                       | Responses          |
|--------|-------------------|---------------------------------------------------|--------------------|
| GET    | `/health`         | Health check                                      | 200                |
| POST   | `/documents`      | Create a document (`title` ≤ 200 chars, `content`) and queue it for processing | 201, 400 |
| GET    | `/documents/{id}` | Get a document by id                              | 200, 404           |
| GET    | `/documents`      | List all documents, newest first                  | 200                |

Documents are returned as `{ id, title, content, createdAt, processedAt }`. `processedAt` stays `null` until the Worker has processed the document.

## Database migrations

The EF Core CLI is pinned as a local tool in `dotnet-tools.json`. The DbContext and migrations live in Infrastructure; the API is the startup project:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/KnowledgeAssistant.Infrastructure --startup-project src/KnowledgeAssistant.Api --output-dir Data/Migrations
```

## Roadmap

- [x] CI with GitHub Actions
- [x] Documents API with EF Core and PostgreSQL
- [x] Messaging with RabbitMQ
- [ ] Deployment to Kubernetes
- [ ] Claude API integration
- [ ] Retrieval-augmented generation (RAG) over documents
