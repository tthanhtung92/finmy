using Finmy.Identity.Application.Authentication;
using Finmy.Identity.Application.Authentication.Dtos;
using Finmy.Modularity.Extensions;
using Finmy.Modularity.Filters;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Finmy.Identity.Api.Endpoints;

public static class IdentityCoreEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/identity");

        group.MapPost("/register", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .AllowAnonymous();

        group.MapPost("/refresh", RefreshAsync)
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>()
            .AllowAnonymous();

        // Anonymous on purpose: logout revokes a refresh token the caller already holds, and
        // requiring a live access token would make logout impossible once it has expired.
        group.MapPost("/logout", LogoutAsync)
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>()
            .AllowAnonymous();
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest req, AuthService svc)
    {
        var result = await svc.RegisterAsync(req);
        // No Users route yet, but return the standard shape anyway
        return result.Match(id => Results.Created($"/api/v1/identity/users/{id}", new { userId = id }));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest req,
        AuthService svc,
        HttpContext httpCtx,
        CancellationToken cancellationToken)
    {
        var ip = GetClientIp(httpCtx);
        var result = await svc.LoginAsync(req, ip, cancellationToken);
        return result.Match(authResult => Results.Ok(authResult));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest req,
        AuthService svc,
        HttpContext httpCtx,
        CancellationToken cancellationToken)
    {
        var ip = GetClientIp(httpCtx);
        var result = await svc.RefreshAsync(req.RefreshToken, ip, cancellationToken);
        return result.Match(authResult => Results.Ok(authResult));
    }

    private static async Task<IResult> LogoutAsync(
        RefreshRequest req,
        AuthService svc,
        CancellationToken cancellationToken)
    {
        await svc.LogoutAsync(req.RefreshToken, cancellationToken);
        return Results.NoContent();
    }

    private static string GetClientIp(HttpContext httpCtx) =>
        httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}