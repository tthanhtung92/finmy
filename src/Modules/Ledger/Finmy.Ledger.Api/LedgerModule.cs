using Finmy.Modularity.Abstractions;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finmy.Ledger.Api;

public sealed class LedgerModule : IModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {

    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {

    }
}