using Finmy.Identity.Infrastructure.Users;
using Finmy.Identity.Infrastructure.Options;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Finmy.Identity.Infrastructure.Persistence;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<IdentitySeedOptions> optionsAccessor)
    {
        IReadOnlyList<string> roles = ["Admin", "User"];

        foreach (var role in roles)
        {
            var roleExist = await roleManager.RoleExistsAsync(role);

            if (!roleExist)
            {
                var newRole = new ApplicationRole { Name = role };
                var newRoleResult = await roleManager.CreateAsync(newRole);
                newRoleResult.EnsureSucceeded("role");
            }
        }

        // Optional Tạo admin mặc định
        if (optionsAccessor.Value.IsSeedAdmin)
        {
            var adminUserName = optionsAccessor.Value.AdminUserName ?? throw new InvalidOperationException($"AdminUserName is not configured");
            var adminEmail = optionsAccessor.Value.AdminEmail ?? throw new InvalidOperationException($"AdminEmail is not configured");
            var adminPassword = optionsAccessor.Value.AdminPassword ?? throw new InvalidOperationException($"AdminPassword is not configured");

            var admin = new ApplicationUser
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var isAdminExist = await userManager.FindByEmailAsync(adminEmail);

            if (isAdminExist == null)
            {
                var createAdminResult = await userManager.CreateAsync(admin, adminPassword);
                createAdminResult.EnsureSucceeded("admin");

                var setLockoutAdminResult = await userManager.SetLockoutEnabledAsync(admin, true);
                setLockoutAdminResult.EnsureSucceeded("admin");

                var addRoleToAdminResult = await userManager.AddToRoleAsync(admin, "Admin");
                addRoleToAdminResult.EnsureSucceeded("admin");
            }
        }
    }

    private static void EnsureSucceeded(this IdentityResult result, string context)
    {
        if (result.Succeeded) return;

        var errorDesc = string.Join("; ", result.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Seed {context} failed: {errorDesc}");
    }
}
