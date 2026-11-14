namespace Modules.Documents.Domain;

public sealed class DocumentAnnotation
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public string Type { get; set; } = "Highlight";
    public int PageIndex { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string? Color { get; set; }
    public double Opacity { get; set; } = 1;
    public string? Contents { get; set; }
    public string? Author { get; set; }
    public string? GeometryJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
