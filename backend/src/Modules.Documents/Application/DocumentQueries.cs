using Acman.BuildingBlocks.Errors;
using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Models;
using Acman.Pdf.Abstractions.Storage;
using Acman.Pdf.Infrastructure.Options;
using MediatR;
using Microsoft.Extensions.Options;
using Modules.Documents.Contracts;
using Modules.Documents.Infrastructure;

namespace Modules.Documents.Application;

public sealed record ListDocumentPagesQuery(Guid Id) : IRequest<IReadOnlyList<DocumentPageDto>>;

public sealed class ListDocumentPagesHandler : IRequestHandler<ListDocumentPagesQuery, IReadOnlyList<DocumentPageDto>>
{
    private readonly DocumentsDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IPdfDocumentFactory _pdfFactory;

    public ListDocumentPagesHandler(
        DocumentsDbContext db,
        IObjectStorage storage,
        IPdfDocumentFactory pdfFactory)
    {
        _db = db;
        _storage = storage;
        _pdfFactory = pdfFactory;
    }

    public async Task<IReadOnlyList<DocumentPageDto>> Handle(
        ListDocumentPagesQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.Id], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.Id}' was not found.");

        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        await using var document = await _pdfFactory.OpenAsync(stream, cancellationToken: cancellationToken);
        return document.GetPages()
            .Select(p => new DocumentPageDto(p.Index, p.WidthPoints, p.HeightPoints, p.RotationDegrees))
            .ToList();
    }
}

public sealed record RenderDocumentPageQuery(
    Guid Id,
    int Page,
    int Dpi = 144) : IRequest<RenderedPage>;

public sealed class RenderDocumentPageHandler : IRequestHandler<RenderDocumentPageQuery, RenderedPage>
{
    private readonly DocumentsDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IPdfRenderer _renderer;
    private readonly IRenderCache _cache;
    private readonly DocumentsOptions _options;

    public RenderDocumentPageHandler(
        DocumentsDbContext db,
        IObjectStorage storage,
        IPdfRenderer renderer,
        IRenderCache cache,
        IOptions<DocumentsOptions> options)
    {
        _db = db;
        _storage = storage;
        _renderer = renderer;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<RenderedPage> Handle(RenderDocumentPageQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ValidationAppException("Page numbers are 1-based.");
        }

        var entity = await _db.Documents.FindAsync([request.Id], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.Id}' was not found.");

        var pageIndex = request.Page - 1;
        if (pageIndex >= entity.PageCount)
        {
            throw new ValidationAppException($"Page {request.Page} exceeds page count {entity.PageCount}.");
        }

        var dpi = Math.Clamp(request.Dpi, 36, 600);
        var cacheKey = $"render:{entity.Id:N}:p{pageIndex}:dpi{dpi}";
        var cached = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cached is { Length: > 0 })
        {
            return new RenderedPage(pageIndex, cached, "image/png", 0, 0, dpi);
        }

        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var rendered = await _renderer.RenderPageAsync(
            stream,
            pageIndex,
            new RenderOptions(Dpi: dpi),
            cancellationToken: cancellationToken);

        await _cache.SetAsync(cacheKey, rendered.ImageBytes, _options.RenderCacheTtl, cancellationToken);
        return rendered;
    }
}

public sealed record DownloadDocumentQuery(Guid Id) : IRequest<DownloadDocumentResult>;

public sealed record DownloadDocumentResult(string FileName, string ContentType, Stream Content, long SizeBytes);

public sealed class DownloadDocumentHandler : IRequestHandler<DownloadDocumentQuery, DownloadDocumentResult>
{
    private readonly DocumentsDbContext _db;
    private readonly IObjectStorage _storage;

    public DownloadDocumentHandler(DocumentsDbContext db, IObjectStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<DownloadDocumentResult> Handle(DownloadDocumentQuery request, CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.Id], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.Id}' was not found.");

        var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        return new DownloadDocumentResult(entity.FileName, entity.ContentType, stream, entity.SizeBytes);
    }
}
