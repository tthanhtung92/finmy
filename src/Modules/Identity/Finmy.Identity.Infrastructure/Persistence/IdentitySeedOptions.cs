namespace Finmy.Identity.Infrastructure.Persistence;

public class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";
    public bool IsSeedAdmin { get; set; }
    public string? AdminUserName { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
}
