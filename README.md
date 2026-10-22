# Knowledge Assistant

[![CI](https://github.com/AhmadAtat/knowledge-assistant/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/AhmadAtat/knowledge-assistant/actions/workflows/ci.yml)

A .NET 10 API that will store documents and answer questions about them using the Claude API (RAG). Built step by step with [Claude Code](https://claude.com/claude-code).

> 🚧 **Work in progress.** The project is under active development and is not feature-complete.

## Tech stack

- **.NET 10** minimal API
- **xUnit** integration tests (`WebApplicationFactory`)
- **Docker** multi-stage build
- **Docker Compose** for local orchestration
- **PostgreSQL 17**

## Project structure

```
src/KnowledgeAssistant.Api/          Minimal API (host setup and routes in Program.cs)
tests/KnowledgeAssistant.Api.Tests/  xUnit integration tests that boot the API in memory
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

### 4. Run the tests

```bash
dotnet test
```

## Roadmap

- [ ] CI with GitHub Actions
- [ ] Messaging with RabbitMQ
- [ ] Deployment to Kubernetes
- [ ] Claude API integration
- [ ] Retrieval-augmented generation (RAG) over documents
