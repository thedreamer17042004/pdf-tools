using Acman.Pdf.Abstractions.Models;

namespace Acman.Pdf.Abstractions.Contracts;

public interface IPdfDocument : IAsyncDisposable, IDisposable
{
    int PageCount { get; }
    PdfMetadata GetMetadata();
    PdfPageInfo GetPage(int pageIndex);
    IReadOnlyList<PdfPageInfo> GetPages();
}

public interface IPdfDocumentFactory
{
    Task<IPdfDocument> OpenAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default);

    Task<IPdfDocument> OpenAsync(
        byte[] bytes,
        string? password = null,
        CancellationToken cancellationToken = default);

    Task<IPdfDocument> OpenAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default);
}

public interface IPdfRenderer
{
    Task<RenderedPage> RenderPageAsync(
        IPdfDocument document,
        int pageIndex,
        RenderOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<RenderedPage> RenderPageAsync(
        Stream stream,
        int pageIndex,
        RenderOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default);
}
