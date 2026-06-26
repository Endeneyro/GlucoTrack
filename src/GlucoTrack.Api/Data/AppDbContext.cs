using GlucoTrack.Shared.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    private readonly Guid _currentUserId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
        : base(options)
    {
        _currentUserId = currentUser.UserId;
    }

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserInsulin> UserInsulins => Set<UserInsulin>();
    public DbSet<TherapyCoefficient> TherapyCoefficients => Set<TherapyCoefficient>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();
    public DbSet<ProductReaction> ProductReactions => Set<ProductReaction>();
    public DbSet<ProductHide> ProductHides => Set<ProductHide>();
    public DbSet<ProductUsage> ProductUsages => Set<ProductUsage>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<GlucoseReading> GlucoseReadings => Set<GlucoseReading>();
    public DbSet<InsulinInjection> InsulinInjections => Set<InsulinInjection>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<MealTemplate> MealTemplates => Set<MealTemplate>();
    public DbSet<MealTemplateItem> MealTemplateItems => Set<MealTemplateItem>();
    public DbSet<PlannedEvent> PlannedEvents => Set<PlannedEvent>();
    public DbSet<PlannedEventMealItem> PlannedEventMealItems => Set<PlannedEventMealItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserSettings>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);
        builder.Entity<UserProfile>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);
        builder.Entity<UserInsulin>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);
        builder.Entity<TherapyCoefficient>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);
        builder.Entity<MealEntry>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);
        builder.Entity<GlucoseReading>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);
        builder.Entity<InsulinInjection>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);
        builder.Entity<MealTemplate>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);
        builder.Entity<PlannedEvent>().HasQueryFilter(e => e.UserId == _currentUserId && !e.IsDeleted);

        builder.Entity<MealTemplateItem>()
            .HasOne(i => i.MealTemplate)
            .WithMany(t => t.Items)
            .HasForeignKey(i => i.MealTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PlannedEventMealItem>()
            .HasOne(i => i.PlannedEvent)
            .WithMany(t => t.MealItems)
            .HasForeignKey(i => i.PlannedEventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Products: base (null) + own + shared visible to everyone
        builder.Entity<Product>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (e.UserId == null || e.UserId == _currentUserId || e.OwnerType == ProductOwnerType.Shared));

        builder.Entity<Product>()
            .HasIndex(p => p.UserId);
        builder.Entity<Product>()
            .HasIndex(p => p.OwnerType);
        builder.Entity<Product>()
            .HasIndex(p => p.ClonedFromProductId);

        // ProductIngredient
        builder.Entity<ProductIngredient>()
            .HasOne(i => i.CompositeProduct)
            .WithMany(p => p.Ingredients)
            .HasForeignKey(i => i.CompositeProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProductIngredient>()
            .HasOne(i => i.IngredientProduct)
            .WithMany()
            .HasForeignKey(i => i.IngredientProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // ProductReaction — composite PK
        builder.Entity<ProductReaction>()
            .HasKey(r => new { r.UserId, r.ProductId });
        builder.Entity<ProductReaction>()
            .HasQueryFilter(r => r.UserId == _currentUserId);

        // ProductUsage — composite PK
        builder.Entity<ProductUsage>()
            .HasKey(u => new { u.UserId, u.ProductId });
        builder.Entity<ProductUsage>()
            .HasQueryFilter(u => u.UserId == _currentUserId);

        // ProductHide — composite PK
        builder.Entity<ProductHide>()
            .HasKey(h => new { h.UserId, h.ProductId });
        builder.Entity<ProductHide>()
            .HasQueryFilter(h => h.UserId == _currentUserId);

        builder.Entity<InviteCode>()
            .HasIndex(i => i.Code)
            .IsUnique();

        builder.Entity<RefreshToken>()
            .HasIndex(r => r.Token)
            .IsUnique();
        builder.Entity<RefreshToken>()
            .HasIndex(r => r.UserId);

        builder.Entity<TherapyCoefficient>()
            .HasIndex(t => new { t.UserId, t.FromTime });
    }
}
