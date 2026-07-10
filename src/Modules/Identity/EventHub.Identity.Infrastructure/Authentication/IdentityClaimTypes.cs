using Microsoft.IdentityModel.JsonWebTokens;

namespace EventHub.Identity.Infrastructure.Authentication;

public static class IdentityClaimTypes
{
    public const string Sub = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Role = "role";
}
