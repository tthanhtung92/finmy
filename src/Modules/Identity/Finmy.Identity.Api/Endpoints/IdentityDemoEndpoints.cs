using System.Security.Claims;

using Finmy.Identity.Infrastructure.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Finmy.Identity.Api.Endpoints;

public sealed class IdentityDemoEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/identity/ping", () => "Identity pong!");

        endpoints.MapGet("/identity/me", (ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(IdentityClaimTypes.Sub);
            var username = user.FindFirstValue(IdentityClaimTypes.Email);
            var roles = user.FindAll(IdentityClaimTypes.Role).Select(c => c.Value);

            return Results.Ok(new
            {
                UserId = userId,
                UserName = username,
                Roles = roles
            });

        }).RequireAuthorization();

        endpoints.MapGet("/identity/admin-only", () => Results.Ok("You are Admin!")).RequireAuthorization(p => p.RequireRole("Admin"));
    }
}
