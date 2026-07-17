using Finmy.Identity.Api;
using Finmy.Modularity.Interfaces;

namespace Finmy.Api.Extensions;

public static class ModuleExtensions
{
    private static readonly IReadOnlyList<IModule> Modules =  [new IdentityModule()];

    public static void AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var module in Modules)
        {
            module.ConfigureServices(services, configuration);
        }
    }

    public static void UseModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in Modules)
        {
            module.MapEndpoints(endpoints);
        }
    }
}
