using System.Security.Claims;

using Finmy.Identity.Infrastructure.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Finmy.Identity.Api.Endpoints;

public static class IdentityDemoEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/identity");

        group.MapGet("/ping", () => "Identity pong!")
            .AllowAnonymous();

        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization();

        group.MapGet("/admin-only", AdminOnly)
            .RequireAuthorization(p => p.RequireRole("Admin"));
    }

    private static IResult GetCurrentUser(ClaimsPrincipal user)
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
    }

    private static IResult AdminOnly() => Results.Ok("You are Admin!");
}