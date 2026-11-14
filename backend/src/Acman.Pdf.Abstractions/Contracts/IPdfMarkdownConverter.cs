namespace Acman.Pdf.Abstractions.Contracts;

/// <summary>PDF → Markdown (text-based; no OCR).</summary>
public interface IPdfMarkdownConverter
{
    Task<string> ToMarkdownAsync(Stream source, CancellationToken cancellationToken = default);
}
