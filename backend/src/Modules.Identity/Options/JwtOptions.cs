namespace Modules.Identity.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "nam-dev";
    public string Audience { get; set; } = "nam-dev-web";
    public string SigningKey { get; set; } = "nam-dev-signing-key-change-me-32chars!";
    public int ExpiryMinutes { get; set; } = 480;
}
