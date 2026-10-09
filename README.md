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
dotnet user-secrets --project src/SUPPORT.Api set "ConnectionStrings:Support" "Host=localhost;Port=5433;Database=support;Username=support;Password=<SUPPORT_PG_PASSWORD>"
dotnet user-secrets --project src/SUPPORT.Api set "InternalClients:0:Token" "<random ≥ 32 ký tự, DASHBOARD dùng cùng giá trị>"
dotnet user-secrets --project src/SUPPORT.Api set "Knowledge:Sources:dashboard" "C:\DEV\DASHBOARD\Knowledge"
dotnet user-secrets --project src/SUPPORT.Api set "Llm:ApiKey" "<GEMINI_API_KEY>"
dotnet run --project src/SUPPORT.Api   # tự migrate DB + index Knowledge lúc khởi động; http://localhost:5160/health/ready
```

Chưa có `Llm:ApiKey` thì API vẫn chạy: `/status` trả `available: false`, chat/reindex trả `503 NMATE_UNAVAILABLE`.
Request mẫu: [src/SUPPORT.Api/SUPPORT.Api.http](src/SUPPORT.Api/SUPPORT.Api.http).

## API (`/internal/v1/*`, header `X-Internal-Token`)

| Method | Path | Ghi chú |
|---|---|---|
| `POST` | `/chat` | SSE: `meta` → `delta`… → `citations` → `done` (hoặc `error`). Cần `X-User-Id`, `X-Org-Id` |
| `GET` | `/conversations` · `/conversations/{id}/messages` | Chỉ của chính user |
| `DELETE` | `/conversations/{id}` | Soft delete |
| `PUT` | `/messages/{id}/feedback` | `{ rating: 1 \| -1, comment? }` |
| `GET` | `/suggestions?route=` · `/status` | Không tốn quota Gemini |
| `POST` | `/knowledge/reindex` | Chỉ embed lại file đổi (so hash) |
| `DELETE` | `/users/{userId}/data` | Xoá hẳn hội thoại của user |

Product do **token** quyết định (`InternalClients`), không lấy từ header.

## Luồng trả lời

1. Lưu câu hỏi → tìm tài liệu (vector HNSW + full-text, gộp bằng RRF).
2. Không chunk nào đủ gần (`Chat:MinCosineSimilarity`) và không khớp full-text chính xác → trả "chưa có tài liệu", **không gọi model**.
3. Ngược lại → `ChatClientAgent` (Agent Framework) + `KnowledgeContextProvider` đưa tài liệu vào instructions → stream.
4. Lưu câu trả lời (kể cả khi bị ngắt giữa chừng, đánh dấu `is_interrupted`).

## Migration

```bash
dotnet ef migrations add <Name> --project src/SUPPORT.Infrastructure --startup-project src/SUPPORT.Infrastructure --output-dir Persistence/Migrations
```

`Init` có 1 đoạn SQL viết tay: hàm `support_unaccent` (IMMUTABLE) mà cột generated `kb_chunk.search_tsv` cần.

## Test

```bash
dotnet test tests/SUPPORT.UnitTests

# Integration: chạy trên server pgvector của docker-compose, mỗi lần tạo 1 DB tạm rồi xoá
export SUPPORT_TEST_DB="Host=localhost;Port=5433;Username=support;Password=<SUPPORT_PG_PASSWORD>;Database=postgres"
dotnet test tests/SUPPORT.IntegrationTests
```

Không dùng Testcontainers: Application Control policy trên máy dev chặn assembly của nó.

```bash
# Lint tài liệu thật bằng parser/chunker production (DASHBOARD CI có thể dùng lại)
SUPPORT_KNOWLEDGE_DIR="C:/DEV/DASHBOARD/Knowledge" dotnet test tests/SUPPORT.UnitTests --filter KnowledgeFilesLint
```

## Cấu trúc

```text
src/
  SUPPORT.Domain          entities, value objects, enums — không phụ thuộc gì
  SUPPORT.Application     use cases (MediatR), interfaces — chỉ phụ thuộc Domain
  SUPPORT.Infrastructure  EF Core + pgvector, Gemini client, ingestion
  SUPPORT.Api             /internal/v1/* — chỉ DASHBOARD gọi (X-Internal-Token)
tests/
  SUPPORT.UnitTests · SUPPORT.IntegrationTests
```
