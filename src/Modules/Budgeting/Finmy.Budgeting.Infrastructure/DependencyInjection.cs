using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Envelopes;
using Finmy.Budgeting.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finmy.Budgeting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure DbContext
        AddDbContext(services, configuration);

        services.AddScoped<IEnvelopeRepository, EnvelopeRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<EnvelopeService>();

        return services;
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BudgetingDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'BudgetingDb' is not configured.");
        }
        services.AddDbContext<BudgetingDbContext>(options => options.UseNpgsql(connectionString));
    }
}
