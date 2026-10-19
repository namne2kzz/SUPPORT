# NMate — AI Support Assistant

NMate is the in-app assistant of the Nexus product family. It answers "how do I…" questions about **NFlow** (repo `DASHBOARD`) from the product's own documentation, with citations, and falls back to general-knowledge answers when a question is not covered by the docs.

The repository and code namespaces are named `SUPPORT`; **NMate** is the user-facing name.

## How it works

```text
Angular widget ──JWT──▶ DASHBOARD  /api/v1/nmate/*        auth, per-user rate limit, forwards user identity
                              │  X-Internal-Token
                              ▼
                        SUPPORT    /internal/v1/*         this repo — internal network only
                              │
              ┌───────────────┴────────────────┐
              ▼                                ▼
   PostgreSQL + pgvector              Gemini API (OpenAI-compatible)
   documents, chunks, conversations   chat + embeddings
```

### Answering a question

1. The question is stored, embedded, and matched against the documentation with **hybrid search**: HNSW cosine search and PostgreSQL full-text search, fused with Reciprocal Rank Fusion.
2. **Documentation mode**: if the best match clears `Chat:MinCosineSimilarity` (or an exact full-text match exists), the answer is generated only from the retrieved chunks and returned with citations.
3. **General mode**: otherwise the model answers from general knowledge, without citations, and is instructed not to invent product-specific facts. Set `Chat:AllowGeneralKnowledge=false` to decline instead, which costs no model call.
4. The answer streams to the browser as Server-Sent Events and is persisted, including partial answers when the stream is cut short.

### Indexing documentation

Markdown files with YAML front-matter (kept in the DASHBOARD repo under `Knowledge/`) are split by heading into ~400–500-token chunks, embedded in batches, and stored in `kb_chunk` as `vector(768)`. Re-indexing is incremental: a file is re-embedded only when its content hash or the embedding model changes.

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core |
| Architecture | Clean Architecture (Domain / Application / Infrastructure / Api), MediatR, FluentValidation |
| AI | Microsoft Agent Framework (`Microsoft.Agents.AI`), `Microsoft.Extensions.AI` |
| LLM provider | Gemini via the OpenAI-compatible endpoint — `gemini-3.1-flash-lite` (chat), `gemini-embedding-2` (embeddings) |
| Storage | PostgreSQL 17 + pgvector (HNSW), EF Core 10 (Npgsql) |
| Tests | xUnit, Shouldly, NSubstitute, `WebApplicationFactory` |

The provider is configuration: application code depends only on `IChatClient` and `IEmbeddingGenerator`.

## Getting started

**Prerequisites:** .NET 10 SDK, Docker, a Gemini API key from Google AI Studio.

```bash
cp .env.example .env                 # set SUPPORT_PG_PASSWORD
docker compose up -d                 # pgvector on :5433, DbGate on http://localhost:8048

dotnet user-secrets --project src/SUPPORT.Api set "ConnectionStrings:Support" "Host=localhost;Port=5433;Database=support;Username=support;Password=<SUPPORT_PG_PASSWORD>"
dotnet user-secrets --project src/SUPPORT.Api set "InternalClients:0:Token" "<random, 32+ chars — DASHBOARD uses the same value>"
dotnet user-secrets --project src/SUPPORT.Api set "Knowledge:Sources:dashboard" "<path to DASHBOARD/Knowledge>"
dotnet user-secrets --project src/SUPPORT.Api set "Llm:ApiKey" "<GEMINI_API_KEY>"

dotnet run --project src/SUPPORT.Api   # http://localhost:5160 — migrates the database and indexes knowledge on startup
```

Without an API key the service still starts: `/status` reports `available: false` and chat/re-index return `503 NMATE_UNAVAILABLE`.
Sample requests: [`src/SUPPORT.Api/SUPPORT.Api.http`](src/SUPPORT.Api/SUPPORT.Api.http).

To use NMate from NFlow, set `NMate:BaseUrl` and `NMate:InternalToken` in the DASHBOARD API configuration.

## Configuration

| Key | Default | Purpose |
|---|---|---|
| `ConnectionStrings:Support` | — | PostgreSQL connection string |
| `InternalClients[]` | — | `{ Product, Token }` pairs allowed to call `/internal/*`; the token determines the product |
| `Llm:ApiKey` | — | Gemini API key (user-secrets / environment only) |
| `Llm:ChatModel` / `Llm:EmbeddingModel` | `gemini-3.1-flash-lite` / `gemini-embedding-2` | Model ids |
| `Llm:EmbeddingDimensions` | `768` | Must match the `vector(768)` column; changing it requires a migration |
| `Knowledge:Sources:{product}` | — | Folder of markdown knowledge files per product |
| `Chat:MinCosineSimilarity` | `0.60` | Relevance threshold for documentation mode (calibrated for Gemini embeddings) |
| `Chat:AllowGeneralKnowledge` | `true` | Answer from general knowledge when no documentation matches |
| `Chat:TopK` / `Chat:HistoryWindow` | `5` / `10` | Chunks per answer / earlier messages sent as context |

## API

All endpoints live under `/internal/v1` and require `X-Internal-Token`. Conversation endpoints also require `X-User-Id` and `X-Org-Id`, which the calling product forwards from its authenticated session. The service is not meant to be exposed publicly.

| Method | Path | Description |
|---|---|---|
| `POST` | `/chat` | Ask a question. SSE stream: `meta` → `delta`… → `citations` → `done` (or `error`) |
| `GET` | `/conversations` | The caller's conversations, most recent first |
| `GET` | `/conversations/{id}/messages` | Messages of one of the caller's conversations |
| `DELETE` | `/conversations/{id}` | Soft-delete a conversation |
| `PUT` | `/messages/{id}/feedback` | Rate an answer: `{ "rating": 1 \| -1, "comment"?: string }` |
| `GET` | `/suggestions?route=` | Starter questions for a screen (no model call) |
| `GET` | `/status` | Availability and knowledge-base size (no model call) |
| `POST` | `/knowledge/reindex` | Re-index the caller's knowledge folder |
| `DELETE` | `/users/{userId}/data` | Permanently delete a user's conversations |

Health probes `/health/live` and `/health/ready` need no token and never call the LLM.

## Project structure

```text
src/
  SUPPORT.Domain           Entities, value objects, enums — no dependencies
  SUPPORT.Application      Use cases (MediatR), interfaces, settings — depends on Domain only
  SUPPORT.Infrastructure   EF Core + pgvector, Gemini clients, agent, chunking, ingestion, hybrid search
  SUPPORT.Api              Internal REST/SSE endpoints, internal-client authentication
tests/
  SUPPORT.UnitTests        Domain, handlers, chunker, front-matter parser, route matching
  SUPPORT.IntegrationTests Real PostgreSQL: schema, ingestion, search, full API with fake LLM
```

## Development

### Migrations

```bash
dotnet ef migrations add <Name> --project src/SUPPORT.Infrastructure --startup-project src/SUPPORT.Infrastructure --output-dir Persistence/Migrations
```

The `Init` migration contains hand-written SQL for `support_unaccent`, an `IMMUTABLE` wrapper around `unaccent()` required by the generated full-text column `kb_chunk.search_tsv`.

### Tests

```bash
dotnet test tests/SUPPORT.UnitTests

# Integration tests create and drop a temporary database on the docker-compose server
export SUPPORT_TEST_DB="Host=localhost;Port=5433;Username=support;Password=<SUPPORT_PG_PASSWORD>;Database=postgres"
dotnet test tests/SUPPORT.IntegrationTests

# Validate a knowledge folder with the production parser and chunker
SUPPORT_KNOWLEDGE_DIR="<path to DASHBOARD/Knowledge>" dotnet test tests/SUPPORT.UnitTests --filter KnowledgeFilesLint
```

Integration tests run against the docker-compose PostgreSQL rather than Testcontainers.

## Deployment

| Piece | Purpose |
|-------|---------|
| `src/SUPPORT.Api/Dockerfile` | Multi-stage image (build from repo root): `docker build -f src/SUPPORT.Api/Dockerfile -t support-api .` |
| `.github/workflows/ci.yml` | On push/PR to `main`: build, unit tests, integration tests against a `pgvector/pgvector:pg17` service container, then a no-push image build on `main` |
| `.github/workflows/cd.yml` | Manual (`workflow_dispatch`): push `ghcr.io/<owner>/support-api:<sha7>` + `latest`, Trivy scan, optional SSH deploy with health gate and automatic rollback |
| `docker-compose.prod.yml` + `.env.prod.example` | Server stack: pgvector + API, joined to the shared `hub-net` network, API bound to `127.0.0.1:8095` only |

**Server setup** — see the header of `docker-compose.prod.yml`. In short: `/opt/support` holds the compose file, `.env` (from `.env.prod.example`, `chmod 600`) and `knowledge/dashboard/`, which is mounted read-only at `/knowledge/dashboard`.

**CD secrets** (per GitHub environment): `SSH_HOST`, `SSH_USER`, `SSH_KEY`, `DEPLOY_PATH` (e.g. `/opt/support`), `HEALTH_URL` (`http://127.0.0.1:8095/health/ready`).

**Knowledge updates** do not need a SUPPORT deploy: DASHBOARD's `knowledge-sync` workflow copies `Knowledge/` into `/opt/support/knowledge/dashboard` and calls `POST /internal/v1/knowledge/reindex`. Only changed documents are re-embedded.

Migrations run at startup and are not rolled back with the image, so every migration must stay compatible with the previous release (expand/contract).

## Free-tier considerations

The Gemini free tier is limited per minute, so the service is built to minimise requests:

- Unchanged documents are never re-embedded (content hash + model id per chunk).
- Embeddings are batched across documents, and progress is kept if a quota error interrupts a run.
- Retrieval runs before the model, so each answer costs one chat request; question embeddings are cached.
- Chat requests are not retried on `429`; the widget shows a "try again shortly" message instead.
- DASHBOARD rate-limits questions per user, and each user can have only one answer streaming at a time.

## Related documentation

- Design and implementation notes: `docs/NMATE-AI-AGENT.md` (shared docs folder)
- Knowledge authoring: front-matter fields `key`, `title`, `module`, `type`, `route`, `language`, `suggestions` — see any file under `DASHBOARD/Knowledge/`
