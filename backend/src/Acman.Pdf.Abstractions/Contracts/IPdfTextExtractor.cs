namespace Acman.Pdf.Abstractions.Contracts;

public interface IPdfTextExtractor
{
    Task<PdfTextDocument> ExtractAsync(
        Stream source,
        string? password = null,
        CancellationToken cancellationToken = default);
}

public sealed record PdfTextDocument(IReadOnlyList<PdfTextPage> Pages)
{
    public string FullText => string.Join("\n\n", Pages.Select(p => p.Text));
}

public sealed record PdfTextPage(int PageIndex, string Text);
