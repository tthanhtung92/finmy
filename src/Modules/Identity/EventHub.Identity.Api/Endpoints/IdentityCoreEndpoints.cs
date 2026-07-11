using EventHub.Identity.Application.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EventHub.Identity.Api.Endpoints;

public sealed class IdentityCoreEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/identity/ping", () => "Identity pong!");

        endpoints.MapPost("/identity/register", async (RegisterRequest req, AuthService svc) =>
        {
            var outcome = await svc.RegisterAsync(req);

            if (outcome.Succeeded)
                return Results.Ok();

            var result = outcome.Reason switch
            {
                RegisterFailureReason.DuplicateEmail => Results.Conflict(outcome.Errors),
                RegisterFailureReason.WeakPassword => Results.ValidationProblem(new Dictionary<string, string[]> { { "password", outcome.Errors } }),
                _ => Results.Problem(),
            };

            return result;
        });

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
