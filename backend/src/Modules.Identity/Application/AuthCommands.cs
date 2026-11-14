using Acman.BuildingBlocks.Errors;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.Identity.Domain;
using Modules.Identity.Services;

namespace Modules.Identity.Application;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string? DisplayName,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record RegisterCommand(string Email, string Password, string? DisplayName) : IRequest<AuthResponse>;
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IJwtTokenService _jwt;

    public RegisterHandler(UserManager<ApplicationUser> users, IJwtTokenService jwt)
    {
        _users = users;
        _jwt = jwt;
    }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await _users.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new ValidationAppException("Email is already registered.", "email_taken");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            EmailConfirmed = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var message = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new ValidationAppException(message, "register_failed");
        }

        var (token, expires) = _jwt.CreateToken(user);
        return new AuthResponse(user.Id, user.Email!, user.DisplayName, token, expires);
    }
}

public sealed class LoginHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IJwtTokenService _jwt;

    public LoginHandler(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IJwtTokenService jwt)
    {
        _users = users;
        _signIn = signIn;
        _jwt = jwt;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email)
                   ?? throw new ValidationAppException("Invalid email or password.", "invalid_credentials");

        var check = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            throw new ValidationAppException("Invalid email or password.", "invalid_credentials");
        }

        var (token, expires) = _jwt.CreateToken(user);
        return new AuthResponse(user.Id, user.Email!, user.DisplayName, token, expires);
    }
}
