namespace Acman.Word.Abstractions.Contracts;

/// <summary>
/// DOCX → PDF via Nam-Dev's own Open XML → PdfSharp layout engine (no LibreOffice / Word).
/// </summary>
public interface IWordToPdfConverter
{
    Task<byte[]> ConvertAsync(
        Stream source,
        WordToPdfOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed record WordToPdfOptions(
    string? Title = null,
    double MarginPoints = 56,
    double BodyFontSize = 11);
