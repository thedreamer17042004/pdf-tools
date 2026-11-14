using Acman.BuildingBlocks.Errors;
using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Storage;
using Acman.Pdf.Core.Validation;
using Acman.Pdf.Infrastructure.Options;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Documents.Contracts;
using Modules.Documents.Domain;
using Modules.Documents.Infrastructure;

namespace Modules.Documents.Application;

public sealed record UploadDocumentCommand(
    string FileName,
    string ContentType,
    Stream Content,
    long? DeclaredSizeBytes,
    Guid? OwnerUserId = null) : IRequest<UploadDocumentResult>;

public sealed class UploadDocumentValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentValidator(IOptions<DocumentsOptions> options)
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(512);
        RuleFor(x => x.ContentType)
            .Must(ct => string.IsNullOrWhiteSpace(ct)
                        || ct.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                        || ct.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Content type must be a PDF.");
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.DeclaredSizeBytes)
            .Must(size => size is null || size <= options.Value.MaxUploadBytes)
            .WithMessage(x => $"File exceeds max upload size of {options.Value.MaxUploadBytes} bytes.");
    }
}

public sealed class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, UploadDocumentResult>
{
    private readonly DocumentsDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IPdfDocumentFactory _pdfFactory;
    private readonly DocumentsOptions _options;

    public UploadDocumentHandler(
        DocumentsDbContext db,
        IObjectStorage storage,
        IPdfDocumentFactory pdfFactory,
        IOptions<DocumentsOptions> options)
    {
        _db = db;
        _storage = storage;
        _pdfFactory = pdfFactory;
        _options = options.Value;
    }

    public async Task<UploadDocumentResult> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        PdfFileValidator.EnsureWithinSizeLimit(buffer.Length, _options.MaxUploadBytes);
        buffer.Position = 0;
        await PdfFileValidator.EnsurePdfMagicAsync(buffer, cancellationToken);

        buffer.Position = 0;
        await using var opened = await _pdfFactory.OpenAsync(buffer, password: null, cancellationToken);
        var pageCount = opened.PageCount;

        var id = Guid.NewGuid();
        var storageKey = $"documents/{id:N}/{SanitizeFileName(request.FileName)}";
        buffer.Position = 0;
        await _storage.EnsureBucketAsync(cancellationToken);
        await _storage.PutAsync(storageKey, buffer, "application/pdf", cancellationToken);

        var entity = Document.Create(
            fileName: Path.GetFileName(request.FileName),
            contentType: "application/pdf",
            sizeBytes: buffer.Length,
            pageCount: pageCount,
            storageKey: storageKey,
            ownerUserId: request.OwnerUserId);
        entity.Id = id;

        _db.Documents.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new UploadDocumentResult(ToDto(entity));
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "document.pdf" : name;
    }

    internal static DocumentDto ToDto(Document entity) => new(
        entity.Id,
        entity.FileName,
        entity.ContentType,
        entity.SizeBytes,
        entity.PageCount,
        entity.CreatedAtUtc,
        entity.OwnerUserId);
}

public sealed record GetDocumentQuery(Guid Id, Guid? RequesterUserId = null) : IRequest<DocumentDto>;

public sealed class GetDocumentHandler : IRequestHandler<GetDocumentQuery, DocumentDto>
{
    private readonly DocumentsDbContext _db;

    public GetDocumentHandler(DocumentsDbContext db) => _db = db;

    public async Task<DocumentDto> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.Id], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.Id}' was not found.");
        if (!entity.CanAccess(request.RequesterUserId))
        {
            throw new ForbiddenException("You do not have access to this document.");
        }

        return UploadDocumentHandler.ToDto(entity);
    }
}

public sealed record ListMyDocumentsQuery(Guid OwnerUserId) : IRequest<IReadOnlyList<DocumentDto>>;

public sealed class ListMyDocumentsHandler : IRequestHandler<ListMyDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    private readonly DocumentsDbContext _db;

    public ListMyDocumentsHandler(DocumentsDbContext db) => _db = db;

    public async Task<IReadOnlyList<DocumentDto>> Handle(ListMyDocumentsQuery request, CancellationToken cancellationToken)
    {
        var items = await _db.Documents.AsNoTracking()
            .Where(d => d.OwnerUserId == request.OwnerUserId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        return items.Select(UploadDocumentHandler.ToDto).ToList();
    }
}

public sealed record DeleteDocumentCommand(Guid Id, Guid RequesterUserId) : IRequest;

public sealed class DeleteDocumentHandler : IRequestHandler<DeleteDocumentCommand>
{
    private readonly DocumentsDbContext _db;
    private readonly IObjectStorage _storage;

    public DeleteDocumentHandler(DocumentsDbContext db, IObjectStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.Id], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.Id}' was not found.");
        if (!entity.CanAccess(request.RequesterUserId) || entity.OwnerUserId is null)
        {
            throw new ForbiddenException("Only the document owner can delete it.");
        }

        entity.DeletedAtUtc = DateTimeOffset.UtcNow;
        entity.UpdatedAtUtc = entity.DeletedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            await _storage.DeleteAsync(entity.StorageKey, cancellationToken);
        }
        catch
        {
            // Soft-delete already applied; storage cleanup best-effort.
        }
    }
}
