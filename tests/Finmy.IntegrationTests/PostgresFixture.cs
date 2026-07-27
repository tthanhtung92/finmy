using Finmy.Budgeting.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace Finmy.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresFixture()
    {
        _container = new PostgreSqlBuilder("postgres:17.10-alpine")
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var context = CreateBudgetingDbContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public BudgetingDbContext CreateBudgetingDbContext()
    {
        var options = new DbContextOptionsBuilder<BudgetingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new BudgetingDbContext(options);
    }
}
