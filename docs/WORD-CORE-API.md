# Nam-Dev Word Core (Aspose.Words-inspired)

Built on **DocumentFormat.OpenXml** + **PdfSharp** (self-built). No Aspose, no LibreOffice.

Base: `POST /api/v1/word/core/...` · Auth: JWT  
Catalog: `GET /api/v1/word/core`  
UI: http://localhost:3000/word

## Capabilities

| Feature | Route | Notes |
|---------|-------|-------|
| Info | `/info`, `/info/set` | title/author/subject |
| Create | `/create` | plain text → DOCX |
| Extract text | `/extract-text` | |
| Replace text | `/replace-text` | |
| Append | `/append` | concatenate DOCX |
| List merge fields | `/merge-fields/list` | MERGEFIELD, `«Name»`, `{{Name}}` |
| **Merge fields** | `/merge-fields` | mail-merge fill |
| **Word → PDF** | `/to-pdf` | Open XML → PdfSharp layout engine |

## Merge fields example

```http
POST /api/v1/word/core/merge-fields
Authorization: Bearer <token>
Content-Type: multipart/form-data

file: template.docx
options: {"values":{"FullName":"Ada Lovelace","Date":"2026-11-14"}}
```

## Word → PDF

Self-built pipeline:

`DOCX (Open XML)` → parse paragraphs/runs/tables → **PdfSharp** draw → `PDF`

Supports: body text, bold/italic, heading styles, simple tables, multi-page flow.  
Not a pixel-perfect Word clone (complex floats/headers/footers/images later).

## vs Aspose.Words

Supported (core): create, metadata, text replace, append, mail-merge placeholders, extract text, DOCX→PDF (own engine).

Not yet: DOC/RTF, nested mail-merge regions, charts, TOC rebuild, images in PDF export, track changes.
