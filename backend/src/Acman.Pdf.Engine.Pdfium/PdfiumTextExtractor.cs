using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Core.Validation;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Logging;

namespace Acman.Pdf.Engine.Pdfium;

public sealed class PdfiumTextExtractor : IPdfTextExtractor
{
    private readonly IDocLib _docLib;
    private readonly ILogger<PdfiumTextExtractor> _logger;

    public PdfiumTextExtractor(ILogger<PdfiumTextExtractor> logger)
    {
        _docLib = DocLib.Instance;
        _logger = logger;
    }

    public async Task<PdfTextDocument> ExtractAsync(
        Stream source,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        if (source.CanSeek) source.Position = 0;
        await source.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        PdfFileValidator.EnsurePdfMagic(bytes);

        try
        {
            using var reader = string.IsNullOrEmpty(password)
                ? _docLib.GetDocReader(bytes, new PageDimensions(1.0))
                : _docLib.GetDocReader(bytes, password, new PageDimensions(1.0));

            var pages = new List<PdfTextPage>(reader.GetPageCount());
            for (var i = 0; i < reader.GetPageCount(); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var page = reader.GetPageReader(i);
                pages.Add(new PdfTextPage(i, page.GetText() ?? string.Empty));
            }

            _logger.LogInformation("Extracted text from {Count} page(s)", pages.Count);
            return new PdfTextDocument(pages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not PdfException)
        {
            throw new PdfOpenException("Failed to extract text from PDF.", ex);
        }
    }
}
