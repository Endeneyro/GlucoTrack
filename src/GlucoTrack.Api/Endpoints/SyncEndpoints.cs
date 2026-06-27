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
                (int)e.InsulinType, e.Carbs, e.GlucoseBefore, e.UpdatedAtUtc, e.IsDeleted, e.LinkedEventId, e.ExtendedDurationHours))
            .ToListAsync();

        // Pull ALL own products (non-incremental). This is a small per-user set; pulling
        // everything avoids losing edits whose client-side UpdatedAtUtc ends up older than
        // a client's server-time sync cursor (clock skew).
        // The admin-curated base catalog is intentionally NOT bulk-synced here — it can grow
        // unbounded, so it's only ever browsed live via "Базовые" and pulled into a user's
        // own products on demand (cloned) when they actually pick one.
        var products = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Ingredients)
                .ThenInclude(i => i.IngredientProduct)
            .Where(e => e.UserId == userId)
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
                e.IsActive, e.Note, e.UpdatedAtUtc, e.IsDeleted,
                e.Brand, e.PeakMinutes, e.DiaHours))
            .ToListAsync();

        var mealTemplates = await db.MealTemplates.IgnoreQueryFilters()
            .Include(t => t.Items)
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .Select(t => new MealTemplateDto(
                t.Id, t.Name,
                t.Items.Select(i => new PlannedMealItem(i.ProductId, i.ProductName, i.Grams, i.MeasureType, i.PieceWeightG)).ToList(),
                t.UpdatedAtUtc, t.IsDeleted, t.HasImage))
            .ToListAsync();

        var plannedEvents = await db.PlannedEvents.IgnoreQueryFilters()
            .Include(t => t.MealItems)
            .Where(e => e.UserId == userId && e.UpdatedAtUtc > sinceTime)
            .Select(t => new PlannedEventDto(
                t.Id, t.PlannedAtUtc, t.EventType, t.Note, t.GroupId,
                t.MealItems.Select(i => new PlannedMealItem(i.ProductId, i.ProductName, i.Grams, i.MeasureType, i.PieceWeightG)).ToList(),
                t.IsDone, t.UpdatedAtUtc, t.IsDeleted, t.InsulinSubtype))
            .ToListAsync();

        return Results.Ok(new SyncPullResponse(
            DateTime.UtcNow, meals, glucose, insulin, productDtos, coefficients, settings, profile, insulins, mealTemplates, plannedEvents));
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
        applied += await ApplyProducts(request.Products, userId, currentUser.IsAdmin, db, conflicts);
        applied += await ApplyTherapyCoefficients(request.TherapyCoefficients, userId, db, conflicts);

        if (request.UserSettings is { } s)
            applied += await ApplyUserSettings(s, userId, db, conflicts);
        if (request.UserProfile is { } p)
            applied += await ApplyUserProfile(p, userId, db, conflicts);
        if (request.UserInsulins is { Count: > 0 } ins)
            applied += await ApplyUserInsulins(ins, userId, db, conflicts);
        if (request.MealTemplates is { Count: > 0 } tpls)
            applied += await ApplyMealTemplates(tpls, userId, db, conflicts);
        if (request.PlannedEvents is { Count: > 0 } events)
            applied += await ApplyPlannedEvents(events, userId, db, conflicts);

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

        var usedProductIds = new List<Guid>();

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
                if (!dto.IsDeleted) usedProductIds.Add(dto.ProductId);
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

        if (usedProductIds.Count > 0)
            await IncrementProductUsageAsync(db, userId, usedProductIds);

        return count;
    }

    // Bumps the per-user "times used" counter that powers the "Часто используемые" filter.
    private static async Task IncrementProductUsageAsync(AppDbContext db, Guid userId, List<Guid> productIds)
    {
        var counts = productIds.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var existingUsages = await db.ProductUsages.IgnoreQueryFilters()
            .Where(u => u.UserId == userId && counts.Keys.Contains(u.ProductId))
            .ToDictionaryAsync(u => u.ProductId);

        foreach (var (productId, addCount) in counts)
        {
            if (existingUsages.TryGetValue(productId, out var usage))
            {
                usage.UseCount += addCount;
                usage.LastUsedAt = DateTime.UtcNow;
            }
            else
            {
                db.ProductUsages.Add(new ProductUsage
                {
                    UserId = userId, ProductId = productId,
                    UseCount = addCount, LastUsedAt = DateTime.UtcNow
                });
            }
        }
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
                    LinkedEventId = dto.LinkedEventId,
                    ExtendedDurationHours = dto.ExtendedDurationHours
                });
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.InjectedAtUtc = dto.InjectedAtUtc; row.Units = dto.Units;
                row.InsulinType = (InsulinType)dto.InsulinType; row.Carbs = dto.Carbs;
                row.GlucoseBefore = dto.GlucoseBefore; row.UpdatedAtUtc = dto.UpdatedAtUtc;
                row.IsDeleted = dto.IsDeleted; row.LinkedEventId = dto.LinkedEventId;
                row.ExtendedDurationHours = dto.ExtendedDurationHours;
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    private static async Task<int> ApplyProducts(
        List<ProductDto> dtos, Guid userId, bool isAdmin, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Ingredients)
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            // Cannot modify base products — unless you're the admin who curates them.
            if (existing.TryGetValue(dto.Id, out var row) && row.UserId == null && !isAdmin)
            { conflicts.Add(dto.Id); continue; }

            if (row == null)
            {
                // Admins curate the shared base catalog: their new products belong to no
                // one specifically (UserId = null) and are visible to everyone as "Базовые".
                var p = isAdmin ? MapDtoToProduct(dto, null) : MapDtoToProduct(dto, userId);
                if (isAdmin) p.OwnerType = ProductOwnerType.Base;
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
                // The client always sends OwnerType=Private (it doesn't know about Base) —
                // don't let that clobber an existing base product's OwnerType.
                if (row.UserId is null) row.OwnerType = ProductOwnerType.Base;
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
                    Brand = dto.Brand, PeakMinutes = dto.PeakMinutes, DiaHours = dto.DiaHours,
                    UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted
                });
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.Name = dto.Name; row.InsulinType = dto.InsulinType;
                row.TypicalDose = dto.TypicalDose; row.IsActive = dto.IsActive;
                row.Note = dto.Note; row.UpdatedAtUtc = dto.UpdatedAtUtc;
                row.Brand = dto.Brand; row.PeakMinutes = dto.PeakMinutes; row.DiaHours = dto.DiaHours;
                row.IsDeleted = dto.IsDeleted;
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    private static async Task<int> ApplyMealTemplates(
        List<MealTemplateDto> dtos, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        // A push can contain duplicate Ids for the same template (e.g. an edit queued before
        // the previous save synced) — keep only the latest one, otherwise two inserts with the
        // same Id race against db.MealTemplates' PK and PostgreSQL throws 23505.
        dtos = dtos.GroupBy(d => d.Id).Select(g => g.OrderByDescending(d => d.UpdatedAtUtc).First()).ToList();
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.MealTemplates.IgnoreQueryFilters()
            .Include(t => t.Items)
            .Where(e => ids.Contains(e.Id) && e.UserId == userId)
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            if (!existing.TryGetValue(dto.Id, out var row))
            {
                var t = new MealTemplate { Id = dto.Id, UserId = userId, Name = dto.Name,
                    UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted };
                foreach (var item in dto.Items)
                    t.Items.Add(new MealTemplateItem
                    {
                        Id = Guid.NewGuid(), MealTemplateId = t.Id, ProductId = item.ProductId,
                        ProductName = item.ProductName, Grams = item.Grams,
                        MeasureType = item.MeasureType, PieceWeightG = item.PieceWeightG
                    });
                db.MealTemplates.Add(t);
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.Name = dto.Name; row.UpdatedAtUtc = dto.UpdatedAtUtc; row.IsDeleted = dto.IsDeleted;
                db.MealTemplateItems.RemoveRange(row.Items);
                foreach (var item in dto.Items)
                    db.MealTemplateItems.Add(new MealTemplateItem
                    {
                        Id = Guid.NewGuid(), MealTemplateId = row.Id, ProductId = item.ProductId,
                        ProductName = item.ProductName, Grams = item.Grams,
                        MeasureType = item.MeasureType, PieceWeightG = item.PieceWeightG
                    });
                count++;
            }
            else conflicts.Add(dto.Id);
        }
        return count;
    }

    private static async Task<int> ApplyPlannedEvents(
        List<PlannedEventDto> dtos, Guid userId, AppDbContext db, List<Guid> conflicts)
    {
        int count = 0;
        // Dedupe by Id: a push can contain duplicate Ids for the same event (e.g. an edit
        // queued before the previous save synced) — keep only the latest one, otherwise two
        // inserts with the same Id race against the PK and PostgreSQL throws 23505.
        dtos = dtos.GroupBy(d => d.Id).Select(g => g.OrderByDescending(d => d.UpdatedAtUtc).First()).ToList();
        var ids = dtos.Select(d => d.Id).ToList();
        var existing = await db.PlannedEvents.IgnoreQueryFilters()
            .Include(t => t.MealItems)
            .Where(e => ids.Contains(e.Id) && e.UserId == userId)
            .ToDictionaryAsync(e => e.Id);

        foreach (var dto in dtos)
        {
            if (!existing.TryGetValue(dto.Id, out var row))
            {
                var t = new PlannedEvent
                {
                    Id = dto.Id, UserId = userId, PlannedAtUtc = dto.PlannedAtUtc,
                    EventType = dto.EventType, Note = dto.Note, GroupId = dto.GroupId,
                    IsDone = dto.IsDone, InsulinSubtype = dto.InsulinSubtype,
                    UpdatedAtUtc = dto.UpdatedAtUtc, IsDeleted = dto.IsDeleted
                };
                foreach (var item in dto.MealItems ?? [])
                    t.MealItems.Add(new PlannedEventMealItem
                    {
                        Id = Guid.NewGuid(), PlannedEventId = t.Id, ProductId = item.ProductId,
                        ProductName = item.ProductName, Grams = item.Grams,
                        MeasureType = item.MeasureType, PieceWeightG = item.PieceWeightG
                    });
                db.PlannedEvents.Add(t);
                count++;
            }
            else if (dto.UpdatedAtUtc >= row.UpdatedAtUtc)
            {
                row.PlannedAtUtc = dto.PlannedAtUtc; row.EventType = dto.EventType;
                row.Note = dto.Note; row.GroupId = dto.GroupId; row.IsDone = dto.IsDone;
                row.InsulinSubtype = dto.InsulinSubtype;
                row.UpdatedAtUtc = dto.UpdatedAtUtc; row.IsDeleted = dto.IsDeleted;
                db.PlannedEventMealItems.RemoveRange(row.MealItems);
                foreach (var item in dto.MealItems ?? [])
                    db.PlannedEventMealItems.Add(new PlannedEventMealItem
                    {
                        Id = Guid.NewGuid(), PlannedEventId = row.Id, ProductId = item.ProductId,
                        ProductName = item.ProductName, Grams = item.Grams,
                        MeasureType = item.MeasureType, PieceWeightG = item.PieceWeightG
                    });
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
        p.UpdatedAtUtc, p.IsDeleted, p.Manufacturer, p.HasImage, p.ClonedFromProductId);

    private static Product MapDtoToProduct(ProductDto dto, Guid? userId)
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
        p.Manufacturer = dto.Manufacturer;
        p.UpdatedAtUtc = dto.UpdatedAtUtc; p.IsDeleted = dto.IsDeleted;
    }
}
