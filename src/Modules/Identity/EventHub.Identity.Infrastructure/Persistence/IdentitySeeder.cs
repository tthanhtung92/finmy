using EventHub.Identity.Infrastructure.Identity;
using EventHub.Identity.Infrastructure.Options;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EventHub.Identity.Infrastructure.Persistence;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IdentitySeedOptions options)
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
        if (options.IsSeedAdmin)
        {
            var adminUserName = options.AdminUserName ?? throw new InvalidOperationException($"AdminUserName is not configured");
            var adminEmail = options.AdminEmail ?? throw new InvalidOperationException($"AdminEmail is not configured");
            var adminPassword = options.AdminPassword ?? throw new InvalidOperationException($"AdminPassword is not configured");

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
