using System.IO.Compression;
using System.Text.Json;
using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Models;

namespace Acman.Api.Endpoints;

/// <summary>
/// Sync byte-in / byte-out PDF core API (Aspose-style facade).
/// Other services call these with multipart PDF files — no document library required.
/// </summary>
public static class PdfCoreEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEndpointRouteBuilder MapPdfCoreEndpoints(this IEndpointRouteBuilder app)
    {
        var core = app.MapGroup("/api/v1/pdf/core").WithTags("PdfCore");

        core.MapGet("/", () => Results.Ok(new
        {
            name = "Nam-Dev PDF Core",
            inspiredBy = "Aspose.PDF key features (MIT stack: PDFsharp + PDFium)",
            auth = "JWT Bearer required",
            surface = new
            {
                sync = "/api/v1/pdf/core/* (multipart file in → PDF/JSON out)",
                jobs = "/api/v1/pdf/* (documentId jobs for library workflow)",
                documents = "/api/v1/documents/{id}/..."
            },
            capabilities = CapabilityCatalog()
        })).AllowAnonymous();

        // —— Document info ——
        core.MapPost("/info", GetInfoAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/info/set", SetInfoAsync).DisableAntiforgery().RequireAuthorization();

        // —— Pages / assemble ——
        core.MapPost("/merge", MergeAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/split", SplitAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/rotate", RotateAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/organize", OrganizeAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/extract-pages", ExtractPagesAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/delete-pages", DeletePagesAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/reverse", ReverseAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/insert-blank", InsertBlankAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/insert-pages", InsertPagesAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/append", AppendAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/crop", CropAsync).DisableAntiforgery().RequireAuthorization();

        // —— Text / stamp / watermark ——
        core.MapPost("/add-text", AddTextAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/header-footer", HeaderFooterAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/watermark", WatermarkAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/page-numbers", PageNumbersAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/stamp-image", StampImageAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/redact-cover", RedactCoverAsync).DisableAntiforgery().RequireAuthorization();

        // —— Security / optimize ——
        core.MapPost("/protect", ProtectAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/unlock", UnlockAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/compress", CompressAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/repair", RepairAsync).DisableAntiforgery().RequireAuthorization();

        // —— Forms (merge fields) ——
        core.MapPost("/forms/fields", ListFieldsAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/forms/values", ExportValuesAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/forms/fill", FillFieldsAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/forms/merge-fields", MergeFieldsAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/forms/flatten", FlattenFormsAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/forms/remove-fields", RemoveFieldsAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/forms/add-fields", AddFieldsAsync).DisableAntiforgery().RequireAuthorization();

        // —— Bookmarks ——
        core.MapPost("/bookmarks", ListBookmarksAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/bookmarks/set", SetBookmarksAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/bookmarks/clear", ClearBookmarksAsync).DisableAntiforgery().RequireAuthorization();

        // —— Text extract / images / iLovePDF gaps ——
        core.MapPost("/extract-text", ExtractTextAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/images-to-pdf", ImagesToPdfAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/to-markdown", ToMarkdownAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/html-to-pdf", HtmlToPdfAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/compare", CompareAsync).DisableAntiforgery().RequireAuthorization();
        core.MapPost("/sign-stamp", SignStampAsync).DisableAntiforgery().RequireAuthorization();

        return app;
    }

    private static object CapabilityCatalog() => new
    {
        document = new[] { "info", "info/set", "compress", "repair" },
        pages = new[]
        {
            "merge", "split", "rotate", "organize", "extract-pages", "delete-pages", "reverse",
            "insert-blank", "insert-pages", "append", "crop"
        },
        textStamp = new[] { "add-text", "header-footer", "watermark", "page-numbers", "stamp-image", "redact-cover", "sign-stamp" },
        security = new[] { "protect", "unlock" },
        forms = new[] { "forms/fields", "forms/values", "forms/fill", "forms/merge-fields", "forms/flatten", "forms/remove-fields", "forms/add-fields" },
        bookmarks = new[] { "bookmarks", "bookmarks/set", "bookmarks/clear" },
        convert = new[] { "extract-text", "images-to-pdf", "to-markdown", "html-to-pdf", "to-images (job: /api/v1/pdf/to-images)" },
        compare = new[] { "compare" },
        deferred = new[] { "OCR", "PAdES/HSM signatures", "true content redaction", "PDF/A conversion", "Scan-to-PDF (use images-to-pdf)", "Word tools: /api/v1/word/core" }
    };

    // —— Handlers ——

    private static async Task<IResult> GetInfoAsync(
        HttpRequest request, IPdfDocumentInfoService info, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var result = await info.GetInfoAsync(file, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SetInfoAsync(
        HttpRequest request, IPdfDocumentInfoService info, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var update = JsonSerializer.Deserialize<PdfInfoUpdate>(json, JsonOptions)
                         ?? new PdfInfoUpdate();
            var bytes = await info.SetInfoAsync(file, update, ct);
            return PdfFile(bytes, "info.pdf");
        }
    }

    private static async Task<IResult> MergeAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var files = await RequirePdfsAsync(request, minCount: 2, ct);
        try
        {
            var streams = files.Select(f => (Stream)f).ToList();
            var bytes = await tools.MergeAsync(streams, ct);
            return PdfFile(bytes, "merged.pdf");
        }
        finally
        {
            foreach (var f in files) await f.DisposeAsync();
        }
    }

    private static async Task<IResult> SplitAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct, jsonOptional: true);
        await using (file)
        {
            List<PageRange>? ranges = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var body = JsonSerializer.Deserialize<SplitCoreBody>(json, JsonOptions);
                ranges = body?.Ranges?.Select(r => new PageRange(r.From, r.To)).ToList();
            }

            var parts = await tools.SplitAsync(file, ranges, ct);
            return ZipPdfs(parts, "split");
        }
    }

    private static async Task<IResult> RotateAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<RotateCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.RotateAsync(file, body.Degrees, body.PageIndexes, ct);
            return PdfFile(bytes, "rotated.pdf");
        }
    }

    private static async Task<IResult> OrganizeAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<OrganizeCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.OrganizeAsync(file, body.PageOrder, ct);
            return PdfFile(bytes, "organized.pdf");
        }
    }

    private static async Task<IResult> ExtractPagesAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<PagesCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.ExtractPagesAsync(file, body.PageIndexes, ct);
            return PdfFile(bytes, "extract.pdf");
        }
    }

    private static async Task<IResult> DeletePagesAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<PagesCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.DeletePagesAsync(file, body.PageIndexes, ct);
            return PdfFile(bytes, "deleted.pdf");
        }
    }

    private static async Task<IResult> ReverseAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var bytes = await tools.ReversePagesAsync(file, ct);
        return PdfFile(bytes, "reversed.pdf");
    }

    private static async Task<IResult> InsertBlankAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<InsertBlankCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.InsertBlankPagesAsync(
                file, body.AtIndex, body.Count ?? 1, body.WidthPoints, body.HeightPoints, ct);
            return PdfFile(bytes, "insert-blank.pdf");
        }
    }

    private static async Task<IResult> InsertPagesAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var baseFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        var insertFile = form.Files.GetFile("insert") ?? form.Files.Skip(1).FirstOrDefault();
        if (baseFile is null || insertFile is null)
        {
            return Results.BadRequest(new { error = new { message = "multipart fields 'file' and 'insert' required" } });
        }

        var json = form["options"].ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            return Results.BadRequest(new { error = new { message = "form field 'options' (JSON) required" } });
        }

        var body = JsonSerializer.Deserialize<InsertPagesCoreBody>(json, JsonOptions)
                   ?? throw BadJson();
        await using var baseStream = baseFile.OpenReadStream();
        await using var insertStream = insertFile.OpenReadStream();
        var bytes = await tools.InsertPagesAsync(baseStream, insertStream, body.AtIndex, body.PageIndexes, ct);
        return PdfFile(bytes, "insert-pages.pdf");
    }

    private static async Task<IResult> AppendAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var baseFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        var appendix = form.Files.GetFile("appendix") ?? form.Files.Skip(1).FirstOrDefault();
        if (baseFile is null || appendix is null)
        {
            return Results.BadRequest(new { error = new { message = "multipart fields 'file' and 'appendix' required" } });
        }

        await using var a = baseFile.OpenReadStream();
        await using var b = appendix.OpenReadStream();
        var bytes = await tools.AppendAsync(a, b, ct);
        return PdfFile(bytes, "appended.pdf");
    }

    private static async Task<IResult> CropAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<CropCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.CropPagesAsync(
                file,
                new CropOptions(body.LeftPoints, body.BottomPoints, body.RightPoints, body.TopPoints, body.PageIndexes),
                ct);
            return PdfFile(bytes, "cropped.pdf");
        }
    }

    private static async Task<IResult> AddTextAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<AddTextCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.AddTextAsync(
                file,
                new AddTextOptions(body.Text, body.XPoints, body.YPoints, body.PageIndex, body.FontSize ?? 12, body.ColorHex ?? "#0F172A"),
                ct);
            return PdfFile(bytes, "add-text.pdf");
        }
    }

    private static async Task<IResult> HeaderFooterAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<HeaderFooterCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.HeaderFooterAsync(
                file,
                new HeaderFooterOptions(body.Header, body.Footer, body.FontSize ?? 10, body.ColorHex ?? "#64748B", body.MarginPoints ?? 28),
                ct);
            return PdfFile(bytes, "header-footer.pdf");
        }
    }

    private static async Task<IResult> WatermarkAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<WatermarkCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.WatermarkAsync(
                file,
                new WatermarkOptions(body.Text ?? "Nam-Dev", body.Opacity ?? 0.25, body.FontSize ?? 48, body.ColorHex ?? "#64748B", body.AngleDegrees ?? -35),
                ct);
            return PdfFile(bytes, "watermark.pdf");
        }
    }

    private static async Task<IResult> PageNumbersAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct, jsonOptional: true);
        await using (file)
        {
            PageNumberOptions? options = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var body = JsonSerializer.Deserialize<PageNumbersCoreBody>(json, JsonOptions);
                if (body is not null)
                {
                    options = new PageNumberOptions(body.Format ?? "{n} / {total}", body.FontSize ?? 10, body.ColorHex ?? "#64748B");
                }
            }

            var bytes = await tools.AddPageNumbersAsync(file, options, ct);
            return PdfFile(bytes, "page-numbers.pdf");
        }
    }

    private static async Task<IResult> StampImageAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var pdf = form.Files.GetFile("file") ?? form.Files.FirstOrDefault(f => f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        var image = form.Files.GetFile("image") ?? form.Files.FirstOrDefault(f => f != pdf);
        if (pdf is null || image is null)
        {
            return Results.BadRequest(new { error = new { message = "multipart 'file' (pdf) and 'image' required" } });
        }

        var json = form["options"].ToString();
        var body = JsonSerializer.Deserialize<StampImageCoreBody>(json, JsonOptions)
                   ?? throw BadJson();
        await using var pdfStream = pdf.OpenReadStream();
        await using var imgMs = new MemoryStream();
        await image.CopyToAsync(imgMs, ct);
        var bytes = await tools.StampImageAsync(
            pdfStream,
            new StampImageOptions(
                imgMs.ToArray(),
                body.XPoints,
                body.YPoints,
                body.WidthPoints,
                body.HeightPoints,
                body.PageIndex,
                body.Opacity ?? 1),
            ct);
        return PdfFile(bytes, "stamp-image.pdf");
    }

    private static async Task<IResult> RedactCoverAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<RedactCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var regions = body.Regions.Select(r =>
                new RedactRegion(r.PageIndex, r.XPoints, r.YPoints, r.WidthPoints, r.HeightPoints, r.ColorHex ?? "#000000")).ToList();
            var bytes = await tools.RedactCoverAsync(file, regions, ct);
            return PdfFile(bytes, "redacted.pdf");
        }
    }

    private static async Task<IResult> ProtectAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<ProtectCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.ProtectAsync(file, new ProtectOptions(body.UserPassword, body.OwnerPassword), ct);
            return PdfFile(bytes, "protected.pdf");
        }
    }

    private static async Task<IResult> UnlockAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<UnlockCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await tools.UnlockAsync(file, body.UserPassword, ct);
            return PdfFile(bytes, "unlocked.pdf");
        }
    }

    private static async Task<IResult> CompressAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct, jsonOptional: true);
        await using (file)
        {
            var removeMeta = true;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var body = JsonSerializer.Deserialize<CompressCoreBody>(json, JsonOptions);
                removeMeta = body?.RemoveMetadata ?? true;
            }

            var result = await tools.CompressAsync(file, new CompressOptions(removeMeta), ct);
            return Results.File(result.Bytes, "application/pdf", "compressed.pdf");
        }
    }

    private static async Task<IResult> ListFieldsAsync(
        HttpRequest request, IPdfFormService forms, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var fields = await forms.ListFieldsAsync(file, ct);
        return Results.Ok(new { items = fields });
    }

    private static async Task<IResult> ExportValuesAsync(
        HttpRequest request, IPdfFormService forms, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var values = await forms.ExportValuesAsync(file, ct);
        return Results.Ok(new { values });
    }

    private static async Task<IResult> FillFieldsAsync(
        HttpRequest request, IPdfFormService forms, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<MergeFieldsCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await forms.FillFieldsAsync(file, body.Values, ct);
            return PdfFile(bytes, "filled.pdf");
        }
    }

    private static async Task<IResult> MergeFieldsAsync(
        HttpRequest request, IPdfFormService forms, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<MergeFieldsCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await forms.MergeFieldsAsync(
                file,
                body.Values,
                new MergeFieldsOptions(body.Flatten ?? false),
                ct);
            return PdfFile(bytes, "merged-fields.pdf");
        }
    }

    private static async Task<IResult> FlattenFormsAsync(
        HttpRequest request, IPdfFormService forms, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var bytes = await forms.FlattenAsync(file, ct);
        return PdfFile(bytes, "flattened.pdf");
    }

    private static async Task<IResult> RemoveFieldsAsync(
        HttpRequest request, IPdfFormService forms, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct, jsonOptional: true);
        await using (file)
        {
            List<string>? names = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var body = JsonSerializer.Deserialize<RemoveFieldsCoreBody>(json, JsonOptions);
                names = body?.FieldNames;
            }

            var bytes = await forms.RemoveFieldsAsync(file, names, ct);
            return PdfFile(bytes, "remove-fields.pdf");
        }
    }

    private static async Task<IResult> ListBookmarksAsync(
        HttpRequest request, IPdfBookmarkService bookmarks, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var items = await bookmarks.ListAsync(file, ct);
        return Results.Ok(new { items });
    }

    private static async Task<IResult> SetBookmarksAsync(
        HttpRequest request, IPdfBookmarkService bookmarks, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<SetBookmarksCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await bookmarks.SetAsync(file, body.Bookmarks, ct);
            return PdfFile(bytes, "bookmarks.pdf");
        }
    }

    private static async Task<IResult> ClearBookmarksAsync(
        HttpRequest request, IPdfBookmarkService bookmarks, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var bytes = await bookmarks.ClearAsync(file, ct);
        return PdfFile(bytes, "bookmarks-cleared.pdf");
    }

    private static async Task<IResult> ExtractTextAsync(
        HttpRequest request, IPdfTextExtractor text, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var extracted = await text.ExtractAsync(file, cancellationToken: ct);
        return Results.Ok(extracted);
    }

    private static async Task<IResult> ImagesToPdfAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var images = new List<ImagePageInput>();
        foreach (var file in form.Files.Where(f => f.Length > 0))
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            images.Add(new ImagePageInput(ms.ToArray(), file.ContentType));
        }

        if (images.Count == 0)
        {
            return Results.BadRequest(new { error = new { message = "At least one image file is required." } });
        }

        var pdf = await tools.ImagesToPdfAsync(images, ct);
        return PdfFile(pdf, "images.pdf");
    }

    private static async Task<IResult> RepairAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var bytes = await tools.RepairAsync(file, ct);
        return PdfFile(bytes, "repaired.pdf");
    }

    private static async Task<IResult> ToMarkdownAsync(
        HttpRequest request, IPdfMarkdownConverter markdown, CancellationToken ct)
    {
        await using var file = await RequirePdfAsync(request, ct);
        var md = await markdown.ToMarkdownAsync(file, ct);
        return Results.Text(md, "text/markdown; charset=utf-8");
    }

    private static async Task<IResult> HtmlToPdfAsync(
        HttpRequest request, IPdfHtmlConverter html, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var htmlText = form["html"].ToString();
        if (string.IsNullOrWhiteSpace(htmlText))
        {
            var upload = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (upload is not null)
            {
                using var reader = new StreamReader(upload.OpenReadStream());
                htmlText = await reader.ReadToEndAsync(ct);
            }
        }

        if (string.IsNullOrWhiteSpace(htmlText))
        {
            return Results.BadRequest(new { error = new { message = "form field 'html' (or html file) required" } });
        }

        var title = form["title"].ToString();
        var bytes = await html.FromHtmlAsync(htmlText, new HtmlToPdfOptions(string.IsNullOrWhiteSpace(title) ? null : title), ct);
        return PdfFile(bytes, "from-html.pdf");
    }

    private static async Task<IResult> CompareAsync(
        HttpRequest request, IPdfCompareService compare, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var left = form.Files.GetFile("left") ?? form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        var right = form.Files.GetFile("right") ?? form.Files.Skip(1).FirstOrDefault();
        if (left is null || right is null)
        {
            return Results.BadRequest(new { error = new { message = "multipart 'left' and 'right' PDF files required" } });
        }

        await using var a = left.OpenReadStream();
        await using var b = right.OpenReadStream();
        var result = await compare.CompareAsync(a, b, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SignStampAsync(
        HttpRequest request, IPdfManipulationService tools, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var pdf = form.Files.GetFile("file") ?? form.Files.FirstOrDefault(f => f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        if (pdf is null)
        {
            return Results.BadRequest(new { error = new { message = "multipart 'file' (pdf) required" } });
        }

        var json = form["options"].ToString();
        var body = JsonSerializer.Deserialize<SignStampCoreBody>(json, JsonOptions)
                   ?? throw BadJson();

        byte[]? imageBytes = null;
        var image = form.Files.GetFile("image") ?? form.Files.GetFile("signature");
        if (image is not null)
        {
            await using var imgMs = new MemoryStream();
            await image.CopyToAsync(imgMs, ct);
            imageBytes = imgMs.ToArray();
        }

        await using var pdfStream = pdf.OpenReadStream();
        var bytes = await tools.SignStampAsync(
            pdfStream,
            new SignStampOptions(
                body.PageIndex,
                body.XPoints,
                body.YPoints,
                body.SignerName,
                imageBytes,
                body.WidthPoints ?? 160,
                body.HeightPoints ?? 60,
                body.FontSize ?? 14,
                body.ColorHex ?? "#1D4ED8"),
            ct);
        return PdfFile(bytes, "signed.pdf");
    }

    private static async Task<IResult> AddFieldsAsync(
        HttpRequest request, IPdfFormService forms, CancellationToken ct)
    {
        var (file, json) = await RequirePdfAndJsonAsync(request, ct);
        await using (file)
        {
            var body = JsonSerializer.Deserialize<AddFieldsCoreBody>(json, JsonOptions)
                       ?? throw BadJson();
            var bytes = await forms.AddFieldsAsync(file, body.Fields, ct);
            return PdfFile(bytes, "form-fields.pdf");
        }
    }

    // —— helpers ——

    private static IResult PdfFile(byte[] bytes, string fileName) =>
        Results.File(bytes, "application/pdf", fileName);

    private static IResult ZipPdfs(IReadOnlyList<byte[]> parts, string prefix)
    {
        using var zipMs = new MemoryStream();
        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < parts.Count; i++)
            {
                var entry = zip.CreateEntry($"{prefix}-{i + 1}.pdf", CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(parts[i]);
            }
        }

        return Results.File(zipMs.ToArray(), "application/zip", $"{prefix}.zip");
    }

    private static Exception BadJson() =>
        new BadHttpRequestException("Invalid JSON in form field 'options'.");

    private static async Task<IFormCollection> RequireFormAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            throw new BadHttpRequestException("multipart/form-data required");
        }

        return await request.ReadFormAsync(ct);
    }

    private static async Task<MemoryStream> RequirePdfAsync(HttpRequest request, CancellationToken ct)
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

    private static async Task<List<MemoryStream>> RequirePdfsAsync(HttpRequest request, int minCount, CancellationToken ct)
    {
        var form = await RequireFormAsync(request, ct);
        var list = new List<MemoryStream>();
        foreach (var file in form.Files.Where(f => f.Length > 0))
        {
            var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            list.Add(ms);
        }

        if (list.Count < minCount)
        {
            foreach (var ms in list) await ms.DisposeAsync();
            throw new BadHttpRequestException($"At least {minCount} PDF file(s) required");
        }

        return list;
    }

    private static async Task<(MemoryStream File, string Json)> RequirePdfAndJsonAsync(
        HttpRequest request,
        CancellationToken ct,
        bool jsonOptional = false)
    {
        var form = await RequireFormAsync(request, ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            throw new BadHttpRequestException("multipart file field 'file' is required");
        }

        var json = form["options"].ToString();
        if (!jsonOptional && string.IsNullOrWhiteSpace(json))
        {
            throw new BadHttpRequestException("form field 'options' (JSON) is required");
        }

        var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        return (ms, json);
    }

    // —— request bodies ——

    private sealed record SplitCoreBody(List<SplitRangeDto>? Ranges);
    private sealed record SplitRangeDto(int From, int To);
    private sealed record RotateCoreBody(int Degrees, List<int>? PageIndexes);
    private sealed record OrganizeCoreBody(List<int> PageOrder);
    private sealed record PagesCoreBody(List<int> PageIndexes);
    private sealed record InsertBlankCoreBody(int AtIndex, int? Count, double? WidthPoints, double? HeightPoints);
    private sealed record InsertPagesCoreBody(int AtIndex, List<int>? PageIndexes);
    private sealed record CropCoreBody(double LeftPoints, double BottomPoints, double RightPoints, double TopPoints, List<int>? PageIndexes);
    private sealed record AddTextCoreBody(string Text, double XPoints, double YPoints, int? PageIndex, double? FontSize, string? ColorHex);
    private sealed record HeaderFooterCoreBody(string? Header, string? Footer, double? FontSize, string? ColorHex, double? MarginPoints);
    private sealed record WatermarkCoreBody(string? Text, double? Opacity, double? FontSize, string? ColorHex, double? AngleDegrees);
    private sealed record PageNumbersCoreBody(string? Format, double? FontSize, string? ColorHex);
    private sealed record StampImageCoreBody(double XPoints, double YPoints, double WidthPoints, double HeightPoints, int? PageIndex, double? Opacity);
    private sealed record RedactCoreBody(List<RedactRegionDto> Regions);
    private sealed record RedactRegionDto(int PageIndex, double XPoints, double YPoints, double WidthPoints, double HeightPoints, string? ColorHex);
    private sealed record ProtectCoreBody(string UserPassword, string? OwnerPassword);
    private sealed record UnlockCoreBody(string UserPassword);
    private sealed record CompressCoreBody(bool? RemoveMetadata);
    private sealed record MergeFieldsCoreBody(Dictionary<string, string> Values, bool? Flatten);
    private sealed record RemoveFieldsCoreBody(List<string>? FieldNames);
    private sealed record SetBookmarksCoreBody(List<PdfBookmarkInput> Bookmarks);
    private sealed record SignStampCoreBody(
        int PageIndex,
        double XPoints,
        double YPoints,
        string? SignerName,
        double? WidthPoints,
        double? HeightPoints,
        double? FontSize,
        string? ColorHex);
    private sealed record AddFieldsCoreBody(List<PdfFormFieldCreate> Fields);
}
