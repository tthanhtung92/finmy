using Microsoft.IdentityModel.JsonWebTokens;

namespace EventHub.Identity.Infrastructure.Authentication;

internal static class IdentityClaimTypes
{
    public const string Sub = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Role = "role";
}
