using System.Security.Claims;
using FluentValidation;
using MediatR;
using Modules.Documents.Application;

namespace Acman.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents").WithTags("Documents");

        group.MapPost("/", UploadAsync).DisableAntiforgery().WithName("UploadDocument");
        group.MapGet("/mine", ListMineAsync).RequireAuthorization().WithName("ListMyDocuments");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetDocument");
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization().WithName("DeleteDocument");
        group.MapGet("/{id:guid}/pages", ListPagesAsync).WithName("ListDocumentPages");
        group.MapGet("/{id:guid}/render", RenderAsync).WithName("RenderDocumentPage");
        group.MapGet("/{id:guid}/download", DownloadAsync).WithName("DownloadDocument");
        group.MapGet("/{id:guid}/text", ExtractTextAsync).WithName("ExtractDocumentText");
        group.MapGet("/{id:guid}/form-fields", ListFormFieldsAsync).WithName("ListFormFields");
        group.MapPost("/{id:guid}/form-fields/fill", FillFormAsync).RequireAuthorization().WithName("FillFormFields");
        group.MapPost("/{id:guid}/form-fields/merge", MergeFormAsync).RequireAuthorization().WithName("MergeFormFields");
        group.MapPost("/{id:guid}/form-fields/flatten", FlattenFormAsync).RequireAuthorization().WithName("FlattenForm");

        return app;
    }

    private static Guid? TryGetUserId(HttpContext http)
    {
        var value = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? http.User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        IMediator mediator,
        IValidator<UploadDocumentCommand> validator,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = new { code = "validation_error", message = "multipart/form-data required." } });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = new { code = "validation_error", message = "file is required." } });
        }

        await using var stream = file.OpenReadStream();
        var command = new UploadDocumentCommand(
            file.FileName,
            file.ContentType,
            stream,
            file.Length,
            TryGetUserId(request.HttpContext));
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/v1/documents/{result.Document.Id}", result.Document);
    }

    private static async Task<IResult> ListMineAsync(HttpContext http, IMediator mediator, CancellationToken ct)
    {
        var userId = TryGetUserId(http) ?? throw new UnauthorizedAccessException();
        var items = await mediator.Send(new ListMyDocumentsQuery(userId), ct);
        return Results.Ok(new { items });
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext http, IMediator mediator, CancellationToken ct)
    {
        var doc = await mediator.Send(new GetDocumentQuery(id, TryGetUserId(http)), ct);
        return Results.Ok(doc);
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext http, IMediator mediator, CancellationToken ct)
    {
        var userId = TryGetUserId(http) ?? throw new UnauthorizedAccessException();
        await mediator.Send(new DeleteDocumentCommand(id, userId), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListPagesAsync(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var pages = await mediator.Send(new ListDocumentPagesQuery(id), cancellationToken);
        return Results.Ok(new { items = pages });
    }

    private static async Task<IResult> RenderAsync(
        Guid id,
        IMediator mediator,
        int page = 1,
        int dpi = 144,
        CancellationToken cancellationToken = default)
    {
        var rendered = await mediator.Send(new RenderDocumentPageQuery(id, page, dpi), cancellationToken);
        return Results.File(rendered.ImageBytes, rendered.ContentType);
    }

    private static async Task<IResult> DownloadAsync(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DownloadDocumentQuery(id), cancellationToken);
        return Results.File(result.Content, result.ContentType, result.FileName);
    }

    private static async Task<IResult> ExtractTextAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var text = await mediator.Send(new ExtractDocumentTextQuery(id), ct);
        return Results.Ok(text);
    }

    private static async Task<IResult> ListFormFieldsAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var fields = await mediator.Send(new ListFormFieldsQuery(id), ct);
        return Results.Ok(new { items = fields });
    }

    private static async Task<IResult> FillFormAsync(
        Guid id,
        FillFormRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var bytes = await mediator.Send(new FillFormCommand(id, body.Values), ct);
        return Results.File(bytes, "application/pdf", "filled.pdf");
    }

    private static async Task<IResult> MergeFormAsync(
        Guid id,
        MergeFormRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var bytes = await mediator.Send(new MergeFormCommand(id, body.Values, body.Flatten ?? false), ct);
        return Results.File(bytes, "application/pdf", "merged-fields.pdf");
    }

    private static async Task<IResult> FlattenFormAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var bytes = await mediator.Send(new FlattenFormCommand(id), ct);
        return Results.File(bytes, "application/pdf", "flattened.pdf");
    }

    public sealed record FillFormRequest(Dictionary<string, string> Values);
    public sealed record MergeFormRequest(Dictionary<string, string> Values, bool? Flatten);
}
