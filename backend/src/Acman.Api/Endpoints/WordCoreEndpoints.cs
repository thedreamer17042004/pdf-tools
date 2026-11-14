using System.Text.Json;
using Acman.Word.Abstractions.Contracts;

namespace Acman.Api.Endpoints;

/// <summary>
/// Sync Word/DOCX core API (Aspose.Words-inspired, Open XML engine).
/// </summary>
public static class WordCoreEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEndpointRouteBuilder MapWordCoreEndpoints(this IEndpointRouteBuilder app)
    {
        var core = app.MapGroup("/api/v1/word/core").WithTags("WordCore");

        core.MapGet("/", () => Results.Ok(new
        {
            name = "Nam-Dev Word Core",
            inspiredBy = "Aspose.Words (Open XML SDK + Nam-Dev PDF for DOCX→PDF)",
            auth = "JWT Bearer required",
            capabilities = new
            {
                document = new[] { "info", "info/set", "create", "extract-text", "replace-text", "append" },
                mailMerge = new[] { "merge-fields/list", "merge-fields" },
                convert = new[] { "to-pdf" },
                notes = new[]
                {
                    "Merge fields support MERGEFIELD, «Name», {{Name}}",
                    "DOCX→PDF uses Nam-Dev Open XML → PdfSharp engine (no LibreOffice)"
                }
            }
        })).AllowAnonymous();

        core.MapPost("/info", GetInfoAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/info/set", SetInfoAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/create", CreateAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/extract-text", ExtractTextAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/replace-text", ReplaceTextAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/append", AppendAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/merge-fields/list", ListMergeFieldsAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/merge-fields", MergeFieldsAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/to-pdf", ToPdfAsync).DisableAntiforgery().RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetInfoAsync(HttpRequest request, IWordDocumentService words, CancellationToken ct)
    {
        await using var file = await RequireDocxAsync(request, ct);
        return Results.Ok(await words.GetInfoAsync(file, ct));
    }

    private static async Task<IResult> SetInfoAsync(HttpRequest request, IWordDocumentService words, CancellationToken ct)
    {
        var (file, json) = await RequireDocxAndJsonAsync(request, ct);
        await using (file)
        {
            var update = JsonSerializer.Deserialize<WordInfoUpdate>(json, JsonOptions) ?? new WordInfoUpdate();
            var bytes = await words.SetInfoAsync(file, update, ct);
            return DocxFile(bytes, "info.docx");
        }
    }

    private static async Task<IResult> CreateAsync(HttpRequest request, IWordDocumentService words, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var text = form["text"].ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Results.BadRequest(new { error = new { message = "form field 'text' required" } });
        }

        var title = form["title"].ToString();
        var author = form["author"].ToString();
        var bytes = await words.CreateFromTextAsync(
            text,
            new WordCreateOptions(
                string.IsNullOrWhiteSpace(title) ? null : title,
                string.IsNullOrWhiteSpace(author) ? null : author),
            ct);
        return DocxFile(bytes, "created.docx");
    }

    private static async Task<IResult> ExtractTextAsync(HttpRequest request, IWordDocumentService words, CancellationToken ct)
    {
        await using var file = await RequireDocxAsync(request, ct);
        var text = await words.ExtractTextAsync(file, ct);
        return Results.Ok(new { text });
    }

    private static async Task<IResult> ReplaceTextAsync(HttpRequest request, IWordDocumentService words, CancellationToken ct)
    {
        var (file, json) = await RequireDocxAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<ReplaceTextBody>(json, JsonOptions)
                       ?? throw new BadHttpRequestException("Invalid options JSON");
            var bytes = await words.ReplaceTextAsync(file, body.Find, body.Replace ?? string.Empty, ct);
            return DocxFile(bytes, "replaced.docx");
        }
    }

    private static async Task<IResult> AppendAsync(HttpRequest request, IWordDocumentService words, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        var appendix = form.Files.GetFile("appendix") ?? form.Files.Skip(1).FirstOrDefault();
        if (file is null || appendix is null)
        {
            return Results.BadRequest(new { error = new { message = "multipart 'file' and 'appendix' required" } });
        }

        await using var a = file.OpenReadStream();
        await using var b = appendix.OpenReadStream();
        var bytes = await words.AppendAsync(a, b, ct);
        return DocxFile(bytes, "appended.docx");
    }

    private static async Task<IResult> ListMergeFieldsAsync(HttpRequest request, IWordDocumentService words, CancellationToken ct)
    {
        await using var file = await RequireDocxAsync(request, ct);
        var fields = await words.ListMergeFieldsAsync(file, ct);
        return Results.Ok(new { items = fields });
    }

    private static async Task<IResult> MergeFieldsAsync(HttpRequest request, IWordDocumentService words, CancellationToken ct)
    {
        var (file, json) = await RequireDocxAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<MergeFieldsBody>(json, JsonOptions)
                       ?? throw new BadHttpRequestException("Invalid options JSON");
            var bytes = await words.MergeFieldsAsync(
                file,
                body.Values,
                new WordMergeOptions(body.RemoveUnusedFields ?? true, body.AlsoReplaceMustache ?? true),
                ct);
            return DocxFile(bytes, "merged.docx");
        }
    }

    private static async Task<IResult> ToPdfAsync(HttpRequest request, IWordToPdfConverter converter, CancellationToken ct)
    {
        await using var file = await RequireDocxAsync(request, ct);
        var pdf = await converter.ConvertAsync(file, new WordToPdfOptions(), ct);
        return Results.File(pdf, "application/pdf", "from-word.pdf");
    }

    private static IResult DocxFile(byte[] bytes, string name) =>
        Results.File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", name);

    private static async Task<IFormCollection> RequireFormAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            throw new BadHttpRequestException("multipart/form-data required");
        }

        return await request.ReadFormAsync(ct);
    }

    private static async Task<MemoryStream> RequireDocxAsync(HttpRequest request, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            throw new BadHttpRequestException("multipart file field 'file' is required");
        }

        var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    private static async Task<(MemoryStream File, string Json)> RequireDocxAndJsonAsync(
        HttpRequest request,
        CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            throw new BadHttpRequestException("multipart file field 'file' is required");
        }

        var json = form["options"].ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new BadHttpRequestException("form field 'options' (JSON) is required");
        }

        var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        return (ms, json);
    }

    private sealed record ReplaceTextBody(string Find, string? Replace);
    private sealed record MergeFieldsBody(
        Dictionary<string, string> Values,
        bool? RemoveUnusedFields,
        bool? AlsoReplaceMustache);
}
