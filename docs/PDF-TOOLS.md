# Nam-Dev PDF Core Tools

Focus: many real PDF processing tools in the core (not OCR/signing/billing).

## Tool catalog

| Tool | API | Notes |
|------|-----|-------|
| Merge | `POST /api/v1/pdf/merge` | 2+ documents |
| Split | `POST /api/v1/pdf/split` | pages → ZIP |
| Rotate | `POST /api/v1/pdf/rotate` | 90/180/270 |
| Compress | `POST /api/v1/pdf/compress` | rebuild + strip metadata |
| Organize | `POST /api/v1/pdf/organize` | reorder / drop / duplicate via 0-based order |
| Extract pages | `POST /api/v1/pdf/extract-pages` | keep listed indexes |
| Delete pages | `POST /api/v1/pdf/delete-pages` | remove listed indexes |
| Reverse | `POST /api/v1/pdf/reverse` | reverse page order |
| Page numbers | `POST /api/v1/pdf/page-numbers` | stamp `{n} / {total}` |
| Watermark | `POST /api/v1/pdf/watermark` | diagonal text |
| Protect | `POST /api/v1/pdf/protect` | user password |
| Unlock | `POST /api/v1/pdf/unlock` | remove encryption with password |
| Images → PDF | `POST /api/v1/pdf/images-to-pdf` | multipart images |
| PDF → Images | `POST /api/v1/pdf/to-images` | PNG ZIP via PDFium |
| Text extract | `GET /api/v1/documents/{id}/text` | Docnet GetText |
| Form fields | `GET/POST .../form-fields` | list / fill / merge / flatten |

**Sync core (for other services):** `GET/POST /api/v1/pdf/core/*` — see [PDF-CORE-API.md](./PDF-CORE-API.md).

All job tools require JWT. Poll `GET /api/v1/jobs/{id}` then download.

UI: http://localhost:3000/tools
