namespace Finmy.Identity.Application.Authentication.Dtos;

public record AuthResult(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);