namespace Modules.Processing.Application;

public sealed class MergeJobPayload
{
    public List<Guid> DocumentIds { get; set; } = [];
    public string OutputFileName { get; set; } = "merged.pdf";
}

public sealed class SplitJobPayload
{
    public Guid DocumentId { get; set; }
    /// <summary>Optional 1-based ranges. Empty = one file per page.</summary>
    public List<SplitRangeDto> Ranges { get; set; } = [];
    public string OutputFileNamePrefix { get; set; } = "split";
}

public sealed class SplitRangeDto
{
    public int From { get; set; }
    public int To { get; set; }
}

public sealed class RotateJobPayload
{
    public Guid DocumentId { get; set; }
    public int Degrees { get; set; } = 90;
    /// <summary>0-based page indexes. Empty = all pages.</summary>
    public List<int> PageIndexes { get; set; } = [];
    public string OutputFileName { get; set; } = "rotated.pdf";
}

public sealed class CompressJobPayload
{
    public Guid DocumentId { get; set; }
    public bool RemoveMetadata { get; set; } = true;
    public string OutputFileName { get; set; } = "compressed.pdf";
}

public sealed class OrganizeJobPayload
{
    public Guid DocumentId { get; set; }
    /// <summary>0-based page indexes in desired order.</summary>
    public List<int> PageOrder { get; set; } = [];
    public string OutputFileName { get; set; } = "organized.pdf";
}

public sealed class WatermarkJobPayload
{
    public Guid DocumentId { get; set; }
    public string Text { get; set; } = "Nam-Dev";
    public double Opacity { get; set; } = 0.25;
    public string OutputFileName { get; set; } = "watermarked.pdf";
}

public sealed class ProtectJobPayload
{
    public Guid DocumentId { get; set; }
    public string UserPassword { get; set; } = string.Empty;
    public string? OwnerPassword { get; set; }
    public string OutputFileName { get; set; } = "protected.pdf";
}

public sealed class ExtractPagesJobPayload
{
    public Guid DocumentId { get; set; }
    public List<int> PageIndexes { get; set; } = [];
    public string OutputFileName { get; set; } = "extracted.pdf";
}

public sealed class ReverseJobPayload
{
    public Guid DocumentId { get; set; }
    public string OutputFileName { get; set; } = "reversed.pdf";
}

public sealed class DeletePagesJobPayload
{
    public Guid DocumentId { get; set; }
    public List<int> PageIndexes { get; set; } = [];
    public string OutputFileName { get; set; } = "deleted-pages.pdf";
}

public sealed class PageNumbersJobPayload
{
    public Guid DocumentId { get; set; }
    public string Format { get; set; } = "{n} / {total}";
    public string OutputFileName { get; set; } = "numbered.pdf";
}

public sealed class UnlockJobPayload
{
    public Guid DocumentId { get; set; }
    public string UserPassword { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = "unlocked.pdf";
}

public sealed class PdfToImagesJobPayload
{
    public Guid DocumentId { get; set; }
    public int Dpi { get; set; } = 144;
    public string OutputFileName { get; set; } = "pages.zip";
}

public sealed record ProcessingJobDto(
    Guid Id,
    string Type,
    string Status,
    string? Error,
    string? ResultFileName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool HasResult);
