using GlucoTrack.Shared.Calculations;

namespace GlucoTrack.Shared.Tests;

public class NutritionGoalCalculatorTests
{
    // Эталон: муж, 80 кг, 180 см, 30 лет → BMR = 800+1125-150+5 = 1780
    [Fact]
    public void Bmr_Male_MifflinReference()
        => Assert.Equal(1780, NutritionGoalCalculator.BasalMetabolicRate(true, 80, 180, 30), precision: 4);

    [Fact]
    public void Bmr_Female_Minus161()
        // 600 + 1031.25 - 125 - 161 = 1345.25
        => Assert.Equal(1345.25, NutritionGoalCalculator.BasalMetabolicRate(false, 60, 165, 25), precision: 4);

    [Fact]
    public void Maintain_Moderate_ReferenceTargets()
    {
        // BaseTDEE = 1780 × 1.55 = 2759; maintain → 2759
        var t = NutritionGoalCalculator.Calculate(true, 80, 180, 30,
            ActivityLevel.Moderate, NutritionGoal.Maintain);

        Assert.Equal(2759, t.Calories);
        Assert.Equal(2759, t.BaseTdee);
        Assert.Equal(144, t.Protein);   // 1.8 × 80
        Assert.Equal(92, t.Fat);        // 0.30 × 2759 / 9 ≈ 91.97
        Assert.Equal(339, t.Carbs);     // (2759 − 576 − 827.7) / 4 ≈ 338.8
        Assert.Equal(0, t.ActiveKcal);
    }

    [Fact]
    public void Loss_AppliesDelta()
    {
        var maintain = NutritionGoalCalculator.Calculate(true, 80, 180, 30, ActivityLevel.Moderate, NutritionGoal.Maintain);
        var loss     = NutritionGoalCalculator.Calculate(true, 80, 180, 30, ActivityLevel.Moderate, NutritionGoal.Loss);
        // Дельта похудения = 0.80 от базовых калорий
        Assert.Equal(Math.Round(maintain.BaseTdee * NutritionGoalCalculator.LossDelta), loss.Calories);
        Assert.True(loss.Calories < maintain.Calories);
    }

    [Fact]
    public void Gain_AppliesDelta()
    {
        var maintain = NutritionGoalCalculator.Calculate(true, 80, 180, 30, ActivityLevel.Moderate, NutritionGoal.Maintain);
        var gain     = NutritionGoalCalculator.Calculate(true, 80, 180, 30, ActivityLevel.Moderate, NutritionGoal.Gain);
        Assert.Equal(Math.Round(maintain.BaseTdee * NutritionGoalCalculator.GainDelta), gain.Calories);
        Assert.True(gain.Calories > maintain.Calories);
    }

    [Fact]
    public void ActiveKcal_GoesToCarbsOnly()
    {
        var baseT  = NutritionGoalCalculator.Calculate(true, 80, 180, 30, ActivityLevel.Moderate, NutritionGoal.Maintain);
        var withAct = NutritionGoalCalculator.Calculate(true, 80, 180, 30, ActivityLevel.Moderate, NutritionGoal.Maintain, activeKcal: 400);

        // Белок и жир НЕ меняются (привязаны к телу/базовым калориям)
        Assert.Equal(baseT.Protein, withAct.Protein);
        Assert.Equal(baseT.Fat, withAct.Fat);
        // Калории +400, и весь прирост ушёл в углеводы: 400 / 4 = 100 г
        Assert.Equal(baseT.Calories + 400, withAct.Calories);
        Assert.Equal(baseT.Carbs + 100, withAct.Carbs);
        Assert.Equal(400, withAct.ActiveKcal);
    }

    [Fact]
    public void NegativeActiveKcal_TreatedAsZero()
    {
        var a = NutritionGoalCalculator.Calculate(true, 80, 180, 30, ActivityLevel.Moderate, NutritionGoal.Maintain, activeKcal: -50);
        var b = NutritionGoalCalculator.Calculate(true, 80, 180, 30, ActivityLevel.Moderate, NutritionGoal.Maintain);
        Assert.Equal(b.Calories, a.Calories);
        Assert.Equal(0, a.ActiveKcal);
    }

    [Theory]
    [InlineData(ActivityLevel.Sedentary, 1.2)]
    [InlineData(ActivityLevel.Light, 1.375)]
    [InlineData(ActivityLevel.Moderate, 1.55)]
    [InlineData(ActivityLevel.High, 1.725)]
    [InlineData(ActivityLevel.VeryHigh, 1.9)]
    public void ActivityFactors(ActivityLevel lvl, double expected)
        => Assert.Equal(expected, NutritionGoalCalculator.ActivityFactor(lvl));

    [Theory]
    [InlineData(0)]    // weight
    public void InvalidWeight_Throws(double w)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            NutritionGoalCalculator.Calculate(true, w, 180, 30, ActivityLevel.Moderate, NutritionGoal.Maintain));

    [Fact]
    public void InvalidHeightOrAge_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NutritionGoalCalculator.Calculate(true, 80, 0, 30, ActivityLevel.Moderate, NutritionGoal.Maintain));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NutritionGoalCalculator.Calculate(true, 80, 180, 0, ActivityLevel.Moderate, NutritionGoal.Maintain));
    }

    [Fact]
    public void AgeYears_ExactAndPreBirthday()
    {
        var today = new DateOnly(2026, 6, 28);
        Assert.Equal(30, NutritionGoalCalculator.AgeYears(new DateOnly(1996, 6, 28), today)); // ДР сегодня
        Assert.Equal(29, NutritionGoalCalculator.AgeYears(new DateOnly(1996, 6, 29), today)); // ДР завтра
    }
}
