namespace EventHub.Identity.Application.Authentication;

public record RegisterOutcome(bool Succeeded, Guid? UserId, RegisterFailureReason Reason, string[] Errors);

public enum RegisterFailureReason
{
    None,
    DuplicateEmail,
    WeakPassword,
    Unknown
}

public record RotatedRefreshToken(Guid UserId, string RawRefreshToken);

public interface IIdentityService
{
    Task<RegisterOutcome> RegisterUserAsync(string email, string password);
    Task<Guid?> VerifyPasswordAsync(string email, string password);
    Task<string> CreateRefreshTokenAsync(Guid userId, string ip, CancellationToken cancellationToken);
    Task<RotatedRefreshToken?> RotateRefreshTokenAsync(string rawRefreshToken, string ip, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId);
    Task<string?> GetEmailAsync(Guid userId);
}
