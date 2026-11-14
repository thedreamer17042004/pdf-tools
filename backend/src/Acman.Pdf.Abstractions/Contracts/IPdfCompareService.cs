namespace Acman.Pdf.Abstractions.Contracts;

/// <summary>Compare two PDFs (page count + extracted text) — iLovePDF Compare-style.</summary>
public interface IPdfCompareService
{
    Task<PdfCompareResult> CompareAsync(
        Stream left,
        Stream right,
        CancellationToken cancellationToken = default);
}

public sealed record PdfCompareResult(
    bool Equal,
    int LeftPageCount,
    int RightPageCount,
    bool PageCountMatch,
    bool TextMatch,
    double TextSimilarity,
    IReadOnlyList<PdfPageTextDiff> PageDiffs,
    string Summary);

public sealed record PdfPageTextDiff(int PageIndex, bool Match, string? LeftPreview, string? RightPreview);
