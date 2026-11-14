namespace Modules.Documents.Domain;

public sealed class Document
{
    public Guid Id { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long SizeBytes { get; set; }
    public int PageCount { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public static Document Create(
        string fileName,
        string contentType,
        long sizeBytes,
        int pageCount,
        string storageKey,
        Guid? ownerUserId = null)
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            PageCount = pageCount,
            StorageKey = storageKey,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public bool CanAccess(Guid? requesterUserId)
    {
        if (OwnerUserId is null) return true;
        return requesterUserId is { } uid && uid == OwnerUserId;
    }
}
