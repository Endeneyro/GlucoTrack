using System.Security.Claims;

namespace GlucoTrack.Api.Data;

public class CurrentUserService : ICurrentUserService
{
    public Guid UserId { get; }
    public bool IsAdmin { get; }

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        UserId = value is not null ? Guid.Parse(value) : Guid.Empty;
        IsAdmin = httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false;
    }
}
