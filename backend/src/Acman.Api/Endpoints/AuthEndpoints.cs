using FluentValidation;
using MediatR;
using Modules.Identity.Application;

namespace Acman.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterCommand command,
            IMediator mediator,
            IValidator<RegisterCommand> validator,
            CancellationToken ct) =>
        {
            var result = await validator.ValidateAsync(command, ct);
            if (!result.IsValid) throw new ValidationException(result.Errors);
            var response = await mediator.Send(command, ct);
            return Results.Ok(response);
        });

        group.MapPost("/login", async (
            LoginCommand command,
            IMediator mediator,
            IValidator<LoginCommand> validator,
            CancellationToken ct) =>
        {
            var result = await validator.ValidateAsync(command, ct);
            if (!result.IsValid) throw new ValidationException(result.Errors);
            var response = await mediator.Send(command, ct);
            return Results.Ok(response);
        });

        group.MapGet("/me", (HttpContext http) =>
        {
            if (http.User.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                id = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                email = http.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                        ?? http.User.FindFirst("email")?.Value,
                displayName = http.User.FindFirst("display_name")?.Value,
                name = http.User.Identity?.Name
            });
        }).RequireAuthorization();

        return app;
    }
}
