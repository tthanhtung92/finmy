namespace EventHub.Identity.Application.Authentication;

public record AuthResult(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);
public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);

public class AuthService(IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator)
{
    private readonly IIdentityService _identityService = identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<RegisterOutcome> RegisterAsync(RegisterRequest request)
    {
        var result = await _identityService.RegisterUserAsync(request.Email, request.Password);
        return result;
    }

    public async Task<AuthResult?> LoginAsync(LoginRequest request, string ip, CancellationToken cancellationToken)
    {
        var userId = await _identityService.VerifyPasswordAsync(request.Email, request.Password);
        if (userId == null) return null;

        var roles = await _identityService.GetRolesAsync(userId.Value);

        var accessToken = _jwtTokenGenerator.GenerateToken(userId.Value.ToString(), request.Email, roles);
        var refreshToken = await _identityService.CreateRefreshTokenAsync(userId.Value, ip, cancellationToken);

        return new AuthResult(accessToken.Value, refreshToken, accessToken.ExpiresAt);
    }
}
