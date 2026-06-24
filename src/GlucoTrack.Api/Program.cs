using System.Text;
using GlucoTrack.Api.Data;
using GlucoTrack.Api.Endpoints;
using GlucoTrack.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddDbContext<AppDbContext>((sp, opts) =>
{
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
});

builder.Services.AddIdentityCore<AppUser>(opts =>
{
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequiredLength = 8;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // One-time backfill: mark HasImage=true for products whose image file already
    // exists on disk from before the HasImage column was introduced.
    {
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var imagesDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "images", "products");
        if (Directory.Exists(imagesDir))
        {
            var idsWithImage = Directory.GetFiles(imagesDir)
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Select(name => Guid.TryParse(name, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToHashSet();

            if (idsWithImage.Count > 0)
            {
                var toUpdate = await db.Products.IgnoreQueryFilters()
                    .Where(p => idsWithImage.Contains(p.Id) && !p.HasImage)
                    .ToListAsync();
                foreach (var p in toUpdate) p.HasImage = true;
                if (toUpdate.Count > 0) await db.SaveChangesAsync();
            }
        }
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    const string adminRole = "Admin";
    if (!await roleManager.RoleExistsAsync(adminRole))
        await roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));

    var adminEmail = config["Admin:Email"] ?? "admin@glucotrack.local";
    var adminPassword = config["Admin:Password"] ?? "Admin1234";

    var admin = await userManager.FindByEmailAsync(adminEmail);
    if (admin is null)
    {
        admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
            throw new Exception("Admin seed failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));
    }
    // Ensure role membership even for an admin account that already existed before the
    // Admin role was introduced — otherwise it would never end up in the role in the DB.
    if (!await userManager.IsInRoleAsync(admin, adminRole))
        await userManager.AddToRoleAsync(admin, adminRole);
}

app.UseBlazorFrameworkFiles();

if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers["Pragma"] = "no-cache";
            ctx.Context.Response.Headers["Expires"] = "0";
        }
    });
}
else
{
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapInviteEndpoints();
app.MapSyncEndpoints();
app.MapMealEndpoints();
app.MapGlucoseEndpoints();
app.MapInsulinEndpoints();
app.MapProductEndpoints();
app.MapMealTemplateEndpoints();
app.MapSettingsEndpoints();
app.MapFallbackToFile("index.html");

app.Run();
