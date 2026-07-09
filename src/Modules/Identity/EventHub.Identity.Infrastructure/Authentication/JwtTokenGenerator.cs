using System.Security.Claims;
using System.Text;

using EventHub.Identity.Application.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EventHub.Identity.Infrastructure.Authentication;

public class JwtTokenGenerator(JwtOptions jwtOptions, TimeProvider timeProvider) : IJwtTokenGenerator
{
    private readonly JwtOptions _jwtOptions = jwtOptions ?? throw new ArgumentNullException(nameof(jwtOptions));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public AccessTokenOutcome GenerateToken(string userId, string email, IEnumerable<string> roles)
    {
        if (roles == null)
        {
            throw new ArgumentNullException(nameof(roles), "Roles cannot be null.");
        }

        var claims = new List<Claim>
        {
            new(IdentityClaimTypes.Sub, userId),
            new(IdentityClaimTypes.Email, email),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(IdentityClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey ?? throw new InvalidOperationException("Signing key is not configured.")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Expires = expiresAt,
            SigningCredentials = creds
        };

        //JsonWebTokenHandler().CreateToken(descriptor)

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);
        var accessTokenOutcome = new AccessTokenOutcome(accessToken, expiresAt);

        return accessTokenOutcome;
    }
}
