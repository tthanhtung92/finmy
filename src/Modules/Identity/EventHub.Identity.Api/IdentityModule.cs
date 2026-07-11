using EventHub.Identity.Api.Endpoints;
using EventHub.Identity.Infrastructure;
using EventHub.Modularity;

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
        IdentityCoreEndpoints.MapEndpoints(endpoints);
        IdentityDemoEndpoints.MapEndpoints(endpoints);
    }
}
