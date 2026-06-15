using GlucoTrack.Shared.Calculations;

namespace GlucoTrack.Shared.Tests;

public class InsulinOnBoardTests
{
    private static readonly DateTime Now = new(2026, 6, 13, 15, 0, 0, DateTimeKind.Utc);

    // ── Fraction curve ────────────────────────────────────────────────────────

    [Fact]
    public void Fraction_AtZero_IsOne()
        => Assert.Equal(1.0, InsulinOnBoard.Fraction(0, 4.0));

    [Fact]
    public void Fraction_At15min_StillOne()
        // Onset = 30 min; до него инсулин не всосался → 100%
        => Assert.Equal(1.0, InsulinOnBoard.Fraction(15, 4.0));

    [Fact]
    public void Fraction_At30min_StillOne()
        => Assert.Equal(1.0, InsulinOnBoard.Fraction(30, 4.0));

    [Fact]
    public void Fraction_At60min_SeventyPercent()
    {
        // Середина пиковой фазы [30,90]: t=(60-30)/(90-30)=0.5 → 1-(1-0.4)*0.5=0.70
        var f = InsulinOnBoard.Fraction(60, 4.0);
        Assert.Equal(0.70, f, precision: 10);
    }

    [Fact]
    public void Fraction_At90min_FourtyPercent()
        // Конец пика → PeakRemaining = 0.40
        => Assert.Equal(0.40, InsulinOnBoard.Fraction(90, 4.0), precision: 10);

    [Fact]
    public void Fraction_AtDia_Zero()
        => Assert.Equal(0.0, InsulinOnBoard.Fraction(240, 4.0));

    [Fact]
    public void Fraction_PastDia_Zero()
        => Assert.Equal(0.0, InsulinOnBoard.Fraction(300, 4.0));

    [Fact]
    public void Fraction_Future_Zero()
        => Assert.Equal(0.0, InsulinOnBoard.Fraction(-1, 4.0));

    // ── Calculate ─────────────────────────────────────────────────────────────

    [Fact]
    public void FreshInjection_FullDose()
    {
        // t=0 → fraction=1.0
        var iob = InsulinOnBoard.Calculate(new[] { (Now, 6.0) }, Now, diaHours: 4.0);
        Assert.Equal(6.0, iob);
    }

    [Fact]
    public void At15min_StillFullDose()
    {
        // t=15 мин — инсулин ещё не всосался, IOB=100%
        var iob = InsulinOnBoard.Calculate(new[] { (Now.AddMinutes(-15), 6.0) }, Now, diaHours: 4.0);
        Assert.Equal(6.0, iob);
    }

    [Fact]
    public void OneHourIn_SeventyPercent()
    {
        // t=60 мин → fraction=0.70 → 6×0.70=4.2
        var iob = InsulinOnBoard.Calculate(new[] { (Now.AddHours(-1), 6.0) }, Now, diaHours: 4.0);
        Assert.Equal(4.2, iob);
    }

    [Fact]
    public void TwoHoursIn_TailPhase()
    {
        // t=120 мин → хвостовая фаза: 0.40*(1-(120-90)/(240-90))=0.40*0.8=0.32 → 6×0.32=1.92
        var iob = InsulinOnBoard.Calculate(new[] { (Now.AddHours(-2), 6.0) }, Now, diaHours: 4.0);
        Assert.Equal(1.92, iob);
    }

    [Fact]
    public void AfterDia_Zero()
    {
        Assert.Equal(0.0, InsulinOnBoard.Calculate(new[] { (Now.AddHours(-4), 6.0) }, Now, 4.0));
        Assert.Equal(0.0, InsulinOnBoard.Calculate(new[] { (Now.AddHours(-5), 6.0) }, Now, 4.0));
    }

    [Fact]
    public void MultipleInjections_Summed()
    {
        // 1ч назад: 3×0.70=2.10; 2ч назад: 4×0.32=1.28 → 3.38
        var iob = InsulinOnBoard.Calculate(new[]
        {
            (Now.AddHours(-1), 3.0),
            (Now.AddHours(-2), 4.0),
        }, Now, 4.0);
        Assert.Equal(3.38, iob);
    }

    [Fact]
    public void FutureInjection_Ignored()
    {
        var iob = InsulinOnBoard.Calculate(new[] { (Now.AddHours(1), 5.0) }, Now, 4.0);
        Assert.Equal(0.0, iob);
    }

    [Fact]
    public void Empty_Zero()
        => Assert.Equal(0.0, InsulinOnBoard.Calculate(Array.Empty<(DateTime, double)>(), Now, 4.0));

    [Fact]
    public void DefaultDia_IsFourHours()
        => Assert.Equal(4.0, InsulinOnBoard.DefaultDiaHours);

    [Fact]
    public void CustomDia_3h_EarlierExpiry()
    {
        // DIA=3ч → укол 3ч назад уже неактивен
        var iob = InsulinOnBoard.Calculate(new[] { (Now.AddHours(-3), 5.0) }, Now, diaHours: 3.0);
        Assert.Equal(0.0, iob);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidDia_Throws(double dia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InsulinOnBoard.Calculate(new[] { (Now, 5.0) }, Now, dia));
    }
}
