using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Finmy.Identity.Infrastructure.Persistence;

public sealed class IdentityModuleDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args)
    {
        // UserSecretID configure at Finmy.Identity.Infrastructure.csproj (<PropertyGroup>)
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<IdentityModuleDbContextFactory>()
            .AddEnvironmentVariables()
            .Build();

        string? connectionString = configuration.GetConnectionString("IdentityDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'IdentityDb' is not configured.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<IdentityModuleDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new IdentityModuleDbContext(optionsBuilder.Options);
    }
}
