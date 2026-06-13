namespace GlucoTrack.Shared.Calculations;

public static class BolusCalculator
{
    /// <summary>
    /// Рассчитывает дозу болюса (ХЕ + коррекция).
    /// </summary>
    /// <param name="carbsGrams">Углеводы в граммах</param>
    /// <param name="currentGlucose">Текущий уровень глюкозы, ммоль/л</param>
    /// <param name="targetGlucose">Целевой уровень глюкозы, ммоль/л</param>
    /// <param name="insulinToCarbRatio">IC: граммов углеводов на 1 ЕД инсулина</param>
    /// <param name="insulinSensitivityFactor">ISF: снижение глюкозы на 1 ЕД, ммоль/л</param>
    /// <returns>Результат с разбивкой на еду и коррекцию</returns>
    public static BolusResult Calculate(
        double carbsGrams,
        double currentGlucose,
        double targetGlucose,
        double insulinToCarbRatio,
        double insulinSensitivityFactor)
    {
        if (insulinToCarbRatio <= 0) throw new ArgumentOutOfRangeException(nameof(insulinToCarbRatio), "IC должен быть > 0");
        if (insulinSensitivityFactor <= 0) throw new ArgumentOutOfRangeException(nameof(insulinSensitivityFactor), "ISF должен быть > 0");
        if (carbsGrams < 0) throw new ArgumentOutOfRangeException(nameof(carbsGrams), "Углеводы не могут быть отрицательными");
        if (currentGlucose < 0) throw new ArgumentOutOfRangeException(nameof(currentGlucose), "Глюкоза не может быть отрицательной");
        if (targetGlucose <= 0) throw new ArgumentOutOfRangeException(nameof(targetGlucose), "Целевая глюкоза должна быть > 0");

        double mealDose = carbsGrams / insulinToCarbRatio;
        double correctionDose = (currentGlucose - targetGlucose) / insulinSensitivityFactor;

        // Суммарная доза не может быть отрицательной — инсулин не забирают
        double totalDose = Math.Max(0, mealDose + correctionDose);

        return new BolusResult(
            MealDose: Math.Round(mealDose, 2),
            CorrectionDose: Math.Round(correctionDose, 2),
            TotalDose: Math.Round(totalDose, 2));
    }
}

public record BolusResult(double MealDose, double CorrectionDose, double TotalDose);
