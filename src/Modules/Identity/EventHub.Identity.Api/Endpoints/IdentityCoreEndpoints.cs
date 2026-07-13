using EventHub.Identity.Application.Authentication;
using EventHub.Identity.Application.Authentication.Dto;
using EventHub.Modularity;
using EventHub.Modularity.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EventHub.Identity.Api.Endpoints;

public sealed class IdentityCoreEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/identity/ping", () => "Identity pong!");

        endpoints
            .MapPost("/identity/register", async (RegisterRequest req, AuthService svc) =>
            {
                var result = await svc.RegisterAsync(req);

                // Tạm thời chưa có route cho Users, nhưng trả ra cho đúng chuẩn
                return result.Match(id => Results.Created($"/identity/users/{id}", new { userId = id }));
            })
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        endpoints.MapPost("/identity/login", async (LoginRequest req, AuthService svc, HttpContext httpCtx, CancellationToken cancellationToken) =>
        {
            var ip = httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var authResult = await svc.LoginAsync(req, ip, cancellationToken);
            return authResult != null ? Results.Ok(authResult) : Results.Unauthorized();
        });

        endpoints.MapPost("/identity/refresh", async (RefreshRequest req, AuthService svc, HttpContext httpCtx, CancellationToken cancellationToken) =>
        {
            var ip = httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var authResult = await svc.RefreshAsync(req.RefreshToken, ip, cancellationToken);
            return authResult != null ? Results.Ok(authResult) : Results.Unauthorized();
        });

        endpoints.MapPost("/identity/logout", async (RefreshRequest req, AuthService svc, CancellationToken cancellationToken) =>
        {
            await svc.LogoutAsync(req.RefreshToken, cancellationToken);
            return Results.NoContent();
        });
    }
}
