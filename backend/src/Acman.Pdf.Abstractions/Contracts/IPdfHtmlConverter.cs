namespace Acman.Pdf.Abstractions.Contracts;

/// <summary>Simple HTML → PDF (text/layout; not a full browser engine).</summary>
public interface IPdfHtmlConverter
{
    Task<byte[]> FromHtmlAsync(
        string html,
        HtmlToPdfOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed record HtmlToPdfOptions(
    string? Title = null,
    double FontSize = 12,
    double MarginPoints = 48);
