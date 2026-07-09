using EventHub.Identity.Application.Authentication;
using EventHub.Identity.Infrastructure;
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
                RegisterFailureReason.WeakPassword => Results.ValidationProblem(new Dictionary<string, string[]> {{ "password", outcome.Errors }}),
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
    }
}
