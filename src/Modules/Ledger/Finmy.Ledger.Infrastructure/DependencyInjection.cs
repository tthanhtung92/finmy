using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Infrastructure.Persistence;
using Finmy.Modularity.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Wolverine.EntityFrameworkCore;

namespace Finmy.Ledger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure DbContext
        AddDbContext(services, configuration);

        // AddSingleton
        services.AddSingleton<ITransactionRequestStatusStore, InMemoryTransactionStatusStore>();
        services.TryAddSingleton(TimeProvider.System);

        // AddScoped
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        return services;
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("LedgerDb");
        services.AddDbContextWithWolverineIntegration<LedgerDbContext>(options => options.UseNpgsql(
            connectionString,
            x => x.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "ledger")));
    }
}
