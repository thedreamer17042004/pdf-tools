using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Abstractions.Models;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Acman.Pdf.Engine.Pdfium;

public sealed class PdfiumRenderer : IPdfRenderer
{
    private readonly IPdfDocumentFactory _factory;
    private readonly ILogger<PdfiumRenderer> _logger;

    public PdfiumRenderer(IPdfDocumentFactory factory, ILogger<PdfiumRenderer> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<RenderedPage> RenderPageAsync(
        IPdfDocument document,
        int pageIndex,
        RenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= RenderOptions.Default;

        if (document is not PdfiumPdfDocument pdfiumDoc)
        {
            throw new PdfRenderException("PDFium renderer requires a document opened by the PDFium factory.");
        }

        pdfiumDoc.EnsurePageIndex(pageIndex);

        try
        {
            return await Task.Run(() => RenderCore(pdfiumDoc, pageIndex, options), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfRenderException($"Failed to render page {pageIndex}.", ex);
        }
    }

    public async Task<RenderedPage> RenderPageAsync(
        Stream stream,
        int pageIndex,
        RenderOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        await using var document = await _factory.OpenAsync(stream, password, cancellationToken);
        return await RenderPageAsync(document, pageIndex, options, cancellationToken);
    }

    private RenderedPage RenderCore(PdfiumPdfDocument document, int pageIndex, RenderOptions options)
    {
        var dpi = Math.Clamp(options.Dpi, 36, 600);
        var scaling = dpi / 72.0;

        using var reader = document.CreateDocReader(scaling);
        using var page = reader.GetPageReader(pageIndex);

        var raw = options.IncludeAnnotations
            ? page.GetImage(RenderFlags.RenderAnnotations)
            : page.GetImage();

        var width = page.GetPageWidth();
        var height = page.GetPageHeight();

        using var image = Image.LoadPixelData<Bgra32>(raw, width, height);
        image.Mutate(ctx => ctx.BackgroundColor(Color.White));

        if (options.MaxWidth is > 0 and var maxWidth && image.Width > maxWidth)
        {
            var scale = maxWidth / (double)image.Width;
            var newHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
            image.Mutate(ctx => ctx.Resize(maxWidth, newHeight));
        }

        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());

        _logger.LogDebug(
            "Rendered page {PageIndex} at {Dpi} DPI => {Width}x{Height} PNG ({Bytes} bytes)",
            pageIndex, dpi, image.Width, image.Height, ms.Length);

        return new RenderedPage(
            PageIndex: pageIndex,
            ImageBytes: ms.ToArray(),
            ContentType: "image/png",
            WidthPixels: image.Width,
            HeightPixels: image.Height,
            Dpi: dpi);
    }
}
