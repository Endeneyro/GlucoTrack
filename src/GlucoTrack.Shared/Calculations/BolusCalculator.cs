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
    /// <param name="insulinOnBoard">Активный инсулин (IOB), ЕД — вычитается только из коррекции, доза на еду остаётся полной</param>
    public static BolusResult Calculate(
        double carbsGrams,
        double currentGlucose,
        double targetGlucose,
        double insulinToCarbRatio,
        double insulinSensitivityFactor,
        double insulinOnBoard = 0)
    {
        if (insulinToCarbRatio <= 0) throw new ArgumentOutOfRangeException(nameof(insulinToCarbRatio), "IC должен быть > 0");
        if (insulinSensitivityFactor <= 0) throw new ArgumentOutOfRangeException(nameof(insulinSensitivityFactor), "ISF должен быть > 0");
        if (carbsGrams < 0) throw new ArgumentOutOfRangeException(nameof(carbsGrams), "Углеводы не могут быть отрицательными");
        if (currentGlucose < 0) throw new ArgumentOutOfRangeException(nameof(currentGlucose), "Глюкоза не может быть отрицательной");
        if (targetGlucose <= 0) throw new ArgumentOutOfRangeException(nameof(targetGlucose), "Целевая глюкоза должна быть > 0");
        if (insulinOnBoard < 0) throw new ArgumentOutOfRangeException(nameof(insulinOnBoard), "Активный инсулин не может быть отрицательным");

        double mealDose = carbsGrams / insulinToCarbRatio;
        // CorrectionDose — «сырая» коррекция глюкозы (до вычета IOB); IOB уменьшает только её.
        // Доза на углеводы (mealDose) всегда полная — её активный инсулин не урезает.
        double correctionDose = (currentGlucose - targetGlucose) / insulinSensitivityFactor;
        double iob = Math.Round(insulinOnBoard, 2);

        var roundedMeal       = Math.Round(mealDose, 2);
        var roundedCorrection = Math.Round(correctionDose, 2);
        // Суммарная доза не может быть отрицательной — инсулин не забирают
        var totalDose = Math.Round(Math.Max(0, roundedMeal + roundedCorrection - iob), 2);
        return new BolusResult(
            MealDose: roundedMeal,
            CorrectionDose: roundedCorrection,
            TotalDose: totalDose,
            InsulinOnBoard: iob);
    }
}

public record BolusResult(double MealDose, double CorrectionDose, double TotalDose, double InsulinOnBoard = 0);

/// <summary>
/// Активный инсулин (Insulin on Board) — сколько ранее введённого болюса ещё работает.
///
/// Кусочно-линейная модель (3 сегмента):
///   0–30 мин   : IOB = 100% — инсулин ещё не всосался, действие не началось.
///   30–90 мин  : быстрое падение 100% → 40% — пик активности ультракороткого инсулина.
///   90–DIA мин : медленный хвост 40% → 0% — остаточная активность.
///
/// Это даёт более безопасный расчёт в первые 30–60 минут после укола,
/// исключая занижение коррекционной дозы на ещё не начавший работать инсулин.
/// </summary>
public static class InsulinOnBoard
{
    /// <summary>Длительность действия болюсного (ультракороткого) инсулина по умолчанию, ч.</summary>
    public const double DefaultDiaHours = 4.0;

    // Границы кривой (в минутах)
    private const double OnsetMinutes    = 30.0;  // начало всасывания
    private const double PeakEndMinutes  = 90.0;  // конец пиковой активности
    private const double PeakRemaining   = 0.40;  // доля IOB оставшаяся в конце пика

    /// <summary>
    /// Доля активного инсулина (0..1) через <paramref name="elapsedMinutes"/> минут
    /// после введения при заданном DIA.
    /// </summary>
    public static double Fraction(double elapsedMinutes, double diaHours)
    {
        if (diaHours <= 0) throw new ArgumentOutOfRangeException(nameof(diaHours), "DIA должен быть > 0");
        if (elapsedMinutes < 0 || elapsedMinutes >= diaHours * 60) return 0.0;

        if (elapsedMinutes < OnsetMinutes)
            return 1.0; // инсулин ещё не всосался — весь IOB на борту

        double diaMinutes = diaHours * 60.0;

        if (elapsedMinutes < PeakEndMinutes)
        {
            // Быстрое падение: 1.0 → PeakRemaining
            double t = (elapsedMinutes - OnsetMinutes) / (PeakEndMinutes - OnsetMinutes);
            return 1.0 - (1.0 - PeakRemaining) * t;
        }

        // Медленный хвост: PeakRemaining → 0
        double t2 = (elapsedMinutes - PeakEndMinutes) / (diaMinutes - PeakEndMinutes);
        return PeakRemaining * (1.0 - t2);
    }

    /// <summary>
    /// Суммарный активный инсулин на момент <paramref name="nowUtc"/>.
    /// Передавать следует только болюсные инъекции (без базала).
    /// Для расширенного болюса передайте <paramref name="extendedDurationHours"/> > 0.
    /// </summary>
    public static double Calculate(
        IEnumerable<(DateTime InjectedAtUtc, double Units, double? ExtendedDurationHours)> bolusInjections,
        DateTime nowUtc,
        double diaHours = DefaultDiaHours)
    {
        if (diaHours <= 0) throw new ArgumentOutOfRangeException(nameof(diaHours), "DIA должен быть > 0");

        double iob = 0;
        foreach (var (injectedAtUtc, units, extDurHours) in bolusInjections)
        {
            if (units <= 0) continue;
            if (extDurHours is > 0)
                iob += ExtendedFraction(units, extDurHours.Value, injectedAtUtc, nowUtc, diaHours);
            else
            {
                var elapsedMinutes = (nowUtc - injectedAtUtc).TotalMinutes;
                iob += units * Fraction(elapsedMinutes, diaHours);
            }
        }
        return Math.Round(iob, 2);
    }

    // Overload для обратной совместимости (стандартные болюсы без поля extended)
    public static double Calculate(
        IEnumerable<(DateTime InjectedAtUtc, double Units)> bolusInjections,
        DateTime nowUtc,
        double diaHours = DefaultDiaHours)
        => Calculate(bolusInjections.Select(x => (x.InjectedAtUtc, x.Units, (double?)null)), nowUtc, diaHours);

    // Расширенный болюс: инсулин подаётся равномерно от startUtc до startUtc+extDurHours.
    // IOB вычисляется численным интегрированием (60 слайсов).
    private static double ExtendedFraction(double units, double extDurHours, DateTime startUtc, DateTime nowUtc, double diaHours)
    {
        const int slices = 60;
        double deliveryMinutes = extDurHours * 60.0;
        double sliceMinutes = deliveryMinutes / slices;
        double unitsPerSlice = units / slices;
        double iob = 0;
        for (int i = 0; i < slices; i++)
        {
            var sliceDeliveredAt = startUtc.AddMinutes(sliceMinutes * (i + 0.5));
            var elapsed = (nowUtc - sliceDeliveredAt).TotalMinutes;
            iob += unitsPerSlice * Fraction(elapsed, diaHours);
        }
        return iob;
    }
}
