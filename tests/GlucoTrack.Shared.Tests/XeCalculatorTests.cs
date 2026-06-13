using GlucoTrack.Shared.Calculations;

namespace GlucoTrack.Shared.Tests;

public class XeCalculatorTests
{
    [Theory]
    [InlineData(12, 1.0)]
    [InlineData(24, 2.0)]
    [InlineData(6, 0.5)]
    [InlineData(0, 0.0)]
    [InlineData(100, 8.33)]
    public void CarbsToXe_DefaultGramsPerXe_CorrectResult(double carbs, double expectedXe)
    {
        Assert.Equal(expectedXe, XeCalculator.CarbsToXe(carbs));
    }

    [Fact]
    public void CarbsToXe_CustomGramsPerXe_CorrectResult()
    {
        // В некоторых странах 1 ХЕ = 10г
        Assert.Equal(3.0, XeCalculator.CarbsToXe(30, gramsPerXe: 10));
    }

    [Theory]
    [InlineData(1.0, 12.0)]
    [InlineData(2.5, 30.0)]
    [InlineData(0, 0.0)]
    public void XeToCarbs_DefaultGramsPerXe_CorrectResult(double xe, double expectedCarbs)
    {
        Assert.Equal(expectedCarbs, XeCalculator.XeToCarbs(xe));
    }

    [Fact]
    public void CarbsToXe_ThenXeToCarbs_RoundTrip()
    {
        double original = 36;
        double xe = XeCalculator.CarbsToXe(original);
        double back = XeCalculator.XeToCarbs(xe);
        Assert.Equal(original, back);
    }

    [Fact]
    public void NegativeCarbs_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => XeCalculator.CarbsToXe(-1));
    }

    [Fact]
    public void ZeroGramsPerXe_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => XeCalculator.CarbsToXe(12, 0));
    }
}
