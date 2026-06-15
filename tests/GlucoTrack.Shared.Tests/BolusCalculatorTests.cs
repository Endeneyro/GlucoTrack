using GlucoTrack.Shared.Calculations;

namespace GlucoTrack.Shared.Tests;

public class BolusCalculatorTests
{
    // IC=10 (10г углеводов на 1 ЕД), ISF=2.0 (2 ммоль/л на 1 ЕД), цель=6.0

    [Fact]
    public void MealOnly_NoCorrection_ReturnsCorrectMealDose()
    {
        var result = BolusCalculator.Calculate(
            carbsGrams: 60,
            currentGlucose: 6.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0);

        Assert.Equal(6.0, result.MealDose);
        Assert.Equal(0.0, result.CorrectionDose);
        Assert.Equal(6.0, result.TotalDose);
    }

    [Fact]
    public void HighGlucose_AddsCorrectionToMealDose()
    {
        // Глюкоза 10.0 при цели 6.0 → коррекция (10-6)/2 = 2 ЕД
        // Еда 60г / IC10 = 6 ЕД → итого 8 ЕД
        var result = BolusCalculator.Calculate(
            carbsGrams: 60,
            currentGlucose: 10.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0);

        Assert.Equal(6.0, result.MealDose);
        Assert.Equal(2.0, result.CorrectionDose);
        Assert.Equal(8.0, result.TotalDose);
    }

    [Fact]
    public void LowGlucose_CorrectionReducesMealDose()
    {
        // Глюкоза 4.0 при цели 6.0 → коррекция (4-6)/2 = -1 ЕД
        // Еда 20г / IC10 = 2 ЕД → итого 1 ЕД
        var result = BolusCalculator.Calculate(
            carbsGrams: 20,
            currentGlucose: 4.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0);

        Assert.Equal(2.0, result.MealDose);
        Assert.Equal(-1.0, result.CorrectionDose);
        Assert.Equal(1.0, result.TotalDose);
    }

    [Fact]
    public void SafetyInvariant_TotalDoseNeverNegative()
    {
        // Гипогликемия + почти нет еды → коррекция перекрывает еду → должно быть 0, не отрицательное
        var result = BolusCalculator.Calculate(
            carbsGrams: 5,
            currentGlucose: 2.5,     // тяжёлая гипогликемия
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0);

        Assert.True(result.TotalDose >= 0, "Доза инсулина не может быть отрицательной");
        Assert.Equal(0.0, result.TotalDose);
    }

    [Fact]
    public void ZeroCarbs_CorrectionOnly()
    {
        // Нет еды, только коррекция высокой глюкозы
        var result = BolusCalculator.Calculate(
            carbsGrams: 0,
            currentGlucose: 14.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0);

        Assert.Equal(0.0, result.MealDose);
        Assert.Equal(4.0, result.CorrectionDose);
        Assert.Equal(4.0, result.TotalDose);
    }

    [Fact]
    public void ZeroCarbs_HypoglycemiaCorrection_ReturnsZero()
    {
        // Нет еды + гипогликемия → никакого инсулина
        var result = BolusCalculator.Calculate(
            carbsGrams: 0,
            currentGlucose: 3.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0);

        Assert.Equal(0.0, result.TotalDose);
    }

    [Fact]
    public void GlucoseAtTarget_ZeroCarbs_ReturnsAllZeros()
    {
        var result = BolusCalculator.Calculate(
            carbsGrams: 0,
            currentGlucose: 6.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0);

        Assert.Equal(0.0, result.MealDose);
        Assert.Equal(0.0, result.CorrectionDose);
        Assert.Equal(0.0, result.TotalDose);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidIC_Throws(double ic)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BolusCalculator.Calculate(60, 6.0, 6.0, ic, 2.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void InvalidISF_Throws(double isf)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BolusCalculator.Calculate(60, 6.0, 6.0, 10, isf));
    }

    [Fact]
    public void NegativeCarbs_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BolusCalculator.Calculate(-1, 6.0, 6.0, 10, 2.0));
    }

    [Fact]
    public void InsulinOnBoard_ReducesCorrectionOnly()
    {
        // Глюкоза 14 при цели 6 → сырая коррекция (14-6)/2 = 4 ЕД.
        // IOB 2.5 ЕД уже на борту → итог 4 − 2.5 = 1.5. Доза на еду = 0 (углеводов нет).
        var result = BolusCalculator.Calculate(
            carbsGrams: 0,
            currentGlucose: 14.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0,
            insulinOnBoard: 2.5);

        Assert.Equal(0.0, result.MealDose);
        Assert.Equal(4.0, result.CorrectionDose); // сырая коррекция до IOB
        Assert.Equal(2.5, result.InsulinOnBoard);
        Assert.Equal(1.5, result.TotalDose);
    }

    [Fact]
    public void InsulinOnBoard_MealDoseStaysFull_OnlyTotalDrops()
    {
        // Углеводы 60/IC10 = 6 ЕД на еду; глюкоза в цели → коррекция 0.
        // IOB 2 ЕД → итог 6 + 0 − 2 = 4. MealDose остаётся 6 (не урезается «вручную»).
        var result = BolusCalculator.Calculate(
            carbsGrams: 60,
            currentGlucose: 6.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0,
            insulinOnBoard: 2.0);

        Assert.Equal(6.0, result.MealDose);
        Assert.Equal(0.0, result.CorrectionDose);
        Assert.Equal(4.0, result.TotalDose);
    }

    [Fact]
    public void InsulinOnBoard_ExceedsNeed_TotalNeverNegative()
    {
        // Небольшая потребность, но крупный IOB → доза не может быть отрицательной
        var result = BolusCalculator.Calculate(
            carbsGrams: 10,            // 1 ЕД на еду
            currentGlucose: 8.0,       // коррекция (8-6)/2 = 1 ЕД
            targetGlucose: 6.0,
            insulinToCarbRatio: 10,
            insulinSensitivityFactor: 2.0,
            insulinOnBoard: 5.0);      // активного инсулина больше, чем нужно

        Assert.Equal(0.0, result.TotalDose);
    }

    [Fact]
    public void NegativeInsulinOnBoard_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BolusCalculator.Calculate(60, 6.0, 6.0, 10, 2.0, insulinOnBoard: -1));
    }

    [Fact]
    public void FractionalCarbs_RoundsToTwoDecimals()
    {
        // 55г / IC9 = 6.111... → 6.11
        var result = BolusCalculator.Calculate(
            carbsGrams: 55,
            currentGlucose: 6.0,
            targetGlucose: 6.0,
            insulinToCarbRatio: 9,
            insulinSensitivityFactor: 2.0);

        Assert.Equal(6.11, result.MealDose);
        Assert.Equal(6.11, result.TotalDose);
    }
}
