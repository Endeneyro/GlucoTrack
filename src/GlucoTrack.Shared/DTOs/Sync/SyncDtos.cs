namespace GlucoTrack.Shared.DTOs.Sync;

// ── Entity DTOs ──────────────────────────────────────────────────────────────

public record MealEntryDto(
    Guid Id,
    DateOnly Date,
    TimeOnly Time,
    int MealType,
    Guid ProductId,
    double Grams,
    DateTime UpdatedAtUtc,
    bool IsDeleted,
    Guid? LinkedEventId = null);

public record GlucoseReadingDto(
    Guid Id,
    DateTime MeasuredAtUtc,
    double ValueMmol,
    DateTime UpdatedAtUtc,
    bool IsDeleted,
    Guid? LinkedEventId = null);

// Planned events are stored client-side (IndexedDB "planned_events") and synced to the
// server like the other entities below; the DTOs live here in Shared so the
// calculation/reconciliation logic and its tests can use them too.
public record PlannedEventDto(
    Guid Id,
    DateTime PlannedAtUtc,
    int EventType,
    string? Note,
    Guid? GroupId,
    List<PlannedMealItem>? MealItems,
    bool IsDone,
    DateTime UpdatedAtUtc,
    bool IsDeleted,
    int? InsulinSubtype = null  // 0=болюс, 1=базал; null для не-инсулиновых событий
);

public record PlannedMealItem(Guid ProductId, string ProductName, double Grams, int MeasureType = 0, double? PieceWeightG = null);

public enum PlannedEventType
{
    Meal     = 0,
    Glucose  = 1,
    Insulin  = 2,
    Weight   = 3,
    Activity = 4
}

public record InsulinInjectionDto(
    Guid Id,
    DateTime InjectedAtUtc,
    double Units,
    int InsulinType,
    double? Carbs,
    double? GlucoseBefore,
    DateTime UpdatedAtUtc,
    bool IsDeleted,
    Guid? LinkedEventId = null,
    double? ExtendedDurationHours = null);

public record ProductIngredientDto(
    Guid Id,
    Guid IngredientProductId,
    string IngredientName,
    double Grams);

public record ProductDto(
    Guid Id,
    Guid? UserId,
    string Name,
    int Category,
    int OwnerType,
    int MeasureType,
    double? PieceWeightG,
    double DefaultServingG,
    bool IsComposite,
    double? TotalYieldG,
    bool IsVerified,
    string? Barcode,
    int LikesCount,
    int DislikesCount,
    int? GlycemicIndex,
    double CaloriesPer100g,
    double ProteinPer100g,
    double FatPer100g,
    double CarbsPer100g,
    double FiberPer100g,
    double? VitaminA,
    double? VitaminC,
    double? VitaminD,
    double? VitaminB1,
    double? VitaminB2,
    double? VitaminB3,
    double? VitaminB4,
    double? VitaminB5,
    double? VitaminB6,
    double? VitaminB9,
    double? VitaminB12,
    double? VitaminE,
    double? VitaminK,
    double? VitaminH,
    double? Calcium,
    double? Phosphorus,
    double? Potassium,
    double? Sodium,
    double? Chlorine,
    double? Magnesium,
    double? Sulfur,
    double? Iron,
    double? Zinc,
    double? Selenium,
    double? Iodine,
    double? Manganese,
    double? Copper,
    double? Fluorine,
    double? Chromium,
    List<ProductIngredientDto>? Ingredients,
    DateTime UpdatedAtUtc,
    bool IsDeleted,
    string? Manufacturer = null,
    bool HasImage = false,
    Guid? ClonedFromProductId = null);

// Search result — includes per-user context (reaction, usage, source)
public record ProductSearchItemDto(
    ProductDto Product,
    int UseCount,
    int? MyReaction,   // null=none 1=like -1=dislike
    string Source,     // "mine" | "base" | "shared"
    bool IsHidden = false);

public record TherapyCoeffDto(
    Guid Id,
    TimeOnly FromTime,
    TimeOnly ToTime,
    double InsulinToCarbRatio,
    double InsulinSensitivityFactor,
    DateTime UpdatedAtUtc,
    bool IsDeleted)
{
    public bool CoversTime(TimeOnly t) =>
        FromTime <= ToTime
            ? t >= FromTime && t < ToTime   // обычный интервал
            : t >= FromTime || t < ToTime;  // через полночь (22:00–06:00)
}

public record UserSettingsDto(
    Guid Id,
    double TargetGlucoseLow,
    double TargetGlucoseHigh,
    double TargetGlucose,
    double XeGrams,
    double DailyCalories,
    double DailyProtein,
    double DailyFat,
    double DailyCarbs,
    bool DisclaimerAccepted,
    DateTime UpdatedAtUtc,
    bool IsDeleted,
    double DiaHours = 4.0);

public record UserProfileDto(
    Guid Id,
    double? HeightCm,
    double? WeightKg,
    DateOnly? DateOfBirth,
    int? Gender,
    int? DiabetesType,
    int? DiagnosisYear,
    DateTime UpdatedAtUtc,
    bool IsDeleted);

public record UserInsulinDto(
    Guid Id,
    string Name,
    int InsulinType,
    double? TypicalDose,
    bool IsActive,
    string? Note,
    DateTime UpdatedAtUtc,
    bool IsDeleted,
    int Brand = 0,
    int PeakMinutes = 75,
    double DiaHours = 4.0);

public record MealTemplateDto(
    Guid Id,
    string Name,
    List<PlannedMealItem> Items,
    DateTime UpdatedAtUtc,
    bool IsDeleted,
    bool HasImage = false);

// ── Sync request/response ────────────────────────────────────────────────────

public record SyncPushRequest(
    List<MealEntryDto> MealEntries,
    List<GlucoseReadingDto> GlucoseReadings,
    List<InsulinInjectionDto> InsulinInjections,
    List<ProductDto> Products,
    List<TherapyCoeffDto> TherapyCoefficients,
    UserSettingsDto? UserSettings,
    UserProfileDto? UserProfile = null,
    List<UserInsulinDto>? UserInsulins = null,
    List<MealTemplateDto>? MealTemplates = null,
    List<PlannedEventDto>? PlannedEvents = null);

public record SyncPushResponse(
    List<Guid> Conflicts,
    int Applied);

public record SyncPullResponse(
    DateTime ServerUtc,
    List<MealEntryDto> MealEntries,
    List<GlucoseReadingDto> GlucoseReadings,
    List<InsulinInjectionDto> InsulinInjections,
    List<ProductDto> Products,
    List<TherapyCoeffDto> TherapyCoefficients,
    UserSettingsDto? UserSettings,
    UserProfileDto? UserProfile = null,
    List<UserInsulinDto>? UserInsulins = null,
    List<MealTemplateDto>? MealTemplates = null,
    List<PlannedEventDto>? PlannedEvents = null);
