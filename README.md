# Nam-Dev PDF Platform

Enterprise PDF processing & editing ecosystem.

## Current milestones

- **Phase 1:** Upload → Open → Render → Navigate → Download
- **Phase 2:** Merge / Split / Rotate jobs + RabbitMQ worker + JWT Identity + Editor shell

| Layer | Technology |
|-------|------------|
| Web | Next.js 16, PDF.js, tools + editor shell |
| Server render | PDFium via Docnet |
| Manipulation | PDFsharp 6.2.4 (`Acman.Pdf.Engine.PdfSharp`) |
| API | ASP.NET Core 9, MediatR, FluentValidation, JWT |
| Worker | `Acman.Worker` (RabbitMQ consumer) |
| Metadata | PostgreSQL (host port **5433**) |
| Binary storage | FileSystem (default) / S3-compatible (LocalStack) |
| Cache | Redis |
| Queue | RabbitMQ |

## Prerequisites

- .NET SDK 9
- Node.js 20+
- Docker Desktop

## Quick start

```bash
# 1. Infrastructure
cp .env.example .env
docker compose up -d

# 2. Backend API + Worker
cd backend
dotnet restore
dotnet run --project src/Acman.Api --urls http://localhost:5080
dotnet run --project src/Acman.Worker

# 3. Frontend (http://localhost:3000)
cd frontend/apps/web
npm install
npm run dev
```

- Viewer: http://localhost:3000/workspace  
- Tools: http://localhost:3000/tools  
- Editor: http://localhost:3000/editor  
- Auth: http://localhost:3000/login

### Useful URLs

| Service | URL |
|---------|-----|
| Web | http://localhost:3000 |
| API | http://localhost:5080 |
| Health | http://localhost:5080/health |
| LocalStack S3 | http://localhost:4566 |
| RabbitMQ UI | http://localhost:15672 (acman / acman_rabbit_password) |

## Tests

```bash
cd backend
dotnet test

cd frontend/apps/web
npm run build
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Phase 1 report](docs/PHASE-1.md)
- [Phase 2 report](docs/PHASE-2.md)

## License notice

Third-party engines (PDFium/Docnet, PDF.js, ImageSharp, etc.) retain their own licenses.  
iText is **not** bundled in Phase 1; enable only with a valid commercial license.
