using GlucoTrack.Shared.Calculations;
using GlucoTrack.Shared.DTOs.Sync;
using GlucoTrack.Shared.Entities;

namespace GlucoTrack.Shared.Events;

public enum GlucoseAlertKind
{
    None,          // сахар в норме либо недавно уже был болюс
    Correction,    // выше нормы, рекомендуется коррекционный укол
    PendingBolus,  // выше нормы, но скоро плановый болюс — коррекция учтётся в нём
    Critical,      // критически высокий — коррекция сейчас, приём пищи отодвинуть
    CorrectionDone,// выше/критически высокий, но коррекция уже введена в ответ на этот замер
    Low,           // ниже нормы (гипогликемия) — съесть быстрые углеводы, инсулин не вводить
    CriticalLow    // тяжёлая гипогликемия — немедленно быстрые углеводы / помощь, риск для жизни
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

    /// <summary>Нижняя граница нормы по умолчанию (гипогликемия), ммоль/л.</summary>
    public const double DefaultTargetLowMmol = 3.9;

    /// <summary>Порог тяжёлой гипогликемии — немедленная угроза жизни, ммоль/л.</summary>
    public const double CriticalLowGlucoseMmol = 3.0;

    public static GlucoseAlert Decide(
        double latestGlucose,
        double targetHigh,
        IEnumerable<InsulinInjectionDto> injections,
        IEnumerable<PlannedEventDto> plannedEvents,
        DateTime nowUtc,
        double criticalMmol = CriticalGlucoseMmol,
        double recentBolusHours = 2,
        double pendingWindowMinutes = 120,
        double? correctionTarget = null,
        double? insulinSensitivityFactor = null,
        double diaHours = InsulinOnBoard.DefaultDiaHours,
        DateTime? latestGlucoseAtUtc = null,
        double targetLow = DefaultTargetLowMmol,
        double criticalLowMmol = CriticalLowGlucoseMmol)
    {
        // Гипогликемия — острая угроза, проверяем ПЕРВОЙ (важнее любого высокого сахара):
        // низкий сахар требует немедленных быстрых углеводов, инсулин противопоказан.
        if (latestGlucose < targetLow)
            return new GlucoseAlert(
                latestGlucose <= criticalLowMmol ? GlucoseAlertKind.CriticalLow : GlucoseAlertKind.Low,
                latestGlucose, null);

        if (latestGlucose <= targetHigh)
            return new GlucoseAlert(GlucoseAlertKind.None, latestGlucose, null);

        var bolusInjections = injections
            .Where(i => !i.IsDeleted && i.Units > 0 && (InsulinType)i.InsulinType == InsulinType.Bolus)
            .Select(i => (i.InjectedAtUtc, i.Units, i.ExtendedDurationHours));

        // Точный расчёт покрытия по активному инсулину (если заданы коэффициенты терапии):
        // сравниваем IOB с дозой, которая реально нужна, чтобы снизить текущий сахар до цели.
        bool preciseCoverage = correctionTarget is > 0 && insulinSensitivityFactor is > 0;
        if (preciseCoverage)
        {
            var iob = InsulinOnBoard.Calculate(bolusInjections, nowUtc, diaHours);
            var neededCorrection = (latestGlucose - correctionTarget!.Value) / insulinSensitivityFactor!.Value;
            // Уже введённая коррекция покрывает нужную дозу — гасим алерт, в т.ч. критический:
            // повторный укол поверх достаточного IOB опаснее (стэкинг → отложенная гипогликемия),
            // чем ожидание действия уже введённого инсулина. Если же IOB не покрывает (доза
            // оказалась мала, а сахар критический) — алерт ниже всё равно сработает.
            if (iob >= neededCorrection)
                return new GlucoseAlert(GlucoseAlertKind.None, latestGlucose, null);
        }

        // Коррекция уже введена в ответ на ЭТОТ замер — болюс с временем не раньше самого замера
        // (а не старый болюс на еду до него). Тогда не кричим «коли сейчас», а подтверждаем факт
        // ввода: повторный укол поверх свежей коррекции опаснее. Покрытие по IOB может быть
        // неполным (доза распалась/округлилась/занижена), но пользователь уже среагировал.
        bool correctionGivenInResponse = latestGlucoseAtUtc is { } readingAt
            && bolusInjections.Any(b => b.InjectedAtUtc >= readingAt
                && (nowUtc - b.InjectedAtUtc).TotalHours >= 0
                && (nowUtc - b.InjectedAtUtc).TotalHours <= recentBolusHours);
        if (correctionGivenInResponse)
            return new GlucoseAlert(GlucoseAlertKind.CorrectionDone, latestGlucose, null);

        // Критически высокий — пробивает грубую блокировку по времени и предстоящий плановый
        // болюс: без коэффициентов терапии нельзя достоверно оценить, покрыл ли недавний болюс
        // рост сахара, а риск молчания (DKA, кетоацидоз) выше риска ложного предупреждения.
        if (latestGlucose >= criticalMmol)
            return new GlucoseAlert(GlucoseAlertKind.Critical, latestGlucose, null);

        // Не критический высокий без коэффициентов — грубый фоллбэк по времени:
        // недавний болюс (в пределах recentBolusHours) считаем достаточным покрытием.
        if (!preciseCoverage)
        {
            var recentBolus = bolusInjections.Any(b =>
                (nowUtc - b.InjectedAtUtc).TotalHours >= 0 && (nowUtc - b.InjectedAtUtc).TotalHours <= recentBolusHours);
            if (recentBolus)
                return new GlucoseAlert(GlucoseAlertKind.None, latestGlucose, null);
        }

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
