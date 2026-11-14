using System.Text.Json;
using Acman.BuildingBlocks.Errors;
using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Documents.Domain;
using Modules.Documents.Infrastructure;

namespace Modules.Documents.Application;

public sealed record AnnotationDto(
    Guid Id,
    Guid DocumentId,
    string Type,
    int PageIndex,
    double X,
    double Y,
    double Width,
    double Height,
    string? Color,
    double Opacity,
    string? Contents,
    string? Author,
    string? GeometryJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AnnotationUpsertDto(
    Guid? Id,
    string Type,
    int PageIndex,
    double X,
    double Y,
    double Width,
    double Height,
    string? Color,
    double Opacity,
    string? Contents,
    string? Author,
    string? GeometryJson);

public sealed record ListAnnotationsQuery(Guid DocumentId) : IRequest<IReadOnlyList<AnnotationDto>>;
public sealed record ReplaceAnnotationsCommand(Guid DocumentId, IReadOnlyList<AnnotationUpsertDto> Annotations) : IRequest<IReadOnlyList<AnnotationDto>>;
public sealed record ExportXfdfQuery(Guid DocumentId) : IRequest<string>;
public sealed record ImportXfdfCommand(Guid DocumentId, string XfdfXml, bool ReplaceExisting) : IRequest<IReadOnlyList<AnnotationDto>>;

public sealed class ReplaceAnnotationsValidator : AbstractValidator<ReplaceAnnotationsCommand>
{
    public ReplaceAnnotationsValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleForEach(x => x.Annotations).ChildRules(a =>
        {
            a.RuleFor(x => x.Type).NotEmpty();
            a.RuleFor(x => x.PageIndex).GreaterThanOrEqualTo(0);
            a.RuleFor(x => x.Width).GreaterThanOrEqualTo(0);
            a.RuleFor(x => x.Height).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class AnnotationHandlers :
    IRequestHandler<ListAnnotationsQuery, IReadOnlyList<AnnotationDto>>,
    IRequestHandler<ReplaceAnnotationsCommand, IReadOnlyList<AnnotationDto>>,
    IRequestHandler<ExportXfdfQuery, string>,
    IRequestHandler<ImportXfdfCommand, IReadOnlyList<AnnotationDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DocumentsDbContext _db;
    private readonly IXfdfService _xfdf;

    public AnnotationHandlers(DocumentsDbContext db, IXfdfService xfdf)
    {
        _db = db;
        _xfdf = xfdf;
    }

    public async Task<IReadOnlyList<AnnotationDto>> Handle(ListAnnotationsQuery request, CancellationToken cancellationToken)
    {
        await EnsureDocumentAsync(request.DocumentId, cancellationToken);
        var items = await _db.Annotations
            .AsNoTracking()
            .Where(a => a.DocumentId == request.DocumentId)
            .OrderBy(a => a.PageIndex)
            .ThenBy(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AnnotationDto>> Handle(ReplaceAnnotationsCommand request, CancellationToken cancellationToken)
    {
        await EnsureDocumentAsync(request.DocumentId, cancellationToken);

        var existing = await _db.Annotations
            .IgnoreQueryFilters()
            .Where(a => a.DocumentId == request.DocumentId)
            .ToListAsync(cancellationToken);

        _db.Annotations.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        var entities = request.Annotations.Select(dto => new DocumentAnnotation
        {
            Id = dto.Id is { } id && id != Guid.Empty ? id : Guid.NewGuid(),
            DocumentId = request.DocumentId,
            Type = dto.Type,
            PageIndex = dto.PageIndex,
            X = dto.X,
            Y = dto.Y,
            Width = dto.Width,
            Height = dto.Height,
            Color = dto.Color,
            Opacity = dto.Opacity <= 0 ? 1 : dto.Opacity,
            Contents = dto.Contents,
            Author = dto.Author,
            GeometryJson = dto.GeometryJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }).ToList();

        _db.Annotations.AddRange(entities);
        await _db.SaveChangesAsync(cancellationToken);
        return entities.Select(ToDto).ToList();
    }

    public async Task<string> Handle(ExportXfdfQuery request, CancellationToken cancellationToken)
    {
        var doc = await EnsureDocumentAsync(request.DocumentId, cancellationToken);
        var items = await _db.Annotations.AsNoTracking()
            .Where(a => a.DocumentId == request.DocumentId)
            .ToListAsync(cancellationToken);
        return _xfdf.Export(items.Select(ToModel), doc.FileName);
    }

    public async Task<IReadOnlyList<AnnotationDto>> Handle(ImportXfdfCommand request, CancellationToken cancellationToken)
    {
        await EnsureDocumentAsync(request.DocumentId, cancellationToken);
        var imported = _xfdf.Import(request.XfdfXml);
        var upserts = imported.Select(a => new AnnotationUpsertDto(
            a.Id,
            a.Type.ToString(),
            a.PageIndex,
            a.Rect.X,
            a.Rect.Y,
            a.Rect.Width,
            a.Rect.Height,
            a.Color,
            a.Opacity,
            a.Contents,
            a.Author,
            SerializeGeometry(a))).ToList();

        if (!request.ReplaceExisting)
        {
            var current = await _db.Annotations.AsNoTracking()
                .Where(a => a.DocumentId == request.DocumentId)
                .ToListAsync(cancellationToken);
            upserts = current.Select(c => new AnnotationUpsertDto(
                    c.Id, c.Type, c.PageIndex, c.X, c.Y, c.Width, c.Height, c.Color, c.Opacity, c.Contents, c.Author, c.GeometryJson))
                .Concat(upserts)
                .ToList();
        }

        return await Handle(new ReplaceAnnotationsCommand(request.DocumentId, upserts), cancellationToken);
    }

    private async Task<Document> EnsureDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        return await _db.Documents.FindAsync([documentId], cancellationToken)
               ?? throw new NotFoundException($"Document '{documentId}' was not found.");
    }

    private static AnnotationDto ToDto(DocumentAnnotation a) => new(
        a.Id, a.DocumentId, a.Type, a.PageIndex, a.X, a.Y, a.Width, a.Height,
        a.Color, a.Opacity, a.Contents, a.Author, a.GeometryJson, a.CreatedAtUtc, a.UpdatedAtUtc);

    private static PdfAnnotation ToModel(DocumentAnnotation a)
    {
        var model = new PdfAnnotation
        {
            Id = a.Id,
            Type = Enum.TryParse<PdfAnnotationType>(a.Type, true, out var t) ? t : PdfAnnotationType.Highlight,
            PageIndex = a.PageIndex,
            Rect = new PdfRect(a.X, a.Y, a.Width, a.Height),
            Color = a.Color,
            Opacity = a.Opacity,
            Contents = a.Contents,
            Author = a.Author,
            CreatedAtUtc = a.CreatedAtUtc,
            UpdatedAtUtc = a.UpdatedAtUtc
        };

        if (!string.IsNullOrWhiteSpace(a.GeometryJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(a.GeometryJson);
                if (doc.RootElement.TryGetProperty("quads", out var quads))
                {
                    model.Quads = quads.EnumerateArray()
                        .Select(q => new PdfRect(
                            q.GetProperty("x").GetDouble(),
                            q.GetProperty("y").GetDouble(),
                            q.GetProperty("width").GetDouble(),
                            q.GetProperty("height").GetDouble()))
                        .ToList();
                }

                if (doc.RootElement.TryGetProperty("inkPaths", out var paths))
                {
                    model.InkPaths = paths.EnumerateArray()
                        .Select(path => (IReadOnlyList<PdfPoint>)path.EnumerateArray()
                            .Select(pt => new PdfPoint(pt.GetProperty("x").GetDouble(), pt.GetProperty("y").GetDouble()))
                            .ToList())
                        .ToList();
                }
            }
            catch (JsonException)
            {
                // ignore malformed geometry
            }
        }

        return model;
    }

    private static string? SerializeGeometry(PdfAnnotation a)
    {
        if ((a.Quads is null || a.Quads.Count == 0) && (a.InkPaths is null || a.InkPaths.Count == 0))
        {
            return null;
        }

        return JsonSerializer.Serialize(new
        {
            quads = a.Quads?.Select(q => new { x = q.X, y = q.Y, width = q.Width, height = q.Height }),
            inkPaths = a.InkPaths?.Select(path => path.Select(p => new { x = p.X, y = p.Y }))
        }, JsonOptions);
    }
}
