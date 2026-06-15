namespace GlucoTrack.Client.Services;

/// <summary>
/// Determines which feature modules are active for the current user.
///
/// Default module set is derived from DiabetesType in UserProfile:
///   Type 1 / LADA  → all modules on
///   Type 2         → Glucose + Nutrition, no bolus/IOB
///   null / other   → Nutrition only
///
/// Each flag can be individually overridden by the user via ModuleOverridesDto
/// stored in IndexedDB "modules" store.
/// </summary>
public class ModuleService
{
    private readonly DbService _db;
    private bool _loaded;

    // ── Computed flags ────────────────────────────────────────────────────────

    /// Glucose measurement tracking
    public bool GlucoseModule { get; private set; }

    /// Insulin injection log
    public bool InsulinModule { get; private set; }

    /// Bolus calculator (requires InsulinModule)
    public bool BolusCalc { get; private set; }

    /// IOB / pre-bolus timing / correction logic (requires BolusCalc)
    public bool AdvancedBolusModule { get; private set; }

    /// Nutrition diary + product database (always on)
    public bool NutritionModule => true;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action? OnChanged;

    public ModuleService(DbService db) => _db = db;

    public async Task InitAsync()
    {
        var profile  = (await _db.GetAllAsync<GlucoTrack.Shared.DTOs.Sync.UserProfileDto>("profile")).FirstOrDefault();
        var overrides = (await _db.GetAllAsync<ModuleOverridesDto>("modules")).FirstOrDefault();

        // Defaults by diabetes type
        int? dtype = profile?.DiabetesType;
        bool defGlucose  = dtype is not null;
        bool defInsulin  = dtype is 1 or 3; // СД1 or LADA
        bool defBolus    = defInsulin;
        bool defAdvanced = defInsulin;

        // Apply manual overrides (null = use default)
        GlucoseModule       = overrides?.GlucoseEnabled  ?? defGlucose;
        InsulinModule       = overrides?.InsulinEnabled  ?? defInsulin;
        BolusCalc           = overrides?.BolusCalcEnabled ?? defBolus;
        AdvancedBolusModule = overrides?.AdvancedBolusEnabled ?? defAdvanced;

        // Enforce dependencies
        if (!InsulinModule) { BolusCalc = false; AdvancedBolusModule = false; }
        if (!BolusCalc) AdvancedBolusModule = false;

        _loaded = true;
        OnChanged?.Invoke();
    }

    public async Task SetOverrideAsync(string module, bool? value)
    {
        var existing = (await _db.GetAllAsync<ModuleOverridesDto>("modules")).FirstOrDefault()
                       ?? new ModuleOverridesDto(Guid.NewGuid(), null, null, null, null);

        var updated = module switch
        {
            "glucose"         => existing with { GlucoseEnabled   = value },
            "insulin"         => existing with { InsulinEnabled   = value },
            "bolus"           => existing with { BolusCalcEnabled = value },
            "advanced_bolus"  => existing with { AdvancedBolusEnabled = value },
            _ => existing
        };

        await _db.PutAsync("modules", updated, pending: false);
        await InitAsync();
    }

    public bool IsLoaded => _loaded;
}

public record ModuleOverridesDto(
    Guid Id,
    bool? GlucoseEnabled,
    bool? InsulinEnabled,
    bool? BolusCalcEnabled,
    bool? AdvancedBolusEnabled);
