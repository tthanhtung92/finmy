namespace Finmy.Identity.Infrastructure.Options;

public class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";
    public bool IsSeedAdmin { get; set; } = false;
    public string? AdminUserName { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
}
