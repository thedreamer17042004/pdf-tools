namespace Acman.Pdf.Abstractions.Models;

public sealed record PdfMetadata(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator,
    string? Producer,
    DateTimeOffset? CreationDate,
    DateTimeOffset? ModificationDate,
    int PageCount);

public sealed record PdfPageInfo(
    int Index,
    double WidthPoints,
    double HeightPoints,
    int RotationDegrees);

public sealed record RenderOptions(
    int Dpi = 144,
    int? MaxWidth = null,
    bool IncludeAnnotations = true)
{
    public static RenderOptions Default { get; } = new();
}

public sealed record RenderedPage(
    int PageIndex,
    byte[] ImageBytes,
    string ContentType,
    int WidthPixels,
    int HeightPixels,
    int Dpi);
