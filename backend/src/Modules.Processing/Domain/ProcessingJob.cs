namespace Modules.Processing.Domain;

public enum ProcessingJobType
{
    Merge = 1,
    Split = 2,
    Rotate = 3,
    Compress = 4,
    Organize = 5,
    Watermark = 6,
    Protect = 7,
    ExtractPages = 8,
    Reverse = 9,
    DeletePages = 10,
    PageNumbers = 11,
    Unlock = 12,
    PdfToImages = 13
}

public enum ProcessingJobStatus
{
    Pending = 0,
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4
}

public sealed class ProcessingJob
{
    public Guid Id { get; set; }
    public ProcessingJobType Type { get; set; }
    public ProcessingJobStatus Status { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? Error { get; set; }
    public string? ResultStorageKey { get; set; }
    public string? ResultFileName { get; set; }
    public string? ResultContentType { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    public static ProcessingJob Create(ProcessingJobType type, string payloadJson, Guid? ownerUserId)
    {
        return new ProcessingJob
        {
            Id = Guid.NewGuid(),
            Type = type,
            Status = ProcessingJobStatus.Pending,
            PayloadJson = payloadJson,
            OwnerUserId = ownerUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
