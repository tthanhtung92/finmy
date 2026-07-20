using Finmy.Identity.Application.Abstractions;
using Finmy.Identity.Application.Authentication.Dtos;
using Finmy.SharedKernel.Results;

namespace Finmy.Identity.Application.Authentication;

public class AuthService(IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator)
{
    private readonly IIdentityService _identityService = identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<Result<Guid>> RegisterAsync(RegisterRequest request)
    {
        var result = await _identityService.RegisterUserAsync(request.Email, request.Password);
        return result;
    }

    public async Task<Result<AuthResult>> LoginAsync(LoginRequest request, string ip, CancellationToken cancellationToken)
    {
        var userId = await _identityService.VerifyPasswordAsync(request.Email, request.Password);

        if (userId is null)
            return new Error("Identity.InvalidCredentials", "Incorrect email or password.", ErrorType.Unauthorized);

        var roles = await _identityService.GetRolesAsync(userId.Value);
        var accessToken = _jwtTokenGenerator.GenerateToken(userId.Value.ToString(), request.Email, roles);
        var refreshToken = await _identityService.CreateRefreshTokenAsync(userId.Value, ip, cancellationToken);

        return new AuthResult(accessToken.Value, refreshToken, accessToken.ExpiresAt);
    }

    public async Task<Result<AuthResult>> RefreshAsync(string rawRefreshToken, string ip, CancellationToken cancellationToken)
    {
        var rotated = await _identityService.RotateRefreshTokenAsync(rawRefreshToken, ip, cancellationToken);
        if (rotated is null)
            return new Error("Identity.InvalidRefreshToken", "Invalid or expired refresh token.", ErrorType.Unauthorized);

        var email = await _identityService.GetEmailAsync(rotated.UserId);
        if (email is null)
            return new Error("Identity.UserNotFound", "Invalid or expired refresh token.", ErrorType.Unauthorized);

        var roles = await _identityService.GetRolesAsync(rotated.UserId);
        var accessToken = _jwtTokenGenerator.GenerateToken(rotated.UserId.ToString(), email, roles);

        return new AuthResult(accessToken.Value, rotated.RawRefreshToken, accessToken.ExpiresAt);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        await _identityService.RevokeRefreshTokenAsync(rawRefreshToken, cancellationToken);
    }
}
