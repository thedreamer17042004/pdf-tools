namespace Acman.Pdf.Abstractions.Contracts;

public interface IPdfManipulationService
{
    Task<byte[]> MergeAsync(IReadOnlyList<Stream> sources, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<byte[]>> SplitAsync(
        Stream source,
        IReadOnlyList<PageRange>? ranges = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> RotateAsync(
        Stream source,
        int degreesClockwise,
        IReadOnlyList<int>? pageIndexes = null,
        CancellationToken cancellationToken = default);

    Task<CompressResult> CompressAsync(
        Stream source,
        CompressOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuild document using 0-based page indexes in the desired order.
    /// Indexes may omit pages (delete) or repeat pages (duplicate).
    /// </summary>
    Task<byte[]> OrganizeAsync(
        Stream source,
        IReadOnlyList<int> pageOrder,
        CancellationToken cancellationToken = default);

    Task<byte[]> WatermarkAsync(
        Stream source,
        WatermarkOptions options,
        CancellationToken cancellationToken = default);

    Task<byte[]> ProtectAsync(
        Stream source,
        ProtectOptions options,
        CancellationToken cancellationToken = default);

    Task<byte[]> ImagesToPdfAsync(
        IReadOnlyList<ImagePageInput> images,
        CancellationToken cancellationToken = default);

    /// <summary>Extract 0-based page indexes into a new PDF (order preserved as given).</summary>
    Task<byte[]> ExtractPagesAsync(
        Stream source,
        IReadOnlyList<int> pageIndexes,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReversePagesAsync(
        Stream source,
        CancellationToken cancellationToken = default);

    Task<byte[]> DeletePagesAsync(
        Stream source,
        IReadOnlyList<int> pageIndexesToDelete,
        CancellationToken cancellationToken = default);

    Task<byte[]> AddPageNumbersAsync(
        Stream source,
        PageNumberOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Open with user password and rewrite without encryption.</summary>
    Task<byte[]> UnlockAsync(
        Stream source,
        string userPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Insert one or more blank pages at a 0-based index.</summary>
    Task<byte[]> InsertBlankPagesAsync(
        Stream source,
        int atIndex,
        int count = 1,
        double? widthPoints = null,
        double? heightPoints = null,
        CancellationToken cancellationToken = default);

    /// <summary>Insert pages from another PDF at a 0-based index.</summary>
    Task<byte[]> InsertPagesAsync(
        Stream source,
        Stream pagesSource,
        int atIndex,
        IReadOnlyList<int>? pageIndexes = null,
        CancellationToken cancellationToken = default);

    /// <summary>Append pages from another PDF to the end.</summary>
    Task<byte[]> AppendAsync(
        Stream source,
        Stream appendix,
        CancellationToken cancellationToken = default);

    Task<byte[]> AddTextAsync(
        Stream source,
        AddTextOptions options,
        CancellationToken cancellationToken = default);

    Task<byte[]> HeaderFooterAsync(
        Stream source,
        HeaderFooterOptions options,
        CancellationToken cancellationToken = default);

    Task<byte[]> StampImageAsync(
        Stream source,
        StampImageOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Cover regions with opaque rectangles (best-effort visual redaction).</summary>
    Task<byte[]> RedactCoverAsync(
        Stream source,
        IReadOnlyList<RedactRegion> regions,
        CancellationToken cancellationToken = default);

    Task<byte[]> CropPagesAsync(
        Stream source,
        CropOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Best-effort repair: rebuild by re-importing pages (like iLovePDF Repair).</summary>
    Task<byte[]> RepairAsync(Stream source, CancellationToken cancellationToken = default);

    /// <summary>Visual signature stamp (image and/or name) — not a cryptographic / PAdES signature.</summary>
    Task<byte[]> SignStampAsync(
        Stream source,
        SignStampOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record PageNumberOptions(
    string Format = "{n} / {total}",
    double FontSize = 10,
    string ColorHex = "#64748B",
    PageNumberPosition Position = PageNumberPosition.BottomCenter,
    double MarginPoints = 24);

public enum PageNumberPosition
{
    BottomCenter = 0,
    BottomLeft = 1,
    BottomRight = 2,
    TopCenter = 3
}

public sealed record WatermarkOptions(
    string Text,
    double Opacity = 0.25,
    double FontSize = 48,
    string ColorHex = "#64748B",
    double AngleDegrees = -35);

public sealed record ProtectOptions(
    string UserPassword,
    string? OwnerPassword = null);

public sealed record ImagePageInput(byte[] Bytes, string ContentType);

/// <summary>1-based inclusive page range.</summary>
public sealed record PageRange(int From, int To);

public sealed record CompressOptions(bool RemoveMetadata = true, bool RebuildViaImport = true);

public sealed record CompressResult(byte[] Bytes, long OriginalSizeBytes, long CompressedSizeBytes)
{
    public double Ratio => OriginalSizeBytes == 0 ? 1 : (double)CompressedSizeBytes / OriginalSizeBytes;
}

public sealed record AddTextOptions(
    string Text,
    double XPoints,
    double YPoints,
    int? PageIndex = null,
    double FontSize = 12,
    string ColorHex = "#0F172A");

public sealed record HeaderFooterOptions(
    string? Header = null,
    string? Footer = null,
    double FontSize = 10,
    string ColorHex = "#64748B",
    double MarginPoints = 28);

public sealed record StampImageOptions(
    byte[] ImageBytes,
    double XPoints,
    double YPoints,
    double WidthPoints,
    double HeightPoints,
    int? PageIndex = null,
    double Opacity = 1);

/// <summary>PDF-user-space rectangle (origin bottom-left).</summary>
public sealed record RedactRegion(
    int PageIndex,
    double XPoints,
    double YPoints,
    double WidthPoints,
    double HeightPoints,
    string ColorHex = "#000000");

public sealed record CropOptions(
    double LeftPoints,
    double BottomPoints,
    double RightPoints,
    double TopPoints,
    IReadOnlyList<int>? PageIndexes = null);

public sealed record SignStampOptions(
    int PageIndex,
    double XPoints,
    double YPoints,
    string? SignerName = null,
    byte[]? ImageBytes = null,
    double WidthPoints = 160,
    double HeightPoints = 60,
    double FontSize = 14,
    string ColorHex = "#1D4ED8");
