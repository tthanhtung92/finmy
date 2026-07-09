using System.Security.Cryptography;

using EventHub.Identity.Application.Authentication;
using EventHub.Identity.Domain.Identity;
using EventHub.Identity.Infrastructure.Identity;
using EventHub.Identity.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace EventHub.Identity.Infrastructure.Authentication;

public class IdentityService(
    UserManager<ApplicationUser> userManager,
    JwtOptions jwtOptions,
    IdentityModuleDbContext dbContext,
    TimeProvider timeProvider) : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly JwtOptions _jwtOptions = jwtOptions;
    private readonly IdentityModuleDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<RegisterOutcome> RegisterUserAsync(string email, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email
        };

        var result = await _userManager.CreateAsync(user, password);
        var failureReason = result.ToRegisterFailureReason();

        return result.Succeeded
            ? new RegisterOutcome(true, user.Id, failureReason, [])
            : new RegisterOutcome(false, null, failureReason, [.. result.Errors.Select(e => e.Description)]);
    }

    public async Task<Guid?> VerifyPasswordAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return null;

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        return isValid ? user.Id : null;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return [];

        var roles = await _userManager.GetRolesAsync(user);
        return roles == null ? [] : roles.ToList();
    }

    public async Task<string> CreateRefreshTokenAsync(Guid userId, string ip, CancellationToken cancellationToken)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = WebEncoders.Base64UrlEncode(randomBytes);

        var tokenHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = _timeProvider.GetUtcNow().AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            CreatedAt = _timeProvider.GetUtcNow(),
            CreatedByIp = ip
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }
}

internal static class RegisterFailureReasonExtensions
{
    private static readonly string[] EmailCodes = [
        nameof(IdentityErrorDescriber.DuplicateEmail),
        nameof(IdentityErrorDescriber.DuplicateUserName),
    ];

    private static readonly string[] PasswordCodes = [
        nameof(IdentityErrorDescriber.PasswordTooShort),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric),
        nameof(IdentityErrorDescriber.PasswordRequiresDigit),
        nameof(IdentityErrorDescriber.PasswordRequiresLower),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars),
    ];

    public static RegisterFailureReason ToRegisterFailureReason(this IdentityResult result)
    {
        if (result.Succeeded)
        {
            return RegisterFailureReason.None;
        }

        if (result.Errors.Any(e => EmailCodes.Contains(e.Code)))
        {
            return RegisterFailureReason.DuplicateEmail;
        }

        if (result.Errors.Any(e => PasswordCodes.Contains(e.Code)))
        {
            return RegisterFailureReason.WeakPassword;
        }

        return RegisterFailureReason.Unknown;
    }
}
