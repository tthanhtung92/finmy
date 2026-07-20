using Finmy.Budgeting.Api;
using Finmy.Identity.Api;
using Finmy.Modularity.Abstractions;

namespace Finmy.Api.Extensions;

public static class ModuleExtensions
{
    private static readonly IReadOnlyList<IModule> Modules =  [new IdentityModule(), new BudgetingModule()];

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
