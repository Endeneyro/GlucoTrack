using GlucoTrack.Shared.Calculations;

namespace GlucoTrack.Shared.Tests;

public class InsulinOnBoardTests
{
    private static readonly DateTime Now = new(2026, 6, 13, 15, 0, 0, DateTimeKind.Utc);

    // ── Fraction curve (экспоненциальная модель oref0) ────────────────────────

    [Fact]
    public void Fraction_AtZero_IsOne()
        => Assert.Equal(1.0, InsulinOnBoard.Fraction(0, 4.0), precision: 6);

    [Fact]
    public void Fraction_AtDia_Zero()
        => Assert.Equal(0.0, InsulinOnBoard.Fraction(240, 4.0));

    [Fact]
    public void Fraction_PastDia_Zero()
        => Assert.Equal(0.0, InsulinOnBoard.Fraction(300, 4.0));

    [Fact]
    public void Fraction_Future_Zero()
        => Assert.Equal(0.0, InsulinOnBoard.Fraction(-1, 4.0));

    [Fact]
    public void Fraction_StartsActingImmediately()
    {
        // В отличие от старой трапеции, экспонента действует уже с первых минут:
        // через 15 мин остаток < 100%, но ещё очень близко к нему.
        var f = InsulinOnBoard.Fraction(15, 4.0);
        Assert.True(f is < 1.0 and > 0.95, $"ожидали 0.95..1.0, получили {f}");
    }

    [Theory]
    // Опорные точки для DIA=4ч, peak=75 (канонический oref0)
    [InlineData(60, 0.737)]
    [InlineData(75, 0.634)]
    [InlineData(120, 0.342)]
    public void Fraction_ReferenceValues(double minutes, double expected)
        => Assert.Equal(expected, InsulinOnBoard.Fraction(minutes, 4.0), precision: 2);

    [Fact]
    public void Fraction_MonotonicallyDecreasing()
    {
        double prev = InsulinOnBoard.Fraction(0, 4.0);
        for (int t = 1; t <= 240; t++)
        {
            double cur = InsulinOnBoard.Fraction(t, 4.0);
            Assert.True(cur <= prev + 1e-9, $"немонотонно на {t} мин: {cur} > {prev}");
            prev = cur;
        }
    }

    [Theory]
    [InlineData(55)]
    [InlineData(75)]
    public void Activity_PeaksNearConfiguredPeak(double peak)
    {
        // Максимум кривой активности должен приходиться примерно на заданный пик (±5 мин)
        double bestT = 0, bestA = -1;
        for (int t = 1; t < 240; t++)
        {
            double a = InsulinOnBoard.Activity(t, 4.0, peak);
            if (a > bestA) { bestA = a; bestT = t; }
        }
        Assert.InRange(bestT, peak - 5, peak + 5);
    }

    [Fact]
    public void Activity_IntegratesToOne()
    {
        // Интеграл активности по [0, DIA] ≈ 1 (вся доза отрабатывает)
        double sum = 0;
        for (int t = 0; t < 240; t++)
            sum += InsulinOnBoard.Activity(t + 0.5, 4.0, 75);
        Assert.Equal(1.0, sum, precision: 2);
    }

    [Fact]
    public void Fraction_FasterPeak_LessIobAtPeakTime()
    {
        // Более быстрый инсулин (Fiasp peak 55) к 75-й минуте оставляет меньше IOB,
        // чем более медленный (Humalog peak 75).
        var fiasp   = InsulinOnBoard.Fraction(75, 4.0, 55);
        var humalog = InsulinOnBoard.Fraction(75, 4.0, 75);
        Assert.True(fiasp < humalog, $"Fiasp {fiasp} ожидался < Humalog {humalog}");
    }

    [Fact]
    public void Fraction_PeakClampedWhenTooLargeForDia()
    {
        // peak >= DIA/2 недопустим математически — клампится, исключения нет
        var f = InsulinOnBoard.Fraction(60, 2.0, peakMinutes: 90); // DIA=120 мин, peak/2=60
        Assert.InRange(f, 0.0, 1.0);
    }

    // ── Calculate ─────────────────────────────────────────────────────────────

    [Fact]
    public void FreshInjection_FullDose()
    {
        var iob = InsulinOnBoard.Calculate(new[] { (Now, 6.0) }, Now, diaHours: 4.0);
        Assert.Equal(6.0, iob);
    }

    [Fact]
    public void OneHourIn_ReferenceValue()
    {
        // t=60 мин → fraction≈0.737 → 6×0.737≈4.42
        var iob = InsulinOnBoard.Calculate(new[] { (Now.AddHours(-1), 6.0) }, Now, diaHours: 4.0);
        Assert.Equal(4.42, iob, precision: 2);
    }

    [Fact]
    public void TwoHoursIn_ReferenceValue()
    {
        // t=120 мин → fraction≈0.342 → 6×0.342≈2.05
        var iob = InsulinOnBoard.Calculate(new[] { (Now.AddHours(-2), 6.0) }, Now, diaHours: 4.0);
        Assert.Equal(2.05, iob, precision: 2);
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
        // 1ч назад: 3×0.737≈2.21; 2ч назад: 4×0.342≈1.37 → ≈3.58
        var iob = InsulinOnBoard.Calculate(new[]
        {
            (Now.AddHours(-1), 3.0),
            (Now.AddHours(-2), 4.0),
        }, Now, 4.0);
        Assert.Equal(3.58, iob, precision: 2);
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
    public void DefaultPeak_Is75Minutes()
        => Assert.Equal(75.0, InsulinOnBoard.DefaultPeakMinutes);

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
