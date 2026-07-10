using System.Security.Claims;

using EventHub.Identity.Application.Authentication;
using EventHub.Identity.Infrastructure;
using EventHub.Identity.Infrastructure.Authentication;
using EventHub.Modularity;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventHub.Identity.Api;

public sealed class IdentityModule : IModule
{

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
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

        // DEMO
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

        // DEMO
        endpoints.MapGet("/identity/admin-only", () => Results.Ok("You are Admin!")).RequireAuthorization(p => p.RequireRole("Admin"));
    }
}
