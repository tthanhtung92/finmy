namespace EventHub.Identity.Application.Authentication.Dto;

public record AuthResult(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);