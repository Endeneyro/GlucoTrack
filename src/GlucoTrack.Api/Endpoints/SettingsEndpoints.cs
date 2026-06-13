using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using GlucoTrack.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/settings").RequireAuthorization();
        settings.MapGet("/", GetSettingsAsync);
        settings.MapPut("/", UpsertSettingsAsync);

        var therapy = app.MapGroup("/api/therapy").RequireAuthorization();
        therapy.MapGet("/", GetTherapyAsync);
        therapy.MapPost("/", CreateTherapyAsync);
        therapy.MapPut("/{id:guid}", UpdateTherapyAsync);
        therapy.MapDelete("/{id:guid}", DeleteTherapyAsync);

        return app;
    }

    // ── User Settings ────────────────────────────────────────────────────────

    private static async Task<IResult> GetSettingsAsync(AppDbContext db)
    {
        var settings = await db.UserSettings.FirstOrDefaultAsync();
        return settings is null ? Results.NoContent() : Results.Ok(settings);
    }

    private static async Task<IResult> UpsertSettingsAsync(
        SettingsRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var settings = await db.UserSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new UserSettings { Id = Guid.NewGuid(), UserId = currentUser.UserId };
            db.UserSettings.Add(settings);
        }

        settings.TargetGlucoseLow = req.TargetGlucoseLow;
        settings.TargetGlucoseHigh = req.TargetGlucoseHigh;
        settings.TargetGlucose = req.TargetGlucose;
        settings.XeGrams = req.XeGrams;
        settings.DailyCalories = req.DailyCalories;
        settings.DailyProtein = req.DailyProtein;
        settings.DailyFat = req.DailyFat;
        settings.DailyCarbs = req.DailyCarbs;
        settings.DisclaimerAccepted = req.DisclaimerAccepted;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(settings);
    }

    // ── Therapy Coefficients ─────────────────────────────────────────────────

    private static async Task<IResult> GetTherapyAsync(AppDbContext db)
        => Results.Ok(await db.TherapyCoefficients.OrderBy(t => t.FromTime).ToListAsync());

    private static async Task<IResult> CreateTherapyAsync(
        TherapyRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var coeff = new TherapyCoefficient
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId,
            FromTime = req.FromTime,
            ToTime = req.ToTime,
            InsulinToCarbRatio = req.InsulinToCarbRatio,
            InsulinSensitivityFactor = req.InsulinSensitivityFactor,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.TherapyCoefficients.Add(coeff);
        await db.SaveChangesAsync();
        return Results.Ok(coeff);
    }

    private static async Task<IResult> UpdateTherapyAsync(Guid id, TherapyRequest req, AppDbContext db)
    {
        var coeff = await db.TherapyCoefficients.FindAsync(id);
        if (coeff is null) return Results.NotFound();
        coeff.FromTime = req.FromTime; coeff.ToTime = req.ToTime;
        coeff.InsulinToCarbRatio = req.InsulinToCarbRatio;
        coeff.InsulinSensitivityFactor = req.InsulinSensitivityFactor;
        coeff.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(coeff);
    }

    private static async Task<IResult> DeleteTherapyAsync(Guid id, AppDbContext db)
    {
        var coeff = await db.TherapyCoefficients.FindAsync(id);
        if (coeff is null) return Results.NotFound();
        coeff.IsDeleted = true; coeff.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private record SettingsRequest(
        double TargetGlucoseLow, double TargetGlucoseHigh, double TargetGlucose,
        double XeGrams, double DailyCalories, double DailyProtein,
        double DailyFat, double DailyCarbs, bool DisclaimerAccepted);

    private record TherapyRequest(
        TimeOnly FromTime, TimeOnly ToTime,
        double InsulinToCarbRatio, double InsulinSensitivityFactor);
}
