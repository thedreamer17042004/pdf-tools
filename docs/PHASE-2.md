# Phase 2 Report — Nam-Dev PDF Platform

## Scope delivered

- **PDF tools (real processing):** merge, split, rotate via `IPdfManipulationService` (PDFsharp 6.2.4 MIT adapter)
- **Async jobs:** `processing_jobs` table + RabbitMQ queue + `Acman.Worker` consumer (inline fallback if RabbitMQ unavailable)
- **Identity:** register / login / JWT bearer; tool APIs require auth
- **Editor shell:** toolbar + page nav + properties panel + PDF.js viewer host
- **Tools UI:** `/tools` pages for merge / split / rotate with job polling + download

## Architecture decisions

1. **Rendering vs manipulation split** — PDFium/Docnet remains render/inspect; PDFsharp handles merge/split/rotate (no iText).
2. **PDFsharp 6.2.4** — last MIT line before AGPL/commercial switch in 6.5+.
3. **Job execution** — enqueue to RabbitMQ when available; otherwise execute inline in API for local reliability.
4. **JWT as default auth scheme** — Identity cookies configured to return 401 instead of redirecting APIs.
5. **Shared object storage root** — `data/objects` at repo root for API + Worker.

## API additions

| Method | Route | Auth |
|--------|-------|------|
| POST | `/api/v1/auth/register` | public |
| POST | `/api/v1/auth/login` | public |
| GET | `/api/v1/auth/me` | JWT |
| POST | `/api/v1/pdf/merge` | JWT |
| POST | `/api/v1/pdf/split` | JWT |
| POST | `/api/v1/pdf/rotate` | JWT |
| GET | `/api/v1/jobs/{id}` | JWT |
| GET | `/api/v1/jobs/{id}/download` | JWT |

## Build & test results

| Check | Result |
|-------|--------|
| `dotnet test` | **10 passed** (Core 3, Tools 3, Pdfium 2, Api 2) |
| E2E merge | Succeeded → `%PDF` 65529 bytes |
| E2E rotate | Succeeded → `rotated.pdf` |
| E2E split | Succeeded → `split.zip` |
| Auth register/login | JWT issued |
| `npm run build` | (see session output) |

## How to run Phase 2

```bash
docker compose up -d
cd backend
dotnet run --project src/Acman.Api --urls http://localhost:5080
dotnet run --project src/Acman.Worker
cd frontend/apps/web && npm run dev
```

- Tools: http://localhost:3000/tools (sign in first)
- Editor: http://localhost:3000/editor
- Login: http://localhost:3000/login

## Known limitations

- Editor shell is chrome-only; annotation persistence / XFDF is Phase 3
- Tools require login; document upload remains open for Phase 1 viewer compatibility
- Split of multi-page docs returns ZIP of single-page PDFs
- RabbitMQ worker must share the same storage root as the API
- No refresh tokens / email confirmation flow yet

## Next phase preview

- Annotation persistence + XFDF
- Compress / organize pages tools
- Stronger tenancy on documents
- Playwright E2E for tools + editor
