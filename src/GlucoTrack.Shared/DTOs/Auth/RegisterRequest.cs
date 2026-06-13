namespace GlucoTrack.Shared.DTOs.Auth;

public record RegisterRequest(string Email, string Password, string InviteCode);
