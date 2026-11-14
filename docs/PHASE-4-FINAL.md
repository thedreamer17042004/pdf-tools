# Phase 4 + Final Report — Nam-Dev PDF Platform

## Scope delivered

### Phase 4
- **Document tenancy:** `OwnerUserId` on documents; upload stamps owner from JWT; `GET /mine`, `DELETE` require auth; owned docs gated by `CanAccess`
- **Library UI:** `/library` list / upload / delete / open / extract text
- **Annotations polish:** freehand ink paths + sticky/text note prompt
- **AcroForm:** list / fill / flatten (best-effort via PDFsharp)

### Final (Phase 5)
- **Text extract:** Docnet/PDFium `GetText` → `GET /api/v1/documents/{id}/text`
- **Watermark** job + UI
- **Password protect** job + UI
- **Images → PDF** endpoint + UI
- **UI redesign:** Plus Jakarta Sans, slate/teal tokens, shared `AppShell`, landing without phase labels

## API additions

| Method | Route | Auth |
|--------|-------|------|
| GET | `/api/v1/documents/mine` | JWT |
| DELETE | `/api/v1/documents/{id}` | JWT (owner) |
| GET | `/api/v1/documents/{id}/text` | public* |
| GET | `/api/v1/documents/{id}/form-fields` | public* |
| POST | `/api/v1/documents/{id}/form-fields/fill` | JWT |
| POST | `/api/v1/documents/{id}/form-fields/flatten` | JWT |
| POST | `/api/v1/pdf/watermark` | JWT |
| POST | `/api/v1/pdf/protect` | JWT |
| POST | `/api/v1/pdf/images-to-pdf` | JWT |

\*Same openness as earlier document routes for share-by-id; owned documents enforce access on get.

## Migrations

- `AddDocumentOwner` → `documents.OwnerUserId`

## Product surface (final)

| Route | Purpose |
|-------|---------|
| `/` | Landing |
| `/login` | Auth → Library |
| `/library` | Document library |
| `/workspace` | Viewer |
| `/editor` | Annotations |
| `/tools` | Online PDF tools |

## Build & test results

| Check | Result |
|-------|--------|
| Core tests | 5 passed |
| Tools tests | 7 passed (incl. watermark/protect) |
| Pdfium tests | 3 passed (incl. text extract) |
| Api tests | 3 passed |
| `npm run build` | OK (`/library` included) |
| Playwright | **3 passed** (routes, annotations, compress) |
| Live smoke | mine + text + watermark + protect |

## Honest out-of-scope (not this MVP)

Full Syncfusion-breadth engines deferred: OCR, PAdES/HSM signing, true content redaction, Office conversion, XFA, comparison, billing. Architecture remains extensible via abstractions.

## How to run

```bash
docker compose up -d
cd backend
dotnet run --project src/Acman.Api --urls http://localhost:5080
dotnet run --project src/Acman.Worker
cd frontend/apps/web && npm run dev
```

## Known limitations

- Form flatten is best-effort (read-only + strip fields), not full appearance bake
- Password protect uses PDFsharp security settings (open with user password)
- Ink freehand stores paths in `geometryJson`; server XFDF ink roundtrip supported
- Anonymous uploads remain possible (null owner) for Phase 1 viewer compatibility
