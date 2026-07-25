using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Infrastructure.Persistence;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Finmy.Ledger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ITransactionStatusStore, InMemoryTransactionStatusStore>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
