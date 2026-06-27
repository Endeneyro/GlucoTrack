using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using GlucoTrack.Shared.DTOs.Sync;
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

        var profile = app.MapGroup("/api/profile").RequireAuthorization();
        profile.MapGet("/", GetProfileAsync);
        profile.MapPut("/", UpsertProfileAsync);

        var insulins = app.MapGroup("/api/insulins").RequireAuthorization();
        insulins.MapGet("/", GetInsulinsAsync);
        insulins.MapPost("/", CreateInsulinAsync);
        insulins.MapPut("/{id:guid}", UpdateInsulinAsync);
        insulins.MapDelete("/{id:guid}", DeleteInsulinAsync);

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
        settings.DiaHours = req.DiaHours > 0 ? req.DiaHours : 4.0;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(settings);
    }

    // ── User Profile ─────────────────────────────────────────────────────────

    private static async Task<IResult> GetProfileAsync(AppDbContext db)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync();
        return profile is null ? Results.NoContent() : Results.Ok(new UserProfileDto(
            profile.Id, profile.HeightCm, profile.WeightKg, profile.DateOfBirth,
            profile.Gender, profile.DiabetesType, profile.DiagnosisYear,
            profile.UpdatedAtUtc, profile.IsDeleted));
    }

    private static async Task<IResult> UpsertProfileAsync(
        UserProfileDto req, AppDbContext db, ICurrentUserService currentUser)
    {
        var profile = await db.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId);
        if (profile is null)
        {
            profile = new UserProfile { Id = req.Id, UserId = currentUser.UserId };
            db.UserProfiles.Add(profile);
        }
        profile.HeightCm = req.HeightCm; profile.WeightKg = req.WeightKg;
        profile.DateOfBirth = req.DateOfBirth; profile.Gender = req.Gender;
        profile.DiabetesType = req.DiabetesType; profile.DiagnosisYear = req.DiagnosisYear;
        profile.UpdatedAtUtc = DateTime.UtcNow; profile.IsDeleted = req.IsDeleted;
        await db.SaveChangesAsync();
        return Results.Ok(profile);
    }

    // ── User Insulins ─────────────────────────────────────────────────────────

    private static async Task<IResult> GetInsulinsAsync(AppDbContext db)
        => Results.Ok(await db.UserInsulins.OrderBy(i => i.InsulinType).ThenBy(i => i.Name).ToListAsync());

    private static async Task<IResult> CreateInsulinAsync(
        InsulinProfileRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var ins = new UserInsulin
        {
            Id = Guid.NewGuid(), UserId = currentUser.UserId,
            Name = req.Name, InsulinType = req.InsulinType,
            TypicalDose = req.TypicalDose, IsActive = req.IsActive, Note = req.Note,
            Brand = req.Brand, PeakMinutes = req.PeakMinutes, DiaHours = req.DiaHours,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.UserInsulins.Add(ins);
        await db.SaveChangesAsync();
        return Results.Ok(ins);
    }

    private static async Task<IResult> UpdateInsulinAsync(
        Guid id, InsulinProfileRequest req, AppDbContext db)
    {
        var ins = await db.UserInsulins.FindAsync(id);
        if (ins is null) return Results.NotFound();
        ins.Name = req.Name; ins.InsulinType = req.InsulinType;
        ins.TypicalDose = req.TypicalDose; ins.IsActive = req.IsActive; ins.Note = req.Note;
        ins.Brand = req.Brand; ins.PeakMinutes = req.PeakMinutes; ins.DiaHours = req.DiaHours;
        ins.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(ins);
    }

    private static async Task<IResult> DeleteInsulinAsync(Guid id, AppDbContext db)
    {
        var ins = await db.UserInsulins.FindAsync(id);
        if (ins is null) return Results.NotFound();
        ins.IsDeleted = true; ins.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
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
        double DailyFat, double DailyCarbs, bool DisclaimerAccepted,
        double DiaHours = 4.0);

    private record TherapyRequest(
        TimeOnly FromTime, TimeOnly ToTime,
        double InsulinToCarbRatio, double InsulinSensitivityFactor);

    private record InsulinProfileRequest(
        string Name, int InsulinType, double? TypicalDose, bool IsActive, string? Note,
        int Brand = 0, int PeakMinutes = 75, double DiaHours = 4.0);
}
