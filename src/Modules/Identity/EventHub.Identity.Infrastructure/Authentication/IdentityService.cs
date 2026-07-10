using System.Security.Cryptography;

using EventHub.Identity.Application.Authentication;
using EventHub.Identity.Domain.Identity;
using EventHub.Identity.Infrastructure.Identity;
using EventHub.Identity.Infrastructure.Options;
using EventHub.Identity.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

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

    public async Task<string> CreateRefreshTokenAsync(Guid userId, string ip, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var (newToken, newRefreshToken) = await GenerateRefreshToken(userId, ip, now);

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newToken;
    }

    public async Task<RotatedRefreshToken?> RotateRefreshTokenAsync(string rawRefreshToken, string ip, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var rawTokenHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawRefreshToken)));

        var dataToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == rawTokenHash, cancellationToken: cancellationToken);

        // Kiểm tra xem có tồn tại rawTokenHash dưới DB không
        if (dataToken == null) return null;

        // Kiểm tra xem rawTokenHash đã revoke lần nào chưa
        if (dataToken.RevokedAt != null)
        {
            await _dbContext.RefreshTokens
                .Where(x => x.UserId == dataToken.UserId && x.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(y => y.RevokedAt, now), cancellationToken: cancellationToken);

            return null;
        }

        // Kiểm tra xem rawTokenHash đã hết hạn chưa
        if (dataToken.ExpiresAt <= now) return null;

        var (newToken, newRefreshToken) = await GenerateRefreshToken(dataToken.UserId, ip, now);

        // Sửa cũ
        dataToken.RevokedAt = now;
        dataToken.ReplacedByTokenHash = newRefreshToken.TokenHash;

        // Thêm mới
        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RotatedRefreshToken(dataToken.UserId, newToken);
    }

    public async Task RevokeRefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var rawTokenHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawRefreshToken)));

        var dataToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == rawTokenHash, cancellationToken: cancellationToken);

        if (dataToken != null && dataToken.RevokedAt == null)
        {
            dataToken.RevokedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return [];

        var roles = await _userManager.GetRolesAsync(user);
        return roles == null ? [] : roles.ToList();
    }

    public async Task<string?> GetEmailAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.Email;
    }

    #region Private Method

    private async Task<(string newToken, RefreshToken newRefreshToken)> GenerateRefreshToken(Guid userId, string ip, DateTimeOffset time)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var newToken = WebEncoders.Base64UrlEncode(randomBytes);

        var newTokenHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(newToken)));
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = newTokenHash,
            ExpiresAt = time.AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            CreatedAt = time,
            CreatedByIp = ip
        };

        return (newToken, newRefreshToken);
    }

    #endregion
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
