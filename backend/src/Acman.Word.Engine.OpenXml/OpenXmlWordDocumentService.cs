using System.Text;
using System.Text.RegularExpressions;
using Acman.Word.Abstractions.Contracts;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace Acman.Word.Engine.OpenXml;

public sealed partial class OpenXmlWordDocumentService : IWordDocumentService
{
    private readonly ILogger<OpenXmlWordDocumentService> _logger;

    public OpenXmlWordDocumentService(ILogger<OpenXmlWordDocumentService> logger) => _logger = logger;

    public Task<WordDocumentInfo> GetInfoAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var doc = Open(source, false);
        var props = doc.PackageProperties;
        var body = doc.MainDocumentPart?.Document?.Body;
        var mergeFields = CollectMergeFieldNames(doc);
        return Task.FromResult(new WordDocumentInfo(
            props.Title,
            props.Creator,
            props.Subject,
            props.LastModifiedBy,
            body?.Elements<Paragraph>().Count() ?? 0,
            body?.Descendants<Table>().Count() ?? 0,
            mergeFields));
    }

    public Task<byte[]> SetInfoAsync(
        Stream source,
        WordInfoUpdate update,
        CancellationToken cancellationToken = default)
    {
        using var ms = CopyToMemory(source);
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var props = doc.PackageProperties;
            if (update.Title is not null) props.Title = update.Title;
            if (update.Author is not null) props.Creator = update.Author;
            if (update.Subject is not null) props.Subject = update.Subject;
            doc.MainDocumentPart?.Document?.Save();
        }

        return Task.FromResult(ms.ToArray());
    }

    public Task<IReadOnlyList<string>> ListMergeFieldsAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        using var doc = Open(source, false);
        return Task.FromResult(CollectMergeFieldNames(doc));
    }

    public Task<byte[]> MergeFieldsAsync(
        Stream source,
        IReadOnlyDictionary<string, string> values,
        WordMergeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WordMergeOptions();
        using var ms = CopyToMemory(source);
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart?.Document?.Body
                       ?? throw new InvalidOperationException("DOCX has no body.");

            // 1) Classic Word MERGEFIELD complex fields
            ReplaceMergeFields(body, values, options.RemoveUnusedFields);

            // 2) Plain placeholders «Name» and {{Name}}
            if (options.AlsoReplaceMustache)
            {
                foreach (var text in body.Descendants<Text>().ToList())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(text.Text)) continue;
                    var updated = text.Text;
                    foreach (var (key, value) in values)
                    {
                        updated = updated
                            .Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase)
                            .Replace($"«{key}»", value, StringComparison.OrdinalIgnoreCase)
                            .Replace($"<<{key}>>", value, StringComparison.OrdinalIgnoreCase);
                    }

                    text.Text = updated;
                }
            }

            doc.MainDocumentPart!.Document.Save();
        }

        _logger.LogInformation("Merged {Count} Word field value(s)", values.Count);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> ReplaceTextAsync(
        Stream source,
        string find,
        string replace,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(find))
        {
            throw new ArgumentException("Find text is required.", nameof(find));
        }

        using var ms = CopyToMemory(source);
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart?.Document?.Body
                       ?? throw new InvalidOperationException("DOCX has no body.");
            foreach (var text in body.Descendants<Text>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (text.Text?.Contains(find, StringComparison.Ordinal) == true)
                {
                    text.Text = text.Text.Replace(find, replace, StringComparison.Ordinal);
                }
            }

            doc.MainDocumentPart!.Document.Save();
        }

        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> AppendAsync(
        Stream source,
        Stream appendix,
        CancellationToken cancellationToken = default)
    {
        using var ms = CopyToMemory(source);
        using var appendixMs = CopyToMemory(appendix);
        using (var doc = WordprocessingDocument.Open(ms, true))
        using (var other = WordprocessingDocument.Open(appendixMs, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body
                       ?? throw new InvalidOperationException("DOCX has no body.");
            var otherBody = other.MainDocumentPart?.Document?.Body
                            ?? throw new InvalidOperationException("Appendix DOCX has no body.");

            body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            foreach (var element in otherBody.Elements().Select(e => e.CloneNode(true)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                body.AppendChild(element);
            }

            doc.MainDocumentPart!.Document.Save();
        }

        _logger.LogInformation("Appended Word document");
        return Task.FromResult(ms.ToArray());
    }

    public Task<string> ExtractTextAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var doc = Open(source, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return Task.FromResult(string.Empty);

        var sb = new StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = string.Concat(para.Descendants<Text>().Select(t => t.Text));
            sb.AppendLine(line);
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    public Task<byte[]> CreateFromTextAsync(
        string text,
        WordCreateOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WordCreateOptions();
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;
            foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
            {
                cancellationToken.ThrowIfCancellationRequested();
                body.AppendChild(new Paragraph(new Run(new Text(line))));
            }

            if (!string.IsNullOrWhiteSpace(options.Title)) doc.PackageProperties.Title = options.Title;
            if (!string.IsNullOrWhiteSpace(options.Author)) doc.PackageProperties.Creator = options.Author;
            main.Document.Save();
        }

        return Task.FromResult(ms.ToArray());
    }

    private static void ReplaceMergeFields(
        Body body,
        IReadOnlyDictionary<string, string> values,
        bool removeUnused)
    {
        // Walk simple instruction texts: MERGEFIELD Name
        foreach (var instr in body.Descendants<FieldCode>().ToList())
        {
            var code = instr.Text?.Trim() ?? string.Empty;
            var match = MergeFieldInstrRegex().Match(code);
            if (!match.Success) continue;

            var name = match.Groups[1].Value.Trim();
            if (!values.TryGetValue(name, out var value))
            {
                // case-insensitive lookup
                value = values.FirstOrDefault(kv =>
                    string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
            }

            if (value is null)
            {
                if (removeUnused)
                {
                    // leave field as-is
                }

                continue;
            }

            // Replace the whole complex field run sequence when possible; else patch instruction area text.
            var run = instr.Ancestors<Run>().FirstOrDefault();
            if (run?.Parent is not null)
            {
                // Find field result text after SEPARATE
                var container = run.Parent;
                var runs = container.Elements<Run>().ToList();
                var idx = runs.IndexOf(run);
                for (var i = idx; i < runs.Count; i++)
                {
                    var fld = runs[i].Elements<FieldChar>().FirstOrDefault();
                    if (fld?.FieldCharType?.Value == FieldCharValues.Separate)
                    {
                        // next text run is result
                        for (var j = i + 1; j < runs.Count; j++)
                        {
                            var end = runs[j].Elements<FieldChar>().FirstOrDefault();
                            if (end?.FieldCharType?.Value == FieldCharValues.End) break;
                            var t = runs[j].GetFirstChild<Text>();
                            if (t is not null)
                            {
                                t.Text = value;
                                break;
                            }
                        }

                        break;
                    }
                }
            }
        }

        // Also replace display text that still shows «Name»
        foreach (var text in body.Descendants<Text>())
        {
            if (string.IsNullOrEmpty(text.Text)) continue;
            foreach (var (key, value) in values)
            {
                text.Text = text.Text
                    .Replace($"«{key}»", value, StringComparison.OrdinalIgnoreCase)
                    .Replace($"MERGEFIELD {key}", value, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static IReadOnlyList<string> CollectMergeFieldNames(WordprocessingDocument doc)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return [];

        foreach (var instr in body.Descendants<FieldCode>())
        {
            var match = MergeFieldInstrRegex().Match(instr.Text ?? string.Empty);
            if (match.Success) names.Add(match.Groups[1].Value.Trim());
        }

        foreach (var text in body.Descendants<Text>())
        {
            if (string.IsNullOrEmpty(text.Text)) continue;
            foreach (Match m in MustacheRegex().Matches(text.Text))
            {
                names.Add(m.Groups[1].Value);
            }

            foreach (Match m in GuillemetRegex().Matches(text.Text))
            {
                names.Add(m.Groups[1].Value);
            }
        }

        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static WordprocessingDocument Open(Stream source, bool editable)
    {
        var ms = CopyToMemory(source);
        return WordprocessingDocument.Open(ms, editable);
    }

    private static MemoryStream CopyToMemory(Stream source)
    {
        if (source.CanSeek) source.Position = 0;
        var ms = new MemoryStream();
        source.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    [GeneratedRegex(@"MERGEFIELD\s+([^\s\\]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MergeFieldInstrRegex();

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}")]
    private static partial Regex MustacheRegex();

    [GeneratedRegex(@"«\s*([A-Za-z0-9_.-]+)\s*»")]
    private static partial Regex GuillemetRegex();
}
