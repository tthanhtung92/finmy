using Finmy.Identity.Infrastructure.Identity;
using Finmy.Identity.Infrastructure.Options;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Finmy.Identity.Infrastructure.Persistence;

public class IdentitySeederHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<IdentitySeedOptions> optionsAccessor) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await IdentitySeeder.SeedAsync(roleManager, userManager, optionsAccessor);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
