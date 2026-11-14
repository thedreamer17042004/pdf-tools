# Phase 3 Report — Nam-Dev PDF Platform

## Scope delivered

- **Annotation persistence:** `document_annotations` table + list/replace API
- **XFDF subset:** import/export via `IXfdfService` / `XfdfService` (Highlight, Underline, StrikeOut, Squiggly, FreeText, Square, Circle, Ink, Text)
- **Editor UI:** draw tools overlay on PDF.js pages, Save, XFDF export/import
- **Compress / Organize:** PDFsharp jobs + `/tools` UI cards
- **Playwright E2E:** route smoke + annotation API + compress job smoke

## Architecture decisions

1. **Annotations stored as DB rows** (not embedded in PDF) for fast edit/save; XFDF is the interchange format.
2. **PDF user-space coords** (lower-left origin) in API/storage; CSS overlay converts at draw time.
3. **Compress** rebuilds via PDFsharp import + content-stream compression + optional metadata strip.
4. **Organize** accepts 0-based `pageOrder` (reorder and/or drop pages).
5. **Playwright** covers UI routes plus API request fixtures when API is up.

## API additions

| Method | Route | Auth |
|--------|-------|------|
| GET | `/api/v1/documents/{id}/annotations` | public* |
| PUT | `/api/v1/documents/{id}/annotations` | public* |
| GET | `/api/v1/documents/{id}/annotations/xfdf` | public* |
| POST | `/api/v1/documents/{id}/annotations/xfdf` | public* |
| POST | `/api/v1/pdf/compress` | JWT |
| POST | `/api/v1/pdf/organize` | JWT |

\*Same openness as Phase 1 document APIs for viewer compatibility; tenancy hardening is later.

## Migration

- `AddDocumentAnnotations` → table `document_annotations`

## Build & test results

| Check | Result |
|-------|--------|
| `dotnet test` (Core) | **5 passed** (incl. XFDF roundtrip) |
| `dotnet test` (Tools) | **5 passed** (incl. compress + organize) |
| `dotnet test` (Api) | **3 passed** (incl. annotations XFDF) |
| `npm run build` | **OK** |
| Playwright | **3 passed** (routes, annotation API, compress job) |
| Live smoke | annotations=1, xfdf_highlight=True, compress=Succeeded, organize=Succeeded |

## How to run Phase 3

```bash
docker compose up -d
cd backend
dotnet run --project src/Acman.Api --urls http://localhost:5080
dotnet run --project src/Acman.Worker
cd frontend/apps/web && npm run dev
# E2E (API should be on :5080)
npm run test:e2e:install
PLAYWRIGHT_SKIP_WEBSERVER=1 npm run test:e2e   # if Next already running
```

- Editor: http://localhost:3000/editor
- Tools: http://localhost:3000/tools (compress + organize cards)

## Known limitations

- Annotation overlay supports rectangle-based tools (ink is boxed, not freehand paths yet)
- Organize UI takes manual 0-based index list (no drag-and-drop page thumbnails yet)
- Document/annotation APIs still lack owner scoping
- Compress ratio depends on source PDF (already-small samples may not shrink much)

## Next phase preview

- Freehand ink paths + sticky notes popover
- Document ownership / tenancy
- Form fill / flatten
- Stronger Playwright UI draw → save flow
