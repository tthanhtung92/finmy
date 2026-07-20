using Finmy.Identity.Application.Authentication;
using Finmy.Identity.Application.Authentication.Dtos;
using Finmy.Modularity.Extensions;
using Finmy.Modularity.Filters;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Finmy.Identity.Api.Endpoints;

public sealed class IdentityCoreEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/identity/register", async (RegisterRequest req, AuthService svc) =>
            {
                var result = await svc.RegisterAsync(req);
                // Tạm thời chưa có route cho Users, nhưng trả ra cho đúng chuẩn
                return result.Match(id => Results.Created($"/identity/users/{id}", new { userId = id }));
            })
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        endpoints
            .MapPost("/identity/login", async (LoginRequest req, AuthService svc, HttpContext httpCtx, CancellationToken cancellationToken) =>
            {
                var ip = httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var result = await svc.LoginAsync(req, ip, cancellationToken);
                return result.Match(authResult => Results.Ok(authResult));
            })
            .AddEndpointFilter<ValidationFilter<LoginRequest>>();

        endpoints
            .MapPost("/identity/refresh", async (RefreshRequest req, AuthService svc, HttpContext httpCtx, CancellationToken cancellationToken) =>
            {
                var ip = httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var result = await svc.RefreshAsync(req.RefreshToken, ip, cancellationToken);
                return result.Match(authResult => Results.Ok(authResult));
            })
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>();

        endpoints
            .MapPost("/identity/logout", async (RefreshRequest req, AuthService svc, CancellationToken cancellationToken) =>
            {
                await svc.LogoutAsync(req.RefreshToken, cancellationToken);
                return Results.NoContent();
            })
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>();
    }
}
