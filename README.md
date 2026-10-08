# SUPPORT — NMate

AI trợ lý hỗ trợ người dùng (RAG) cho hệ sinh thái Nexus. Tên hiển thị: **NMate**.
Design/plan: `C:\DEV\docs\NMATE-AI-AGENT.md`.

## Stack

.NET 10 · Clean Architecture (Domain / Application / Infrastructure / Api) · MediatR · FluentValidation ·
Microsoft Agent Framework (`Microsoft.Agents.AI`) · Gemini API (OpenAI-compatible) · PostgreSQL + pgvector.

## Chạy local

```bash
cp .env.example .env              # điền SUPPORT_PG_PASSWORD
docker compose up -d              # pgvector ở :5433
dotnet user-secrets --project src/SUPPORT.Api set "Llm:ApiKey" "<GEMINI_API_KEY>"
dotnet run --project src/SUPPORT.Api   # http://localhost:5160/health/live
```

## Cấu trúc

```
src/
  SUPPORT.Domain          entities, value objects, enums — không phụ thuộc gì
  SUPPORT.Application     use cases (MediatR), interfaces — chỉ phụ thuộc Domain
  SUPPORT.Infrastructure  EF Core + pgvector, Gemini client, ingestion
  SUPPORT.Api             /internal/v1/* — chỉ DASHBOARD gọi (X-Internal-Token)
tests/
  SUPPORT.UnitTests · SUPPORT.IntegrationTests
```
