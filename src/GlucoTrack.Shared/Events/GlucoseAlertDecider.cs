using GlucoTrack.Shared.DTOs.Sync;
using GlucoTrack.Shared.Entities;

namespace GlucoTrack.Shared.Events;

public enum GlucoseAlertKind
{
    None,        // сахар в норме либо недавно уже был болюс
    Correction,  // выше нормы, рекомендуется коррекционный укол
    PendingBolus,// выше нормы, но скоро плановый болюс — коррекция учтётся в нём
    Critical     // критически высокий — коррекция сейчас, приём пищи отодвинуть
}

public record GlucoseAlert(GlucoseAlertKind Kind, double Glucose, DateTime? PendingBolusUtc);

/// <summary>
/// Решает, какой алерт показать по последнему замеру сахара, учитывая недавно введённый
/// болюс и предстоящий плановый болюс (защита от стакинга инсулина). Чистая логика без UI.
/// </summary>
public static class GlucoseAlertDecider
{
    /// <summary>Порог критически высокого сахара: коррекция сейчас + отодвинуть приём пищи.</summary>
    public const double CriticalGlucoseMmol = 15.0;

    public static GlucoseAlert Decide(
        double latestGlucose,
        double targetHigh,
        IEnumerable<InsulinInjectionDto> injections,
        IEnumerable<PlannedEventDto> plannedEvents,
        DateTime nowUtc,
        double criticalMmol = CriticalGlucoseMmol,
        double recentBolusHours = 2,
        double pendingWindowMinutes = 120)
    {
        if (latestGlucose <= targetHigh)
            return new GlucoseAlert(GlucoseAlertKind.None, latestGlucose, null);

        // Уже введённый болюс в пределах recentBolusHours покрывает коррекцию — алерт не нужен.
        var recentBolus = injections.Any(i =>
            !i.IsDeleted &&
            (InsulinType)i.InsulinType == InsulinType.Bolus &&
            (nowUtc - i.InjectedAtUtc).TotalHours <= recentBolusHours &&
            (nowUtc - i.InjectedAtUtc).TotalHours >= 0);
        if (recentBolus)
            return new GlucoseAlert(GlucoseAlertKind.None, latestGlucose, null);

        // Критически высокий — приоритет над «свернуть в плановый болюс».
        if (latestGlucose >= criticalMmol)
            return new GlucoseAlert(GlucoseAlertKind.Critical, latestGlucose, null);

        // Незавершённый плановый болюс рядом — коррекция учтётся в нём, отдельную не предлагаем.
        var pendingBolus = plannedEvents
            .Where(e => !e.IsDeleted && !e.IsDone
                && (PlannedEventType)e.EventType == PlannedEventType.Insulin
                && Math.Abs((e.PlannedAtUtc - nowUtc).TotalMinutes) <= pendingWindowMinutes)
            .OrderBy(e => Math.Abs((e.PlannedAtUtc - nowUtc).TotalMinutes))
            .FirstOrDefault();
        if (pendingBolus is not null)
            return new GlucoseAlert(GlucoseAlertKind.PendingBolus, latestGlucose, pendingBolus.PlannedAtUtc);

        return new GlucoseAlert(GlucoseAlertKind.Correction, latestGlucose, null);
    }
}
