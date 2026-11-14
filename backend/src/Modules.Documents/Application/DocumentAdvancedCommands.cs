using Acman.BuildingBlocks.Errors;
using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Storage;
using MediatR;
using Modules.Documents.Infrastructure;

namespace Modules.Documents.Application;

public sealed record ExtractDocumentTextQuery(Guid DocumentId) : IRequest<DocumentTextDto>;
public sealed record DocumentTextDto(Guid DocumentId, IReadOnlyList<DocumentTextPageDto> Pages, string FullText);
public sealed record DocumentTextPageDto(int PageIndex, string Text);

public sealed record ListFormFieldsQuery(Guid DocumentId) : IRequest<IReadOnlyList<FormFieldDto>>;
public sealed record FormFieldDto(string Name, string FieldType, string? Value, bool ReadOnly);

public sealed record FillFormCommand(Guid DocumentId, IReadOnlyDictionary<string, string> Values) : IRequest<byte[]>;
public sealed record MergeFormCommand(
    Guid DocumentId,
    IReadOnlyDictionary<string, string> Values,
    bool Flatten = false) : IRequest<byte[]>;
public sealed record FlattenFormCommand(Guid DocumentId) : IRequest<byte[]>;

public sealed class DocumentAdvancedHandlers :
    IRequestHandler<ExtractDocumentTextQuery, DocumentTextDto>,
    IRequestHandler<ListFormFieldsQuery, IReadOnlyList<FormFieldDto>>,
    IRequestHandler<FillFormCommand, byte[]>,
    IRequestHandler<MergeFormCommand, byte[]>,
    IRequestHandler<FlattenFormCommand, byte[]>
{
    private readonly DocumentsDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IPdfTextExtractor _text;
    private readonly IPdfFormService _forms;

    public DocumentAdvancedHandlers(
        DocumentsDbContext db,
        IObjectStorage storage,
        IPdfTextExtractor text,
        IPdfFormService forms)
    {
        _db = db;
        _storage = storage;
        _text = text;
        _forms = forms;
    }

    public async Task<DocumentTextDto> Handle(ExtractDocumentTextQuery request, CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.DocumentId], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var extracted = await _text.ExtractAsync(stream, cancellationToken: cancellationToken);
        return new DocumentTextDto(
            entity.Id,
            extracted.Pages.Select(p => new DocumentTextPageDto(p.PageIndex, p.Text)).ToList(),
            extracted.FullText);
    }

    public async Task<IReadOnlyList<FormFieldDto>> Handle(ListFormFieldsQuery request, CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.DocumentId], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var fields = await _forms.ListFieldsAsync(stream, cancellationToken);
        return fields.Select(f => new FormFieldDto(f.Name, f.FieldType, f.Value, f.ReadOnly)).ToList();
    }

    public async Task<byte[]> Handle(FillFormCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.DocumentId], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        return await _forms.FillFieldsAsync(stream, request.Values, cancellationToken);
    }

    public async Task<byte[]> Handle(MergeFormCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.DocumentId], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        return await _forms.MergeFieldsAsync(
            stream,
            request.Values,
            new MergeFieldsOptions(request.Flatten),
            cancellationToken);
    }

    public async Task<byte[]> Handle(FlattenFormCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Documents.FindAsync([request.DocumentId], cancellationToken)
                     ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        return await _forms.FlattenAsync(stream, cancellationToken);
    }
}
