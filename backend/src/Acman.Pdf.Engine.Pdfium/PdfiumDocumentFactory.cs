using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Core.Validation;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Logging;

namespace Acman.Pdf.Engine.Pdfium;

public sealed class PdfiumDocumentFactory : IPdfDocumentFactory
{
    private readonly IDocLib _docLib;
    private readonly ILogger<PdfiumDocumentFactory> _logger;

    public PdfiumDocumentFactory(ILogger<PdfiumDocumentFactory> logger)
        : this(DocLib.Instance, logger)
    {
    }

    internal PdfiumDocumentFactory(IDocLib docLib, ILogger<PdfiumDocumentFactory> logger)
    {
        _docLib = docLib;
        _logger = logger;
    }

    public async Task<IPdfDocument> OpenAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        await PdfFileValidator.EnsurePdfMagicAsync(stream, cancellationToken);
        await using var copy = new MemoryStream();
        stream.Position = 0;
        await stream.CopyToAsync(copy, cancellationToken);
        return await OpenAsync(copy.ToArray(), password, cancellationToken);
    }

    public Task<IPdfDocument> OpenAsync(
        byte[] bytes,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PdfFileValidator.EnsurePdfMagic(bytes);

        try
        {
            using var probe = string.IsNullOrEmpty(password)
                ? _docLib.GetDocReader(bytes, new PageDimensions(1.0))
                : _docLib.GetDocReader(bytes, password, new PageDimensions(1.0));

            var pageCount = probe.GetPageCount();
            _logger.LogDebug("Opened PDF via PDFium with {PageCount} page(s)", pageCount);
            return Task.FromResult<IPdfDocument>(new PdfiumPdfDocument(_docLib, bytes, password, pageCount));
        }
        catch (Exception ex) when (IsPasswordRelated(ex, password))
        {
            throw new PdfPasswordRequiredException();
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to open PDF with PDFium engine.", ex);
        }
    }

    public async Task<IPdfDocument> OpenAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return await OpenAsync(bytes, password, cancellationToken);
    }

    private static bool IsPasswordRelated(Exception ex, string? password)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("password", StringComparison.OrdinalIgnoreCase)
               || (string.IsNullOrEmpty(password) && message.Contains("encrypt", StringComparison.OrdinalIgnoreCase));
    }
}
