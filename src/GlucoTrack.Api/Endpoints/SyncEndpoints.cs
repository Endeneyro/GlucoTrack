using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using GlucoTrack.Shared.DTOs.Sync;
using GlucoTrack.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Endpoints;

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sync").RequireAuthorization();
        group.MapGet("/pull", PullAsync);
        group.MapPost("/push", PushAsync);
        return app;
    }

    private static async Task<IResult> PullAsync(long? since, AppDbContext db, ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId;
        var sinceTime = since.HasValue ? new DateTime(since.Value, DateTimeKind.Utc) : DateTime.MinValue;

        var meals = await db.MealEntries.IgnoreQueryFilters()
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .Select(e => new MealEntryDto(e.Id, e.Date, e.Time, (int)e.MealType,
                e.ProductId, e.Grams, e.UpdatedAtUtc, e.IsDeleted, e.LinkedEventId))
            .ToListAsync();

        var glucose = await db.GlucoseReadings.IgnoreQueryFilters()
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .Select(e => new GlucoseReadingDto(e.Id, e.MeasuredAtUtc, e.ValueMmol,
                e.UpdatedAtUtc, e.IsDeleted, e.LinkedEventId))
            .ToListAsync();

        var insulin = await db.InsulinInjections.IgnoreQueryFilters()
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .Select(e => new InsulinInjectionDto(e.Id, e.InjectedAtUtc, e.Units,
                (int)e.InsulinType, e.Carbs, e.GlucoseBefore, e.UpdatedAtUtc, e.IsDeleted, e.LinkedEventId))
            .ToListAsync();

        // Pull ALL own + base products (non-incremental). Products are a small reference
        // set that rarely changes; pulling everything avoids losing edits whose client-side
        // UpdatedAtUtc ends up older than a client's server-time sync cursor (clock skew).
        var products = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Ingredients)
                .ThenInclude(i => i.IngredientProduct)
            .Where(e => e.UserId == userId || e.UserId == null)
            .ToListAsync();

        var productDtos = products.Select(MapProductToDto).ToList();

        var coefficients = await db.TherapyCoefficients.IgnoreQueryFilters()
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .Select(e => new TherapyCoeffDto(e.Id, e.FromTime, e.ToTime,
                e.InsulinToCarbRatio, e.InsulinSensitivityFactor, e.UpdatedAtUtc, e.IsDeleted))
            .ToListAsync();

        var settings = await db.UserSettings.IgnoreQueryFilters()
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .OrderByDescending(e => e.UpdatedAtUtc)
            .Select(e => new UserSettingsDto(e.Id, e.TargetGlucoseLow, e.TargetGlucoseHigh,
                e.TargetGlucose, e.XeGrams, e.DailyCalories, e.DailyProtein, e.DailyFat,
                e.DailyCarbs, e.DisclaimerAccepted, e.UpdatedAtUtc, e.IsDeleted, e.DiaHours))
            .FirstOrDefaultAsync();

        var profile = await db.UserProfiles.IgnoreQueryFilters()
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .OrderByDescending(e => e.UpdatedAtUtc)
            .Select(e => new UserProfileDto(e.Id, e.HeightCm, e.WeightKg, e.DateOfBirth,
                e.Gender, e.DiabetesType, e.DiagnosisYear, e.UpdatedAtUtc, e.IsDeleted))
            .FirstOrDefaultAsync();

        var insulins = await db.UserInsulins.IgnoreQueryFilters()
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .Select(e => new UserInsulinDto(e.Id, e.Name, e.InsulinType, e.TypicalDose,
                e.IsActive, e.Note, e.UpdatedAtUtc, e.IsDeleted))
            .ToListAsync();

        return Results.Ok(new SyncPullResponse(
            DateTime.UtcNow, meals, glucose, insulin, productDtos, coefficients, settings, profile, insulins));
    }

    private static async Task<IResult> PushAsync(
        SyncPushRequest request, AppDbContext db, ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId;
        var conflicts = new List<Guid>();
        int applied = 0;

        applied += await ApplyMealEntries(request.MealEntries, userId, db, conflicts);
        applied += await ApplyGlucoseReadings(request.GlucoseReadings, userId, db, conflicts);
        applied += await ApplyInsulinInjections(request.InsulinInjections, userId, db, conflicts);
        applied += await ApplyProducts(request.Products, userId, db, conflicts);
        applied += await ApplyTherapyCoefficients(request.TherapyCoefficients, userId, db, conflicts);

        if (request.UserSettings is { } s)
            applied += await ApplyUserSettings(s, userId, db, conflicts);
        if (request.UserProfile is { } p)
            applied += await ApplyUserProfile(p, userId, db, conflicts);
        if (request.UserInsulins is { Count: > 0 } ins)
            applied += await ApplyUserInsulins(ins, userId, db, conflicts);

        await db.SaveChangesAsync();
        return Results.Ok(new SyncPushResponse(conflicts, applied));
    }

    // ── Apply helpers ─────────────────────────────────────────────────────────

    private static async Task<int> ApplyMealEntries(
        List<MealEntryDto> dtos, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.MealEntries.IgnoreQueryFilters()
            .Where(e => ids.Contains(e.Id) && e.UserId == userId)
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            if (!existing.TryGetValue(dto.Id, out var row))
            {
                db.MealEntries.Add(new MealEntry
                {
                    Id = dto.Id, UserId = userId, Date = dto.Date, Time = dto.Time,
                    MealType = (MealType)dto.MealType, ProductId = dto.ProductId,
                    Grams = dto.Grams, UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted,
                    LinkedEventId = dto.LinkedEventId
                });
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.Date = dto.Date; row.Time = dto.Time;
                row.MealType = (MealType)dto.MealType; row.ProductId = dto.ProductId;
                row.Grams = dto.Grams; row.UpdatedAtUtc = dto.UpdatedAtUtc;
                row.IsDeleted = dto.IsDeleted; row.LinkedEventId = dto.LinkedEventId;
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    private static async Task<int> ApplyGlucoseReadings(
        List<GlucoseReadingDto> dtos, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.GlucoseReadings.IgnoreQueryFilters()
            .Where(e => ids.Contains(e.Id) && e.UserId == userId)
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            if (!existing.TryGetValue(dto.Id, out var row))
            {
                db.GlucoseReadings.Add(new GlucoseReading
                {
                    Id = dto.Id, UserId = userId, MeasuredAtUtc = dto.MeasuredAtUtc,
                    ValueMmol = dto.ValueMmol, UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted,
                    LinkedEventId = dto.LinkedEventId
                });
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.MeasuredAtUtc = dto.MeasuredAtUtc; row.ValueMmol = dto.ValueMmol;
                row.UpdatedAtUtc = dto.UpdatedAtUtc; row.IsDeleted = dto.IsDeleted;
                row.LinkedEventId = dto.LinkedEventId;
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    private static async Task<int> ApplyInsulinInjections(
        List<InsulinInjectionDto> dtos, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.InsulinInjections.IgnoreQueryFilters()
            .Where(e => ids.Contains(e.Id) && e.UserId == userId)
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            if (!existing.TryGetValue(dto.Id, out var row))
            {
                db.InsulinInjections.Add(new InsulinInjection
                {
                    Id = dto.Id, UserId = userId, InjectedAtUtc = dto.InjectedAtUtc,
                    Units = dto.Units, InsulinType = (InsulinType)dto.InsulinType,
                    Carbs = dto.Carbs, GlucoseBefore = dto.GlucoseBefore,
                    UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted,
                    LinkedEventId = dto.LinkedEventId
                });
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.InjectedAtUtc = dto.InjectedAtUtc; row.Units = dto.Units;
                row.InsulinType = (InsulinType)dto.InsulinType; row.Carbs = dto.Carbs;
                row.GlucoseBefore = dto.GlucoseBefore; row.UpdatedAtUtc = dto.UpdatedAtUtc;
                row.IsDeleted = dto.IsDeleted; row.LinkedEventId = dto.LinkedEventId;
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    private static async Task<int> ApplyProducts(
        List<ProductDto> dtos, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Ingredients)
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            // Cannot modify base products
            if (existing.TryGetValue(dto.Id, out var row) && row.UserId == null)
            { conflicts.Add(dto.Id); continue; }

            if (row == null)
            {
                var p = MapDtoToProduct(dto, userId);
                db.Products.Add(p);
                if (dto.Ingredients?.Count > 0)
                    foreach (var ing in dto.Ingredients)
                        db.ProductIngredients.Add(new ProductIngredient
                        {
                            Id = ing.Id, CompositeProductId = p.Id,
                            IngredientProductId = ing.IngredientProductId, Grams = ing.Grams
                        });
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                ApplyDtoToProduct(dto, row);
                // Sync ingredients: remove all then re-add
                db.ProductIngredients.RemoveRange(row.Ingredients);
                if (dto.Ingredients?.Count > 0)
                    foreach (var ing in dto.Ingredients)
                        db.ProductIngredients.Add(new ProductIngredient
                        {
                            Id = ing.Id, CompositeProductId = row.Id,
                            IngredientProductId = ing.IngredientProductId, Grams = ing.Grams
                        });
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    private static async Task<int> ApplyTherapyCoefficients(
        List<TherapyCoeffDto> dtos, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.TherapyCoefficients.IgnoreQueryFilters()
            .Where(e => ids.Contains(e.Id) && e.UserId == userId)
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            if (!existing.TryGetValue(dto.Id, out var row))
            {
                db.TherapyCoefficients.Add(new TherapyCoefficient
                {
                    Id = dto.Id, UserId = userId, FromTime = dto.FromTime, ToTime = dto.ToTime,
                    InsulinToCarbRatio = dto.InsulinToCarbRatio,
                    InsulinSensitivityFactor = dto.InsulinSensitivityFactor,
                    UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted
                });
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.FromTime = dto.FromTime; row.ToTime = dto.ToTime;
                row.InsulinToCarbRatio = dto.InsulinToCarbRatio;
                row.InsulinSensitivityFactor = dto.InsulinSensitivityFactor;
                row.UpdatedAtUtc = dto.UpdatedAtUtc; row.IsDeleted = dto.IsDeleted;
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    private static async Task<int> ApplyUserSettings(
        UserSettingsDto dto, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        var row = await db.UserSettings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.UserId == userId);

        if (row == null)
        {
            db.UserSettings.Add(new UserSettings
            {
                Id = dto.Id, UserId = userId,
                TargetGlucoseLow = dto.TargetGlucoseLow, TargetGlucoseHigh = dto.TargetGlucoseHigh,
                TargetGlucose = dto.TargetGlucose, XeGrams = dto.XeGrams,
                DailyCalories = dto.DailyCalories, DailyProtein = dto.DailyProtein,
                DailyFat = dto.DailyFat, DailyCarbs = dto.DailyCarbs,
                DisclaimerAccepted = dto.DisclaimerAccepted,
                DiaHours = dto.DiaHours > 0 ? dto.DiaHours : 4.0,
                UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted
            });
            return 1;
        }

        if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
        {
            row.TargetGlucoseLow = dto.TargetGlucoseLow; row.TargetGlucoseHigh = dto.TargetGlucoseHigh;
            row.TargetGlucose = dto.TargetGlucose; row.XeGrams = dto.XeGrams;
            row.DailyCalories = dto.DailyCalories; row.DailyProtein = dto.DailyProtein;
            row.DailyFat = dto.DailyFat; row.DailyCarbs = dto.DailyCarbs;
            row.DisclaimerAccepted = dto.DisclaimerAccepted;
            row.DiaHours = dto.DiaHours > 0 ? dto.DiaHours : 4.0;
            row.UpdatedAtUtc = dto.UpdatedAtUtc; row.IsDeleted = dto.IsDeleted;
            return 1;
        }

        conflicts.Add(dto.Id);
        return 0;
    }

    private static async Task<int> ApplyUserProfile(
        UserProfileDto dto, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        var row = await db.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.UserId == userId);
        if (row == null)
        {
            db.UserProfiles.Add(new UserProfile
            {
                Id = dto.Id, UserId = userId,
                HeightCm = dto.HeightCm, WeightKg = dto.WeightKg,
                DateOfBirth = dto.DateOfBirth, Gender = dto.Gender,
                DiabetesType = dto.DiabetesType, DiagnosisYear = dto.DiagnosisYear,
                UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted
            });
            return 1;
        }
        if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
        {
            row.HeightCm = dto.HeightCm; row.WeightKg = dto.WeightKg;
            row.DateOfBirth = dto.DateOfBirth; row.Gender = dto.Gender;
            row.DiabetesType = dto.DiabetesType; row.DiagnosisYear = dto.DiagnosisYear;
            row.UpdatedAtUtc = dto.UpdatedAtUtc; row.IsDeleted = dto.IsDeleted;
            return 1;
        }
        conflicts.Add(dto.Id);
        return 0;
    }

    private static async Task<int> ApplyUserInsulins(
        List<UserInsulinDto> dtos, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.UserInsulins.IgnoreQueryFilters()
            .Where(e => ids.Contains(e.Id) && e.UserId == userId)
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            if (!existing.TryGetValue(dto.Id, out var row))
            {
                db.UserInsulins.Add(new UserInsulin
                {
                    Id = dto.Id, UserId = userId, Name = dto.Name,
                    InsulinType = dto.InsulinType, TypicalDose = dto.TypicalDose,
                    IsActive = dto.IsActive, Note = dto.Note,
                    UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted
                });
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.Name = dto.Name; row.InsulinType = dto.InsulinType;
                row.TypicalDose = dto.TypicalDose; row.IsActive = dto.IsActive;
                row.Note = dto.Note; row.UpdatedAtUtc = dto.UpdatedAtUtc;
                row.IsDeleted = dto.IsDeleted;
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    internal static ProductDto MapProductToDto(Product p) => new(
        p.Id, p.UserId, p.Name,
        (int)p.Category, (int)p.OwnerType, (int)p.MeasureType,
        p.PieceWeightG, p.DefaultServingG,
        p.IsComposite, p.TotalYieldG, p.IsVerified, p.Barcode,
        p.LikesCount, p.DislikesCount,
        p.GlycemicIndex,
        p.CaloriesPer100g, p.ProteinPer100g, p.FatPer100g, p.CarbsPer100g, p.FiberPer100g,
        p.VitaminA, p.VitaminC, p.VitaminD,
        p.VitaminB1, p.VitaminB2, p.VitaminB3, p.VitaminB4, p.VitaminB5, p.VitaminB6, p.VitaminB9, p.VitaminB12,
        p.VitaminE, p.VitaminK, p.VitaminH,
        p.Calcium, p.Phosphorus, p.Potassium, p.Sodium, p.Chlorine, p.Magnesium, p.Sulfur, p.Iron, p.Zinc, p.Selenium, p.Iodine,
        p.Manganese, p.Copper, p.Fluorine, p.Chromium,
        p.Ingredients.Select(i => new ProductIngredientDto(
            i.Id, i.IngredientProductId,
            i.IngredientProduct?.Name ?? string.Empty, i.Grams)).ToList(),
        p.UpdatedAtUtc, p.IsDeleted);

    private static Product MapDtoToProduct(ProductDto dto, Guid userId)
    {
        var p = new Product { Id = dto.Id, UserId = userId };
        ApplyDtoToProduct(dto, p);
        return p;
    }

    private static void ApplyDtoToProduct(ProductDto dto, Product p)
    {
        p.Name = dto.Name;
        p.Category = (ProductCategory)dto.Category;
        p.OwnerType = (ProductOwnerType)dto.OwnerType;
        p.MeasureType = (ProductMeasureType)dto.MeasureType;
        p.PieceWeightG = dto.PieceWeightG;
        p.DefaultServingG = dto.DefaultServingG;
        p.IsComposite = dto.IsComposite;
        p.TotalYieldG = dto.TotalYieldG;
        p.IsVerified = dto.IsVerified;
        p.Barcode = dto.Barcode;
        p.GlycemicIndex = dto.GlycemicIndex;
        p.CaloriesPer100g = dto.CaloriesPer100g; p.ProteinPer100g = dto.ProteinPer100g;
        p.FatPer100g = dto.FatPer100g; p.CarbsPer100g = dto.CarbsPer100g;
        p.FiberPer100g = dto.FiberPer100g;
        p.VitaminA = dto.VitaminA; p.VitaminC = dto.VitaminC; p.VitaminD = dto.VitaminD;
        p.VitaminB1 = dto.VitaminB1; p.VitaminB2 = dto.VitaminB2; p.VitaminB3 = dto.VitaminB3;
        p.VitaminB4 = dto.VitaminB4; p.VitaminB5 = dto.VitaminB5; p.VitaminB6 = dto.VitaminB6;
        p.VitaminB9 = dto.VitaminB9; p.VitaminB12 = dto.VitaminB12;
        p.VitaminE = dto.VitaminE; p.VitaminK = dto.VitaminK; p.VitaminH = dto.VitaminH;
        p.Calcium = dto.Calcium; p.Phosphorus = dto.Phosphorus; p.Potassium = dto.Potassium;
        p.Sodium = dto.Sodium; p.Chlorine = dto.Chlorine; p.Magnesium = dto.Magnesium;
        p.Sulfur = dto.Sulfur; p.Iron = dto.Iron; p.Zinc = dto.Zinc; p.Selenium = dto.Selenium; p.Iodine = dto.Iodine;
        p.Manganese = dto.Manganese; p.Copper = dto.Copper; p.Fluorine = dto.Fluorine; p.Chromium = dto.Chromium;
        p.UpdatedAtUtc = dto.UpdatedAtUtc; p.IsDeleted = dto.IsDeleted;
    }
}
