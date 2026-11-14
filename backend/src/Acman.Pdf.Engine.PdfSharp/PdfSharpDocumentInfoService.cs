using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Abstractions.Models;
using Acman.Pdf.Core.Validation;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf.IO;

namespace Acman.Pdf.Engine.PdfSharp;

public sealed class PdfSharpDocumentInfoService : IPdfDocumentInfoService
{
    private readonly ILogger<PdfSharpDocumentInfoService> _logger;

    public PdfSharpDocumentInfoService(ILogger<PdfSharpDocumentInfoService> logger) => _logger = logger;

    public async Task<PdfDocumentInfo> GetInfoAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        var pages = new List<PdfPageInfo>(doc.PageCount);
        for (var i = 0; i < doc.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = doc.Pages[i];
            pages.Add(new PdfPageInfo(i, page.Width.Point, page.Height.Point, page.Rotate));
        }

        var info = doc.Info;
        return new PdfDocumentInfo(
            NullIfEmpty(info.Title),
            NullIfEmpty(info.Author),
            NullIfEmpty(info.Subject),
            NullIfEmpty(info.Keywords),
            NullIfEmpty(info.Creator),
            NullIfEmpty(info.Producer),
            ToOffset(info.CreationDate),
            ToOffset(info.ModificationDate),
            doc.PageCount,
            pages);
    }

    public async Task<byte[]> SetInfoAsync(
        Stream source,
        PdfInfoUpdate update,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        var info = doc.Info;
        if (update.Title is not null) info.Title = update.Title;
        if (update.Author is not null) info.Author = update.Author;
        if (update.Subject is not null) info.Subject = update.Subject;
        if (update.Keywords is not null) info.Keywords = update.Keywords;
        if (update.Creator is not null) info.Creator = update.Creator;
        // Producer is read-only in PDFsharp (library identity).
        info.ModificationDate = DateTime.UtcNow;

        _logger.LogInformation("Updated PDF document info");
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateTimeOffset? ToOffset(DateTime value) =>
        value == DateTime.MinValue ? null : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek) stream.Position = 0;
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
