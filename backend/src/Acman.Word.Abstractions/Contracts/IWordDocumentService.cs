namespace Acman.Word.Abstractions.Contracts;

/// <summary>Aspose.Words-inspired DOCX API on top of Open XML.</summary>
public interface IWordDocumentService
{
    Task<WordDocumentInfo> GetInfoAsync(Stream source, CancellationToken cancellationToken = default);

    Task<byte[]> SetInfoAsync(
        Stream source,
        WordInfoUpdate update,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListMergeFieldsAsync(
        Stream source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mail-merge style: fill MERGEFIELD / «Name» / {{Name}} placeholders.
    /// </summary>
    Task<byte[]> MergeFieldsAsync(
        Stream source,
        IReadOnlyDictionary<string, string> values,
        WordMergeOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReplaceTextAsync(
        Stream source,
        string find,
        string replace,
        CancellationToken cancellationToken = default);

    Task<byte[]> AppendAsync(
        Stream source,
        Stream appendix,
        CancellationToken cancellationToken = default);

    Task<string> ExtractTextAsync(Stream source, CancellationToken cancellationToken = default);

    Task<byte[]> CreateFromTextAsync(
        string text,
        WordCreateOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed record WordDocumentInfo(
    string? Title,
    string? Author,
    string? Subject,
    string? Creator,
    int ParagraphCount,
    int TableCount,
    IReadOnlyList<string> MergeFields);

public sealed record WordInfoUpdate(
    string? Title = null,
    string? Author = null,
    string? Subject = null);

public sealed record WordMergeOptions(
    bool RemoveUnusedFields = true,
    bool AlsoReplaceMustache = true);

public sealed record WordCreateOptions(string? Title = null, string? Author = null);
