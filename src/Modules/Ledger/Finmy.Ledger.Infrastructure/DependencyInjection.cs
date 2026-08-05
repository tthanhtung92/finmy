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

        // Configure Options
        services.AddOptions<TransactionStatusOptions>()
            .Bind(configuration.GetSection(TransactionStatusOptions.SectionName));

        // AddSingleton
        services.TryAddSingleton(TimeProvider.System);

        // AddScoped
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<ITransactionRequestStatusStore, PostgresTransactionStatusStore>();

        // Configure Hosted Service
        // Registered as itself too, not just as IHostedService, so a test can resolve the
        // service directly and run PruneExpiredAsync() once instead of waiting out the timer.
        services.AddSingleton<TransactionRequestPruningService>();
        services.AddHostedService(sp => sp.GetRequiredService<TransactionRequestPruningService>());

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
