using Microsoft.AspNetCore.Identity;

namespace GlucoTrack.Api.Data;

public class AppUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
