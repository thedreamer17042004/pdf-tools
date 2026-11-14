using Acman.Pdf.Abstractions.Models;

namespace Acman.Pdf.Abstractions.Contracts;

/// <summary>Aspose-like document info / page properties API.</summary>
public interface IPdfDocumentInfoService
{
    Task<PdfDocumentInfo> GetInfoAsync(Stream source, CancellationToken cancellationToken = default);

    Task<byte[]> SetInfoAsync(
        Stream source,
        PdfInfoUpdate update,
        CancellationToken cancellationToken = default);
}

public sealed record PdfDocumentInfo(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator,
    string? Producer,
    DateTimeOffset? CreationDate,
    DateTimeOffset? ModificationDate,
    int PageCount,
    IReadOnlyList<PdfPageInfo> Pages);

public sealed record PdfInfoUpdate(
    string? Title = null,
    string? Author = null,
    string? Subject = null,
    string? Keywords = null,
    string? Creator = null,
    string? Producer = null);
