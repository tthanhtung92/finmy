namespace EventHub.Identity.Application.Authentication;

public record RegisterOutcome(bool Succeeded, Guid? UserId, RegisterFailureReason Reason, string[] Errors);

public enum RegisterFailureReason
{
    None,
    DuplicateEmail,
    WeakPassword,
    Unknown
}

public interface IIdentityService
{
    Task<RegisterOutcome> RegisterUserAsync(string email, string password);
    Task<Guid?> VerifyPasswordAsync(string email, string password);
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId);
    Task<string> CreateRefreshTokenAsync(Guid userId, string ip, CancellationToken cancellationToken);
}
