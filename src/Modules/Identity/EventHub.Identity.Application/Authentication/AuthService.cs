using EventHub.Identity.Application.Authentication.Dto;
using EventHub.SharedKernel.Results;

namespace EventHub.Identity.Application.Authentication;

public class AuthService(IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator)
{
    private readonly IIdentityService _identityService = identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<Result<Guid>> RegisterAsync(RegisterRequest request)
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

    public async Task<AuthResult?> RefreshAsync(string rawRefreshToken, string ip, CancellationToken cancellationToken)
    {
        var rotated = await _identityService.RotateRefreshTokenAsync(rawRefreshToken, ip, cancellationToken);
        if (rotated == null) return null;

        var email = await _identityService.GetEmailAsync(rotated.UserId);
        if (email == null) return null;

        var roles = await _identityService.GetRolesAsync(rotated.UserId);
        var accessToken = _jwtTokenGenerator.GenerateToken(rotated.UserId.ToString(), email, roles);

        return new AuthResult(accessToken.Value, rotated.RawRefreshToken, accessToken.ExpiresAt);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        await _identityService.RevokeRefreshTokenAsync(rawRefreshToken, cancellationToken);
    }
}
