using FluentValidation;
using MediatR;
using Modules.Processing.Application;

namespace Acman.Api.Endpoints;

public static class ToolEndpoints
{
    public static IEndpointRouteBuilder MapToolEndpoints(this IEndpointRouteBuilder app)
    {
        var pdf = app.MapGroup("/api/v1/pdf").WithTags("PdfTools");
        var jobs = app.MapGroup("/api/v1/jobs").WithTags("Jobs");

        pdf.MapPost("/merge", async (
            MergeRequest body,
            IMediator mediator,
            IValidator<EnqueueMergeCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueMergeCommand(body.DocumentIds, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/split", async (
            SplitRequest body,
            IMediator mediator,
            IValidator<EnqueueSplitCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueSplitCommand(body.DocumentId, body.Ranges, body.OutputPrefix);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/rotate", async (
            RotateRequest body,
            IMediator mediator,
            IValidator<EnqueueRotateCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueRotateCommand(body.DocumentId, body.Degrees, body.PageIndexes, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/compress", async (
            CompressRequest body,
            IMediator mediator,
            IValidator<EnqueueCompressCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueCompressCommand(body.DocumentId, body.RemoveMetadata ?? true, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/organize", async (
            OrganizeRequest body,
            IMediator mediator,
            IValidator<EnqueueOrganizeCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueOrganizeCommand(body.DocumentId, body.PageOrder, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/watermark", async (
            WatermarkRequest body,
            IMediator mediator,
            IValidator<EnqueueWatermarkCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueWatermarkCommand(
                body.DocumentId,
                body.Text ?? "Nam-Dev",
                body.Opacity ?? 0.25,
                body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/protect", async (
            ProtectRequest body,
            IMediator mediator,
            IValidator<EnqueueProtectCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueProtectCommand(body.DocumentId, body.UserPassword, body.OwnerPassword, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/images-to-pdf", ImagesToPdfAsync).DisableAntiforgery().RequireAuthorization();

        pdf.MapPost("/extract-pages", async (
            ExtractPagesRequest body,
            IMediator mediator,
            IValidator<EnqueueExtractPagesCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueExtractPagesCommand(body.DocumentId, body.PageIndexes, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/reverse", async (
            ReverseRequest body,
            IMediator mediator,
            IValidator<EnqueueReverseCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueReverseCommand(body.DocumentId, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/delete-pages", async (
            DeletePagesRequest body,
            IMediator mediator,
            IValidator<EnqueueDeletePagesCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueDeletePagesCommand(body.DocumentId, body.PageIndexes, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/page-numbers", async (
            PageNumbersRequest body,
            IMediator mediator,
            IValidator<EnqueuePageNumbersCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueuePageNumbersCommand(body.DocumentId, body.Format, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/unlock", async (
            UnlockRequest body,
            IMediator mediator,
            IValidator<EnqueueUnlockCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueueUnlockCommand(body.DocumentId, body.UserPassword, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        pdf.MapPost("/to-images", async (
            PdfToImagesRequest body,
            IMediator mediator,
            IValidator<EnqueuePdfToImagesCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new EnqueuePdfToImagesCommand(body.DocumentId, body.Dpi ?? 144, body.OutputFileName);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var job = await mediator.Send(cmd, ct);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", job);
        }).RequireAuthorization();

        jobs.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var job = await mediator.Send(new GetJobQuery(id), ct);
            return Results.Ok(job);
        }).RequireAuthorization();

        jobs.MapGet("/{id:guid}/download", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DownloadJobResultQuery(id), ct);
            return Results.File(result.Content, result.ContentType, result.FileName);
        }).RequireAuthorization();

        return app;
    }

    public sealed record MergeRequest(List<Guid> DocumentIds, string? OutputFileName);
    public sealed record SplitRequest(Guid DocumentId, List<SplitRangeDto>? Ranges, string? OutputPrefix);
    public sealed record RotateRequest(Guid DocumentId, int Degrees, List<int>? PageIndexes, string? OutputFileName);
    public sealed record CompressRequest(Guid DocumentId, bool? RemoveMetadata, string? OutputFileName);
    public sealed record OrganizeRequest(Guid DocumentId, List<int> PageOrder, string? OutputFileName);
    public sealed record WatermarkRequest(Guid DocumentId, string? Text, double? Opacity, string? OutputFileName);
    public sealed record ProtectRequest(Guid DocumentId, string UserPassword, string? OwnerPassword, string? OutputFileName);
    public sealed record ExtractPagesRequest(Guid DocumentId, List<int> PageIndexes, string? OutputFileName);
    public sealed record ReverseRequest(Guid DocumentId, string? OutputFileName);
    public sealed record DeletePagesRequest(Guid DocumentId, List<int> PageIndexes, string? OutputFileName);
    public sealed record PageNumbersRequest(Guid DocumentId, string? Format, string? OutputFileName);
    public sealed record UnlockRequest(Guid DocumentId, string UserPassword, string? OutputFileName);
    public sealed record PdfToImagesRequest(Guid DocumentId, int? Dpi, string? OutputFileName);

    private static async Task<IResult> ImagesToPdfAsync(
        HttpRequest request,
        Acman.Pdf.Abstractions.Contracts.IPdfManipulationService tools,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = new { message = "multipart/form-data required" } });
        }

        var form = await request.ReadFormAsync(ct);
        var files = form.Files.Where(f => f.Length > 0).ToList();
        if (files.Count == 0)
        {
            return Results.BadRequest(new { error = new { message = "At least one image file is required." } });
        }

        var images = new List<Acman.Pdf.Abstractions.Contracts.ImagePageInput>();
        foreach (var file in files)
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            images.Add(new Acman.Pdf.Abstractions.Contracts.ImagePageInput(ms.ToArray(), file.ContentType));
        }

        var pdf = await tools.ImagesToPdfAsync(images, ct);
        return Results.File(pdf, "application/pdf", "images.pdf");
    }
}
