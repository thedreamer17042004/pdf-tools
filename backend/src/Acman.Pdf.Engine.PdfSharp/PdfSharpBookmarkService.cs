using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Core.Validation;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Acman.Pdf.Engine.PdfSharp;

public sealed class PdfSharpBookmarkService : IPdfBookmarkService
{
    private readonly ILogger<PdfSharpBookmarkService> _logger;

    public PdfSharpBookmarkService(ILogger<PdfSharpBookmarkService> logger) => _logger = logger;

    public async Task<IReadOnlyList<PdfBookmarkInfo>> ListAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        return MapOutlines(doc.Outlines, doc);
    }

    public async Task<byte[]> SetAsync(
        Stream source,
        IReadOnlyList<PdfBookmarkInput> bookmarks,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        doc.Outlines.Clear();
        AddOutlines(doc.Outlines, bookmarks, doc, cancellationToken);
        _logger.LogInformation("Set {Count} top-level bookmark(s)", bookmarks.Count);
        return Save(doc);
    }

    public async Task<byte[]> ClearAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        doc.Outlines.Clear();
        _logger.LogInformation("Cleared bookmarks");
        return Save(doc);
    }

    private static IReadOnlyList<PdfBookmarkInfo> MapOutlines(PdfOutlineCollection outlines, PdfDocument doc)
    {
        var list = new List<PdfBookmarkInfo>(outlines.Count);
        foreach (PdfOutline outline in outlines)
        {
            var pageIndex = 0;
            if (outline.DestinationPage is not null)
            {
                for (var i = 0; i < doc.PageCount; i++)
                {
                    if (ReferenceEquals(doc.Pages[i], outline.DestinationPage))
                    {
                        pageIndex = i;
                        break;
                    }
                }
            }

            list.Add(new PdfBookmarkInfo(
                outline.Title ?? string.Empty,
                pageIndex,
                outline.Opened,
                MapOutlines(outline.Outlines, doc)));
        }

        return list;
    }

    private static void AddOutlines(
        PdfOutlineCollection target,
        IReadOnlyList<PdfBookmarkInput> bookmarks,
        PdfDocument doc,
        CancellationToken cancellationToken)
    {
        foreach (var bookmark in bookmarks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (bookmark.PageIndex < 0 || bookmark.PageIndex >= doc.PageCount)
            {
                throw new PdfPageOutOfRangeException(bookmark.PageIndex, doc.PageCount);
            }

            var outline = target.Add(bookmark.Title, doc.Pages[bookmark.PageIndex], bookmark.Opened);
            if (bookmark.Children is { Count: > 0 })
            {
                AddOutlines(outline.Outlines, bookmark.Children, doc, cancellationToken);
            }
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek) stream.Position = 0;
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static byte[] Save(PdfDocument document)
    {
        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }
}
