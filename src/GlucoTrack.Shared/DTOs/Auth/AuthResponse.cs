namespace GlucoTrack.Shared.DTOs.Auth;

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
