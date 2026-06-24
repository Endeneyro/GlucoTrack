namespace GlucoTrack.Api.Data;

public interface ICurrentUserService
{
    Guid UserId { get; }
    bool IsAdmin { get; }
}
