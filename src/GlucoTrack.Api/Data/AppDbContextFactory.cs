using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GlucoTrack.Api.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=glucotrack_design;Username=postgres")
            .Options;
        return new AppDbContext(opts, new DesignTimeCurrentUser());
    }

    private class DesignTimeCurrentUser : ICurrentUserService
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdmin => false;
    }
}
