using FluentValidation;
using MediatR;
using Modules.Documents.Application;

namespace Acman.Api.Endpoints;

public static class AnnotationEndpoints
{
    public static IEndpointRouteBuilder MapAnnotationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents/{documentId:guid}/annotations").WithTags("Annotations");

        group.MapGet("/", async (Guid documentId, IMediator mediator, CancellationToken ct) =>
        {
            var items = await mediator.Send(new ListAnnotationsQuery(documentId), ct);
            return Results.Ok(new { items });
        });

        group.MapPut("/", async (
            Guid documentId,
            ReplaceAnnotationsRequest body,
            IMediator mediator,
            IValidator<ReplaceAnnotationsCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new ReplaceAnnotationsCommand(documentId, body.Annotations);
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid) throw new ValidationException(validation.Errors);
            var items = await mediator.Send(cmd, ct);
            return Results.Ok(new { items });
        });

        group.MapGet("/xfdf", async (Guid documentId, IMediator mediator, CancellationToken ct) =>
        {
            var xml = await mediator.Send(new ExportXfdfQuery(documentId), ct);
            return Results.Text(xml, "application/vnd.adobe.xfdf");
        });

        group.MapPost("/xfdf", async (
            Guid documentId,
            ImportXfdfRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var items = await mediator.Send(
                new ImportXfdfCommand(documentId, body.XfdfXml, body.ReplaceExisting),
                ct);
            return Results.Ok(new { items });
        });

        return app;
    }

    public sealed record ReplaceAnnotationsRequest(List<AnnotationUpsertDto> Annotations);
    public sealed record ImportXfdfRequest(string XfdfXml, bool ReplaceExisting = true);
}
