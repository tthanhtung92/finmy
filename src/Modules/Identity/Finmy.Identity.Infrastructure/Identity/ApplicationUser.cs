using Finmy.Identity.Domain.Identity;

using Microsoft.AspNetCore.Identity;

namespace Finmy.Identity.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
