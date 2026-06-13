namespace GlucoTrack.Shared.Calculations;

public static class XeCalculator
{
    public const double DefaultGramsPerXe = 12.0;

    /// <summary>Переводит граммы углеводов в хлебные единицы.</summary>
    public static double CarbsToXe(double carbsGrams, double gramsPerXe = DefaultGramsPerXe)
    {
        if (gramsPerXe <= 0) throw new ArgumentOutOfRangeException(nameof(gramsPerXe), "Граммов на ХЕ должно быть > 0");
        if (carbsGrams < 0) throw new ArgumentOutOfRangeException(nameof(carbsGrams), "Углеводы не могут быть отрицательными");
        return Math.Round(carbsGrams / gramsPerXe, 2);
    }

    /// <summary>Переводит хлебные единицы в граммы углеводов.</summary>
    public static double XeToCarbs(double xe, double gramsPerXe = DefaultGramsPerXe)
    {
        if (gramsPerXe <= 0) throw new ArgumentOutOfRangeException(nameof(gramsPerXe), "Граммов на ХЕ должно быть > 0");
        if (xe < 0) throw new ArgumentOutOfRangeException(nameof(xe), "ХЕ не могут быть отрицательными");
        return Math.Round(xe * gramsPerXe, 2);
    }
}
