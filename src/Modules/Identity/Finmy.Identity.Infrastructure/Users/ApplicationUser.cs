using Finmy.Identity.Domain.RefreshTokens;

using Microsoft.AspNetCore.Identity;

namespace Finmy.Identity.Infrastructure.Users;

public class ApplicationUser : IdentityUser<Guid>
{
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
