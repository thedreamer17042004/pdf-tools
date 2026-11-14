namespace Modules.Documents.Contracts;

public sealed record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    int PageCount,
    DateTimeOffset CreatedAtUtc,
    Guid? OwnerUserId = null);

public sealed record DocumentPageDto(
    int Index,
    double WidthPoints,
    double HeightPoints,
    int RotationDegrees);

public sealed record UploadDocumentResult(DocumentDto Document);
