namespace GlucoTrack.Shared.DTOs.Auth;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
