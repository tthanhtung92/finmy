using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Finmy.Budgeting.Infrastructure.Persistence;

public sealed class BudgetingDbContextFactory : IDesignTimeDbContextFactory<BudgetingDbContext>
{
    public BudgetingDbContext CreateDbContext(string[] args)
    {
        // UserSecretID configure at Finmy.Budgeting.Infrastructure.csproj (<PropertyGroup>)
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BudgetingDbContextFactory>()
            .AddEnvironmentVariables()
            .Build();

        string? connectionString = configuration.GetConnectionString("BudgetingDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'BudgetingDb' is not configured.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<BudgetingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new BudgetingDbContext(optionsBuilder.Options);
    }
}
