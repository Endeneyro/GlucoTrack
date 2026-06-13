using GlucoTrack.Shared.Calculations;

namespace GlucoTrack.Shared.Tests;

public class TirCalculatorTests
{
    [Fact]
    public void AllInRange_100Percent()
    {
        var readings = new[] { 5.0, 6.0, 7.0, 8.0, 9.0 };
        var result = TirCalculator.Calculate(readings);

        Assert.Equal(100.0, result.TirPercent);
        Assert.Equal(0.0, result.TarPercent);
        Assert.Equal(0.0, result.TbrPercent);
    }

    [Fact]
    public void AllAboveRange_100TarPercent()
    {
        var readings = new[] { 11.0, 13.0, 15.0 };
        var result = TirCalculator.Calculate(readings);

        Assert.Equal(0.0, result.TirPercent);
        Assert.Equal(100.0, result.TarPercent);
        Assert.Equal(0.0, result.TbrPercent);
    }

    [Fact]
    public void AllBelowRange_100TbrPercent()
    {
        var readings = new[] { 2.0, 3.0, 3.5 };
        var result = TirCalculator.Calculate(readings);

        Assert.Equal(0.0, result.TirPercent);
        Assert.Equal(0.0, result.TarPercent);
        Assert.Equal(100.0, result.TbrPercent);
    }

    [Fact]
    public void MixedReadings_CorrectPercentages()
    {
        // 2 в диапазоне, 1 выше, 1 ниже → TIR=50%, TAR=25%, TBR=25%
        var readings = new[] { 3.0, 6.0, 8.0, 12.0 };
        var result = TirCalculator.Calculate(readings);

        Assert.Equal(50.0, result.TirPercent);
        Assert.Equal(25.0, result.TarPercent);
        Assert.Equal(25.0, result.TbrPercent);
        Assert.Equal(4, result.TotalReadings);
    }

    [Fact]
    public void BoundaryValues_IncludedInRange()
    {
        // Границы 3.9 и 10.0 — должны попасть в диапазон (включительно)
        var readings = new[] { 3.9, 10.0 };
        var result = TirCalculator.Calculate(readings);

        Assert.Equal(100.0, result.TirPercent);
    }

    [Fact]
    public void JustOutsideBoundaries_NotInRange()
    {
        var readings = new[] { 3.8, 10.1 };
        var result = TirCalculator.Calculate(readings);

        Assert.Equal(0.0, result.TirPercent);
        Assert.Equal(50.0, result.TarPercent);
        Assert.Equal(50.0, result.TbrPercent);
    }

    [Fact]
    public void EmptyReadings_ReturnsAllZeros()
    {
        var result = TirCalculator.Calculate(Array.Empty<double>());

        Assert.Equal(0.0, result.TirPercent);
        Assert.Equal(0.0, result.TarPercent);
        Assert.Equal(0.0, result.TbrPercent);
        Assert.Equal(0, result.TotalReadings);
    }

    [Fact]
    public void SingleReading_InRange()
    {
        var result = TirCalculator.Calculate(new[] { 7.0 });

        Assert.Equal(100.0, result.TirPercent);
        Assert.Equal(1, result.TotalReadings);
    }

    [Fact]
    public void CustomBoundaries_Respected()
    {
        // Ночной диапазон 4.0–8.0
        var readings = new[] { 5.0, 9.0, 3.5 };
        var result = TirCalculator.Calculate(readings, low: 4.0, high: 8.0);

        Assert.Equal(33.3, result.TirPercent);
        Assert.Equal(33.3, result.TarPercent);
        Assert.Equal(33.3, result.TbrPercent);
    }

    [Fact]
    public void HighLowerThanLow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TirCalculator.Calculate(new[] { 6.0 }, low: 10.0, high: 4.0));
    }

    [Fact]
    public void SumOfPercentsEquals100()
    {
        var readings = new[] { 2.0, 5.0, 6.0, 11.0, 7.0 };
        var result = TirCalculator.Calculate(readings);
        Assert.Equal(100.0, result.TirPercent + result.TarPercent + result.TbrPercent);
    }
}
