namespace Acman.Pdf.Abstractions.Contracts;

/// <summary>Aspose-like outline / bookmark API.</summary>
public interface IPdfBookmarkService
{
    Task<IReadOnlyList<PdfBookmarkInfo>> ListAsync(
        Stream source,
        CancellationToken cancellationToken = default);

    /// <summary>Replace all top-level bookmarks with the given list.</summary>
    Task<byte[]> SetAsync(
        Stream source,
        IReadOnlyList<PdfBookmarkInput> bookmarks,
        CancellationToken cancellationToken = default);

    Task<byte[]> ClearAsync(Stream source, CancellationToken cancellationToken = default);
}

public sealed record PdfBookmarkInfo(string Title, int PageIndex, bool Opened, IReadOnlyList<PdfBookmarkInfo> Children);

public sealed record PdfBookmarkInput(string Title, int PageIndex, bool Opened = true, IReadOnlyList<PdfBookmarkInput>? Children = null);
