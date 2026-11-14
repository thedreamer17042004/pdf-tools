using Acman.Pdf.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

namespace Acman.Pdf.Engine.Pdfium;

public sealed class PdfiumCompareService : IPdfCompareService
{
    private readonly IPdfTextExtractor _text;
    private readonly IPdfDocumentFactory _factory;
    private readonly ILogger<PdfiumCompareService> _logger;

    public PdfiumCompareService(
        IPdfTextExtractor text,
        IPdfDocumentFactory factory,
        ILogger<PdfiumCompareService> logger)
    {
        _text = text;
        _factory = factory;
        _logger = logger;
    }

    public async Task<PdfCompareResult> CompareAsync(
        Stream left,
        Stream right,
        CancellationToken cancellationToken = default)
    {
        await using var leftCopy = await CopyAsync(left, cancellationToken);
        await using var rightCopy = await CopyAsync(right, cancellationToken);

        await using var leftDoc = await _factory.OpenAsync(leftCopy, cancellationToken: cancellationToken);
        leftCopy.Position = 0;
        await using var rightDoc = await _factory.OpenAsync(rightCopy, cancellationToken: cancellationToken);
        rightCopy.Position = 0;

        var leftText = await _text.ExtractAsync(leftCopy, cancellationToken: cancellationToken);
        rightCopy.Position = 0;
        var rightText = await _text.ExtractAsync(rightCopy, cancellationToken: cancellationToken);

        var pageCountMatch = leftDoc.PageCount == rightDoc.PageCount;
        var maxPages = Math.Max(leftDoc.PageCount, rightDoc.PageCount);
        var diffs = new List<PdfPageTextDiff>(maxPages);
        var matchedPages = 0;

        for (var i = 0; i < maxPages; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var l = leftText.Pages.FirstOrDefault(p => p.PageIndex == i)?.Text?.Trim() ?? string.Empty;
            var r = rightText.Pages.FirstOrDefault(p => p.PageIndex == i)?.Text?.Trim() ?? string.Empty;
            var match = string.Equals(l, r, StringComparison.Ordinal);
            if (match) matchedPages++;
            diffs.Add(new PdfPageTextDiff(
                i,
                match,
                Preview(l),
                Preview(r)));
        }

        var similarity = maxPages == 0 ? 1.0 : (double)matchedPages / maxPages;
        var fullTextMatch = string.Equals(
            Normalize(leftText.FullText),
            Normalize(rightText.FullText),
            StringComparison.Ordinal);
        var equal = pageCountMatch && fullTextMatch;

        var summary = equal
            ? "Documents appear identical (page count + extracted text)."
            : pageCountMatch
                ? $"Same page count ({leftDoc.PageCount}), text differs (similarity {similarity:P0})."
                : $"Different page counts ({leftDoc.PageCount} vs {rightDoc.PageCount}).";

        _logger.LogInformation("Compared PDFs: equal={Equal}, similarity={Similarity:P0}", equal, similarity);
        return new PdfCompareResult(
            equal,
            leftDoc.PageCount,
            rightDoc.PageCount,
            pageCountMatch,
            fullTextMatch,
            similarity,
            diffs,
            summary);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n").Trim();

    private static string? Preview(string value) =>
        string.IsNullOrEmpty(value) ? null : (value.Length <= 120 ? value : value[..117] + "...");

    private static async Task<MemoryStream> CopyAsync(Stream source, CancellationToken ct)
    {
        if (source.CanSeek) source.Position = 0;
        var ms = new MemoryStream();
        await source.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}
