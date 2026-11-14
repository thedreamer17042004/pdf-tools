# Nam-Dev PDF Core API (Aspose-inspired)

Sync **byte-in / byte-out** surface so other services can call PDF tools without the document library.

Base: `POST /api/v1/pdf/core/...` · Auth: JWT Bearer  
Catalog: `GET /api/v1/pdf/core` (anonymous)

## Contract

| Field | Role |
|-------|------|
| `file` | multipart PDF (or multiple for merge) |
| `options` | JSON string with operation args |
| `insert` / `appendix` / `image` | extra multipart files when needed |

Response: `application/pdf`, `application/zip`, or JSON.

## Feature map (vs Aspose.PDF)

| Aspose area | Nam-Dev core route | Status |
|-------------|------------------|--------|
| Document info | `/info`, `/info/set` | ✅ |
| Merge / concatenate | `/merge`, `/append` | ✅ |
| Split | `/split` | ✅ |
| Insert blank / pages | `/insert-blank`, `/insert-pages` | ✅ |
| Delete / extract / reorder | `/delete-pages`, `/extract-pages`, `/organize`, `/reverse` | ✅ |
| Rotate | `/rotate` | ✅ |
| Crop | `/crop` | ✅ |
| Add text | `/add-text` | ✅ |
| Header / footer | `/header-footer` | ✅ |
| Watermark / page numbers | `/watermark`, `/page-numbers` | ✅ |
| Image stamp | `/stamp-image` | ✅ |
| Visual redaction (cover) | `/redact-cover` | ✅ (overlay; not content-stream erase) |
| Encrypt / decrypt | `/protect`, `/unlock` | ✅ |
| Compress | `/compress` | ✅ |
| AcroForm list / fill | `/forms/fields`, `/forms/fill` | ✅ |
| **Merge fields** | `/forms/merge-fields` | ✅ fill + optional flatten |
| Export values | `/forms/values` | ✅ |
| Flatten / remove fields | `/forms/flatten`, `/forms/remove-fields` | ✅ |
| Bookmarks | `/bookmarks`, `/bookmarks/set`, `/bookmarks/clear` | ✅ |
| Extract text | `/extract-text` | ✅ |
| Images → PDF | `/images-to-pdf` | ✅ |
| PDF → images | job `/api/v1/pdf/to-images` | ✅ |
| Repair | `/repair` | ✅ |
| Compare | `/compare` | ✅ |
| PDF → Markdown | `/to-markdown` | ✅ |
| HTML → PDF | `/html-to-pdf` | ✅ (simple HTML) |
| Sign stamp | `/sign-stamp` | ✅ visual only |
| Add form fields | `/forms/add-fields` | ✅ visual placeholders |
| OCR / PAdES / Office / PDF/A | — | deferred — see ILOVEPDF-GAP.md |

## Merge fields example

```http
POST /api/v1/pdf/core/forms/merge-fields
Authorization: Bearer <token>
Content-Type: multipart/form-data

file: template.pdf
options: {"values":{"FullName":"Ada Lovelace","Date":"2026-11-14"},"flatten":true}
```

Library document variant: `POST /api/v1/documents/{id}/form-fields/merge`

## Job surface (library)

Existing async tools remain at `/api/v1/pdf/*` (documentId → job → download).
