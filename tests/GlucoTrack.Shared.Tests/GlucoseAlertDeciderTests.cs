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
    public void RecentBolusWithin2h_SuppressesEverything()
    {
        Assert.Equal(GlucoseAlertKind.None, Decide(18.0, inj: new[] { Bolus(1.5) }).Kind);
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
}
