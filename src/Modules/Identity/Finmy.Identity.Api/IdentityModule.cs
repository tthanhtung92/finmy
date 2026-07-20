using Finmy.Identity.Api.Endpoints;
using Finmy.Identity.Application.Authentication.Dtos;
using Finmy.Identity.Infrastructure;
using Finmy.Modularity.Abstractions;

using FluentValidation;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finmy.Identity.Api;

public sealed class IdentityModule : IModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);

        // Validator
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<RefreshRequestValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        IdentityCoreEndpoints.MapEndpoints(endpoints);
        IdentityDemoEndpoints.MapEndpoints(endpoints);
    }
}
