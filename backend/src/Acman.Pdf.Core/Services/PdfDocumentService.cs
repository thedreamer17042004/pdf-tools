using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Models;
using Acman.Pdf.Core.Validation;

namespace Acman.Pdf.Core.Services;

/// <summary>
/// Engine-agnostic orchestration for open / inspect / render.
/// </summary>
public sealed class PdfDocumentService
{
    private readonly IPdfDocumentFactory _documentFactory;
    private readonly IPdfRenderer _renderer;

    public PdfDocumentService(IPdfDocumentFactory documentFactory, IPdfRenderer renderer)
    {
        _documentFactory = documentFactory;
        _renderer = renderer;
    }

    public async Task<IPdfDocument> OpenAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        await PdfFileValidator.EnsurePdfMagicAsync(stream, cancellationToken);
        return await _documentFactory.OpenAsync(stream, password, cancellationToken);
    }

    public async Task<(int PageCount, PdfMetadata Metadata)> InspectAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        await using var document = await OpenAsync(stream, password, cancellationToken);
        return (document.PageCount, document.GetMetadata());
    }

    public Task<RenderedPage> RenderAsync(
        Stream stream,
        int pageIndex,
        RenderOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default)
        => _renderer.RenderPageAsync(stream, pageIndex, options, password, cancellationToken);
}
