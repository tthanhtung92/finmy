using EventHub.Identity.Infrastructure.Identity;
using EventHub.Identity.Infrastructure.Options;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EventHub.Identity.Infrastructure.Persistence;

public class IdentitySeederHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IdentitySeedOptions options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await IdentitySeeder.SeedAsync(roleManager, userManager, options);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
