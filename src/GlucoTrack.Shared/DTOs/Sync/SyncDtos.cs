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
    bool IsDeleted);

public record InsulinInjectionDto(
    Guid Id,
    DateTime InjectedAtUtc,
    double Units,
    int InsulinType,
    double? Carbs,
    double? GlucoseBefore,
    DateTime UpdatedAtUtc,
    bool IsDeleted);

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
    bool IsDeleted);

// Search result — includes per-user context (reaction, usage, source)
public record ProductSearchItemDto(
    ProductDto Product,
    int UseCount,
    int? MyReaction,   // null=none 1=like -1=dislike
    string Source);    // "mine" | "base" | "shared"

public record TherapyCoeffDto(
    Guid Id,
    TimeOnly FromTime,
    TimeOnly ToTime,
    double InsulinToCarbRatio,
    double InsulinSensitivityFactor,
    DateTime UpdatedAtUtc,
    bool IsDeleted);

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
    bool IsDeleted);

// ── Sync request/response ────────────────────────────────────────────────────

public record SyncPushRequest(
    List<MealEntryDto> MealEntries,
    List<GlucoseReadingDto> GlucoseReadings,
    List<InsulinInjectionDto> InsulinInjections,
    List<ProductDto> Products,
    List<TherapyCoeffDto> TherapyCoefficients,
    UserSettingsDto? UserSettings);

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
    UserSettingsDto? UserSettings);
