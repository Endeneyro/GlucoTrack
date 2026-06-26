using GlucoTrack.Shared.DTOs.Sync;
using GlucoTrack.Shared.Entities;
using GlucoTrack.Shared.Events;

namespace GlucoTrack.Shared.Tests;

public class GlucoseAlertDeciderTests
{
    private static readonly DateTime Now = new(2026, 6, 13, 15, 0, 0, DateTimeKind.Utc);
    private const double TargetHigh = 10.0;

    private static InsulinInjectionDto Bolus(double hoursAgo) =>
        new(Guid.NewGuid(), Now.AddHours(-hoursAgo), 4, (int)InsulinType.Bolus, null, null, Now, false);

    private static PlannedEventDto PlannedInsulin(double minutesFromNow, bool done = false) =>
        new(Guid.NewGuid(), Now.AddMinutes(minutesFromNow), (int)PlannedEventType.Insulin,
            null, null, null, done, Now, false);

    private static GlucoseAlert Decide(double glucose,
        IEnumerable<InsulinInjectionDto>? inj = null,
        IEnumerable<PlannedEventDto>? events = null) =>
        GlucoseAlertDecider.Decide(glucose, TargetHigh, inj ?? [], events ?? [], Now);

    [Fact]
    public void AtOrBelowTarget_None()
    {
        Assert.Equal(GlucoseAlertKind.None, Decide(10.0).Kind);
        Assert.Equal(GlucoseAlertKind.None, Decide(8.0).Kind);
    }

    [Fact]
    public void HighNoBolusNoPlan_Correction()
    {
        Assert.Equal(GlucoseAlertKind.Correction, Decide(12.0).Kind);
    }

    [Fact]
    public void RecentBolusWithin2h_SuppressesCorrection()
    {
        Assert.Equal(GlucoseAlertKind.None, Decide(12.0, inj: new[] { Bolus(1.5) }).Kind);
    }

    [Fact]
    public void RecentBolusWithin2h_DoesNotSuppressCritical()
    {
        // A recent bolus that clearly didn't cover the rise to a critical level must not
        // silence the alert — the risk of staying silent outweighs a false stacking warning.
        Assert.Equal(GlucoseAlertKind.Critical, Decide(18.0, inj: new[] { Bolus(1.5) }).Kind);
    }

    [Fact]
    public void ZeroDoseBolus_DoesNotSuppress()
    {
        var zeroBolus = new InsulinInjectionDto(Guid.NewGuid(), Now.AddHours(-0.5), 0, (int)InsulinType.Bolus, null, null, Now, false);
        Assert.Equal(GlucoseAlertKind.Correction, Decide(12.0, inj: new[] { zeroBolus }).Kind);
    }

    [Fact]
    public void BolusOlderThan2h_DoesNotSuppress()
    {
        Assert.Equal(GlucoseAlertKind.Correction, Decide(12.0, inj: new[] { Bolus(2.5) }).Kind);
    }

    [Fact]
    public void Critical_AtExactly15()
    {
        Assert.Equal(GlucoseAlertKind.Critical, Decide(15.0).Kind);
    }

    [Fact]
    public void Critical_OverridesPendingBolus()
    {
        // Сахар критический И есть плановый болюс — приоритет у Critical
        var result = Decide(16.0, events: new[] { PlannedInsulin(30) });
        Assert.Equal(GlucoseAlertKind.Critical, result.Kind);
    }

    [Fact]
    public void PendingBolusWithin120_GivesHintWithTime()
    {
        var ev = PlannedInsulin(60);
        var result = Decide(12.0, events: new[] { ev });
        Assert.Equal(GlucoseAlertKind.PendingBolus, result.Kind);
        Assert.Equal(ev.PlannedAtUtc, result.PendingBolusUtc);
    }

    [Fact]
    public void PendingBolusOutside120_FallsBackToCorrection()
    {
        Assert.Equal(GlucoseAlertKind.Correction, Decide(12.0, events: new[] { PlannedInsulin(130) }).Kind);
    }

    [Fact]
    public void DonePlannedBolus_Ignored()
    {
        Assert.Equal(GlucoseAlertKind.Correction, Decide(12.0, events: new[] { PlannedInsulin(30, done: true) }).Kind);
    }

    [Fact]
    public void PendingBolusBoundary120_StillHint()
    {
        Assert.Equal(GlucoseAlertKind.PendingBolus, Decide(12.0, events: new[] { PlannedInsulin(120) }).Kind);
    }

    [Fact]
    public void Priority_CriticalBeatsPendingBeatsCorrection()
    {
        // та же конфигурация планового болюса, но разный уровень сахара
        var events = new[] { PlannedInsulin(30) };
        Assert.Equal(GlucoseAlertKind.PendingBolus, Decide(12.0, events: events).Kind);
        Assert.Equal(GlucoseAlertKind.Critical, Decide(15.0, events: events).Kind);
    }

    // ── Smart IOB-vs-needed-correction path (therapy coefficients configured) ──────────────

    [Fact]
    public void IobCoversNeededCorrection_Suppresses()
    {
        // ISF=2 (1 ЕД снижает на 2 ммоль/л), цель 6.0, сахар 12.0 → нужно (12-6)/2 = 3 ЕД коррекции.
        // Болюс 4 ЕД 20 минут назад ещё не начал действовать (IOB=100% = 4 ЕД) — этого достаточно.
        var result = GlucoseAlertDecider.Decide(12.0, TargetHigh, new[] { Bolus(20.0 / 60) }, [], Now,
            correctionTarget: 6.0, insulinSensitivityFactor: 2.0);
        Assert.Equal(GlucoseAlertKind.None, result.Kind);
    }

    [Fact]
    public void IobDoesNotCoverNeededCorrection_StillAlerts()
    {
        // Тот же болюс 4 ЕД, но введён 3.5ч назад — почти весь IOB израсходован, а сахар всё
        // равно высокий: предыдущая доза явно не справилась, нужно предупреждение.
        var result = GlucoseAlertDecider.Decide(12.0, TargetHigh, new[] { Bolus(3.5) }, [], Now,
            correctionTarget: 6.0, insulinSensitivityFactor: 2.0);
        Assert.Equal(GlucoseAlertKind.Correction, result.Kind);
    }

    [Fact]
    public void NoTherapyCoefficients_FallsBackToTimeHeuristic()
    {
        // Без ISF/цели — старое поведение «болюс был недавно → подавляем».
        var result = GlucoseAlertDecider.Decide(12.0, TargetHigh, new[] { Bolus(1.5) }, [], Now);
        Assert.Equal(GlucoseAlertKind.None, result.Kind);
    }

    // ── Критический алерт учитывает уже введённую коррекцию (точный расчёт по IOB) ──────────

    [Fact]
    public void Critical_IobCoversNeededCorrection_Suppresses()
    {
        // Сахар критический 25.0, ISF=2, цель 6.0 → нужно (25-6)/2 = 9.5 ЕД коррекции.
        // Болюс 10 ЕД только что введён (IOB=100% = 10 ЕД) — этого достаточно, повторный
        // укол поверх привёл бы к стэкингу. Критический алерт гасится.
        var bigBolus = new InsulinInjectionDto(Guid.NewGuid(), Now.AddMinutes(-20), 10, (int)InsulinType.Bolus, null, null, Now, false);
        var result = GlucoseAlertDecider.Decide(25.0, TargetHigh, new[] { bigBolus }, [], Now,
            correctionTarget: 6.0, insulinSensitivityFactor: 2.0);
        Assert.Equal(GlucoseAlertKind.None, result.Kind);
    }

    [Fact]
    public void Critical_IobPartiallyCovers_StillCritical()
    {
        // Тот же критический сахар 25.0 (нужно 9.5 ЕД), но болюс всего 4 ЕД (IOB=4 < 9.5) —
        // введённой коррекции недостаточно, критический алерт обязан сработать.
        var result = GlucoseAlertDecider.Decide(25.0, TargetHigh, new[] { Bolus(20.0 / 60) }, [], Now,
            correctionTarget: 6.0, insulinSensitivityFactor: 2.0);
        Assert.Equal(GlucoseAlertKind.Critical, result.Kind);
    }

    [Fact]
    public void Critical_NoCoefficients_RecentBolusStillCritical()
    {
        // Без коэффициентов терапии при критическом сахаре эвристике «болюс был недавно»
        // не доверяем — критический алерт пробивает её (как и раньше).
        var result = GlucoseAlertDecider.Decide(25.0, TargetHigh, new[] { Bolus(0.5) }, [], Now);
        Assert.Equal(GlucoseAlertKind.Critical, result.Kind);
    }
}
