namespace GlucoTrack.Shared.Calculations;

public static class TirCalculator
{
    /// <summary>
    /// Рассчитывает Time In Range, Time Above Range, Time Below Range.
    /// </summary>
    /// <param name="readings">Замеры глюкозы в ммоль/л</param>
    /// <param name="low">Нижняя граница нормы (по умолчанию 3.9)</param>
    /// <param name="high">Верхняя граница нормы (по умолчанию 10.0)</param>
    public static TirResult Calculate(
        IReadOnlyList<double> readings,
        double low = 3.9,
        double high = 10.0)
    {
        if (low <= 0) throw new ArgumentOutOfRangeException(nameof(low), "Нижняя граница должна быть > 0");
        if (high <= low) throw new ArgumentOutOfRangeException(nameof(high), "Верхняя граница должна быть > нижней");
        if (readings.Count == 0)
            return new TirResult(0, 0, 0, 0);

        int inRange = 0, above = 0, below = 0;
        foreach (var r in readings)
        {
            if (r < low) below++;
            else if (r > high) above++;
            else inRange++;
        }

        int total = readings.Count;
        return new TirResult(
            TirPercent: Math.Round(inRange * 100.0 / total, 1),
            TarPercent: Math.Round(above * 100.0 / total, 1),
            TbrPercent: Math.Round(below * 100.0 / total, 1),
            TotalReadings: total);
    }
}

public record TirResult(double TirPercent, double TarPercent, double TbrPercent, int TotalReadings);
