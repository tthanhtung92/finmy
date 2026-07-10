namespace EventHub.Identity.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public int AccessTokenLifetimeMinutes { get; set; }
    public int RefreshTokenLifetimeDays { get; set; }

    /// <summary>
    /// The signing key used to sign the JWT tokens. This should be a secure, random string and kept secret.
    /// Current stored in dotnet user-secrets for development.
    /// User-secrets key: "Jwt:SigningKey"
    /// </summary>
    public string? SigningKey { get; set; }
}
