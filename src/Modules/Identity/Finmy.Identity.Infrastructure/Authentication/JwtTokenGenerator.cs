using System.Security.Claims;
using System.Text;

using Finmy.Identity.Application.Authentication;
using Finmy.Identity.Infrastructure.Options;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Finmy.Identity.Infrastructure.Authentication;

public class JwtTokenGenerator(IOptions<JwtOptions> jwtOptionsAccessor, TimeProvider timeProvider) : IJwtTokenGenerator
{
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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptionsAccessor.Value.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(jwtOptionsAccessor.Value.AccessTokenLifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwtOptionsAccessor.Value.Issuer,
            Audience = jwtOptionsAccessor.Value.Audience,
            Expires = expiresAt,
            SigningCredentials = creds
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);
        var accessTokenOutcome = new AccessTokenOutcome(accessToken, expiresAt);

        return accessTokenOutcome;
    }
}
