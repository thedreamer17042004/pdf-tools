# Nam-Dev Architecture

## Overview

Nam-Dev is a modular PDF platform (Syncfusion-inspired breadth, original code).

```text
Browser (Next.js + PDF.js + AppShell)
        │
        ▼
Acman.Api  ──► Modules.Documents / Identity / Processing
                    │
        ┌───────────┼──────────────┐
        ▼           ▼              ▼
 PostgreSQL    IObjectStorage   PDF engines
 (metadata)   (FileSystem/S3)   Pdfium + PDFsharp + Worker
```

## Backend layers

| Project | Responsibility |
|---------|----------------|
| `Acman.Pdf.Abstractions` | Engine contracts (render, manipulate, text, forms, XFDF) |
| `Acman.Pdf.Core` | Validation, XFDF service, orchestration |
| `Acman.Pdf.Engine.Pdfium` | Docnet/PDFium render + text extract |
| `Acman.Pdf.Engine.PdfSharp` | Merge/split/rotate/compress/organize/watermark/protect/images/forms |
| `Acman.Pdf.Infrastructure` | Object storage, Redis render cache |
| `Modules.Documents` | Documents, annotations, ownership |
| `Modules.Identity` | Register / login / JWT |
| `Modules.Processing` | Async jobs + RabbitMQ worker |
| `Acman.Api` / `Acman.Worker` | HTTP host + job consumer |

## Frontend

- `/` landing · `/library` · `/workspace` · `/editor` · `/tools` · `/login`
- Shared `AppShell`, Plus Jakarta Sans, slate/teal design tokens

## Storage

- Default **FileSystem** under `data/objects`
- Optional LocalStack S3 (`Storage:Provider=S3`)
- PostgreSQL host port **5433**
