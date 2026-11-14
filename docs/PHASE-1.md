# Phase 1 Report — Nam-Dev PDF Platform

## Completed features

- Monorepo scaffold (`backend/`, `frontend/apps/web`, `docs/`, `samples/`, Docker Compose)
- PDF Core SDK abstractions + PDFium adapter (Docnet + ImageSharp)
- ASP.NET Core 9 API with Clean Architecture Documents module
- Real upload / open / page listing / PDFium render / download
- PostgreSQL metadata + EF Core migration
- Redis render cache (graceful degrade if unavailable)
- FileSystem object storage (S3 adapter ready; LocalStack provisioned)
- Next.js 16 Nam-Dev landing + PDF.js workspace (lazy page canvases)
- Automated tests (Core, Pdfium, API)
- Docker Compose: Postgres (5433), Redis, RabbitMQ, LocalStack

## Build & test results

| Check | Result |
|-------|--------|
| `dotnet build` | Succeeded |
| `dotnet test` | **7 passed** (Core 3, Pdfium 2, Api 2) |
| `npm run build` | Succeeded |
| E2E API | Upload → pages → PNG render → download (byte-identical) |
| `/health` | Healthy (Postgres + Redis) |

### Manual E2E sample (2026-11-14)

```text
POST /api/v1/documents  → pageCount=2, sizeBytes=829
GET  .../pages          → 612×792 pt pages
GET  .../render?page=1  → PNG 10005 bytes (PDFium)
GET  .../download       → %PDF, 829 bytes (matches original)
```

## Architecture decisions

1. **Vendor isolation** — Docnet types stay inside `Acman.Pdf.Engine.Pdfium`.
2. **Dual rendering** — PDF.js for interactive browser UX; PDFium for server `/render`.
3. **Metadata vs bytes** — Postgres for document rows; object storage for PDF binaries.
4. **Postgres host port 5433** — avoids collision with existing local Postgres on 5432.
5. **FileSystem storage default** — MinIO images were not pullable; S3 path remains available via LocalStack/`Storage:Provider=S3`.
6. **Open local-dev auth** — no JWT in Phase 1; Identity deferred.

## Files created (high level)

- `docker-compose.yml`, `.env.example`, `README.md`
- `backend/Acman.sln` + projects under `backend/src/*` and `backend/tests/*`
- `frontend/apps/web` (Next.js 16 + PDF.js workspace)
- `docs/ARCHITECTURE.md`, `docs/PHASE-1.md`
- `samples/hello.pdf`

## Known limitations

- Password-protected PDF UI not implemented (engine throws `PdfPasswordRequiredException`)
- No annotations, forms, signatures, OCR, AI, merge/split tools
- No iText adapter (commercial license required before enabling)
- RabbitMQ is up but idle (workers in Phase 2)
- ImageSharp 3.x still reports NuGet advisory warnings — track upgrade to a patched line
- Large-document virtualization is basic (IntersectionObserver), not production-tuned
- No malware scanning / tenant isolation / signed download URLs yet
- Sample `hello.pdf` is a minimal fixture for CI, not a stress corpus

## How to run

```bash
cp .env.example .env
docker compose up -d
cd backend && dotnet run --project src/Acman.Api --urls http://localhost:5080
cd frontend/apps/web && npm install && npm run dev
```

Open http://localhost:3000/workspace

## Next phase (Phase 2 preview)

- Online tools: merge / split / rotate / compress (async jobs via RabbitMQ)
- Identity basics (register/login/API keys)
- Editor shell (toolbar + command stack)
- Stronger PDF corpus + Playwright E2E
