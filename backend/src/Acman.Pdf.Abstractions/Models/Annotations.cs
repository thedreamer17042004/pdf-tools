namespace Acman.Pdf.Abstractions.Models;

public enum PdfAnnotationType
{
    Highlight = 1,
    Underline = 2,
    Strikeout = 3,
    Squiggly = 4,
    StickyNote = 5,
    FreeText = 6,
    Ink = 7,
    Rectangle = 8,
    Circle = 9,
    Line = 10
}

/// <summary>PDF user-space rectangle: x,y = lower-left; width/height in points.</summary>
public sealed record PdfRect(double X, double Y, double Width, double Height);

public sealed record PdfPoint(double X, double Y);

public sealed class PdfAnnotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public PdfAnnotationType Type { get; set; }
    public int PageIndex { get; set; }
    public PdfRect Rect { get; set; } = new(0, 0, 0, 0);
    public string? Color { get; set; } = "#FFFF00";
    public double Opacity { get; set; } = 1;
    public string? Contents { get; set; }
    public string? Author { get; set; }
    public IReadOnlyList<PdfRect>? Quads { get; set; }
    public IReadOnlyList<IReadOnlyList<PdfPoint>>? InkPaths { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AnnotationDocument
{
    public IList<PdfAnnotation> Annotations { get; set; } = new List<PdfAnnotation>();
}
