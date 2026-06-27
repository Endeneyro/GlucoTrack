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
/// Экспоненциальная модель oref0/Loop (OpenAPS), параметризованная двумя числами:
///   • DIA  — длительность действия инсулина (ч);
///   • peak — время пика активности (мин), зависит от препарата (Fiasp ≈ 55, Humalog ≈ 75 …).
///
/// Кривая <see cref="Fraction"/> — остаток инсулина (1→0), её вычитают из коррекционной дозы.
/// Кривая <see cref="Activity"/> — скорость действия (−dIOB/dt), для визуализации профиля.
/// В отличие от прежней кусочно-линейной трапеции, экспонента начинает действовать
/// сразу (медленно), даёт гладкий пик ровно на <c>peak</c> и плавный хвост к DIA.
/// </summary>
public static class InsulinOnBoard
{
    /// <summary>Длительность действия болюсного (ультракороткого) инсулина по умолчанию, ч.</summary>
    public const double DefaultDiaHours = 4.0;

    /// <summary>Время пика по умолчанию (ультракороткий инсулин), мин.</summary>
    public const double DefaultPeakMinutes = 75.0;

    /// <summary>
    /// Параметры экспоненциальной модели (tau, a, S) для заданных DIA и пика.
    /// peak клампится в (0, DIA/2) — модель определена только при peak &lt; DIA/2.
    /// </summary>
    private static (double Tau, double A, double S, double End) CurveParams(double diaHours, double peakMinutes)
    {
        double end = diaHours * 60.0;
        // peak должен быть строго меньше DIA/2, иначе tau вырождается (деление на ~0)
        double peak = Math.Clamp(peakMinutes, 10.0, Math.Max(10.0, end * 0.5 - 1.0));

        double tau = peak * (1.0 - peak / end) / (1.0 - 2.0 * peak / end);
        double a   = 2.0 * tau / end;
        double s   = 1.0 / (1.0 - a + (1.0 + a) * Math.Exp(-end / tau));
        return (tau, a, s, end);
    }

    /// <summary>
    /// Доля активного инсулина (0..1) через <paramref name="elapsedMinutes"/> минут
    /// после введения при заданных DIA и времени пика.
    /// </summary>
    public static double Fraction(double elapsedMinutes, double diaHours, double peakMinutes)
    {
        if (diaHours <= 0) throw new ArgumentOutOfRangeException(nameof(diaHours), "DIA должен быть > 0");

        var (tau, a, s, end) = CurveParams(diaHours, peakMinutes);
        if (elapsedMinutes < 0 || elapsedMinutes >= end) return 0.0;

        double t = elapsedMinutes;
        double iob = 1.0 - s * (1.0 - a)
            * ((t * t / (tau * end * (1.0 - a)) - t / tau - 1.0) * Math.Exp(-t / tau) + 1.0);
        return Math.Clamp(iob, 0.0, 1.0);
    }

    /// <summary>Перегрузка с пиком по умолчанию (обратная совместимость).</summary>
    public static double Fraction(double elapsedMinutes, double diaHours)
        => Fraction(elapsedMinutes, diaHours, DefaultPeakMinutes);

    /// <summary>
    /// Активность инсулина — доля дозы, отрабатывающая за минуту, через
    /// <paramref name="elapsedMinutes"/> мин. Интеграл по [0, DIA] = 1, максимум ≈ на пике.
    /// Используется для отрисовки профиля действия (не для расчёта дозы).
    /// </summary>
    public static double Activity(double elapsedMinutes, double diaHours, double peakMinutes)
    {
        if (diaHours <= 0) throw new ArgumentOutOfRangeException(nameof(diaHours), "DIA должен быть > 0");

        var (tau, _, s, end) = CurveParams(diaHours, peakMinutes);
        if (elapsedMinutes <= 0 || elapsedMinutes >= end) return 0.0;

        double t = elapsedMinutes;
        return s / (tau * tau) * t * (1.0 - t / end) * Math.Exp(-t / tau);
    }

    /// <summary>
    /// Суммарный активный инсулин на момент <paramref name="nowUtc"/>.
    /// Передавать следует только болюсные инъекции (без базала).
    /// Для расширенного болюса передайте <paramref name="extendedDurationHours"/> > 0.
    /// </summary>
    public static double Calculate(
        IEnumerable<(DateTime InjectedAtUtc, double Units, double? ExtendedDurationHours)> bolusInjections,
        DateTime nowUtc,
        double diaHours = DefaultDiaHours,
        double peakMinutes = DefaultPeakMinutes)
    {
        if (diaHours <= 0) throw new ArgumentOutOfRangeException(nameof(diaHours), "DIA должен быть > 0");

        double iob = 0;
        foreach (var (injectedAtUtc, units, extDurHours) in bolusInjections)
        {
            if (units <= 0) continue;
            if (extDurHours is > 0)
                iob += ExtendedFraction(units, extDurHours.Value, injectedAtUtc, nowUtc, diaHours, peakMinutes);
            else
            {
                var elapsedMinutes = (nowUtc - injectedAtUtc).TotalMinutes;
                iob += units * Fraction(elapsedMinutes, diaHours, peakMinutes);
            }
        }
        return Math.Round(iob, 2);
    }

    // Overload для обратной совместимости (стандартные болюсы без поля extended)
    public static double Calculate(
        IEnumerable<(DateTime InjectedAtUtc, double Units)> bolusInjections,
        DateTime nowUtc,
        double diaHours = DefaultDiaHours,
        double peakMinutes = DefaultPeakMinutes)
        => Calculate(bolusInjections.Select(x => (x.InjectedAtUtc, x.Units, (double?)null)), nowUtc, diaHours, peakMinutes);

    // Расширенный болюс: инсулин подаётся равномерно от startUtc до startUtc+extDurHours.
    // IOB вычисляется численным интегрированием (60 слайсов).
    private static double ExtendedFraction(double units, double extDurHours, DateTime startUtc, DateTime nowUtc, double diaHours, double peakMinutes)
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
            iob += unitsPerSlice * Fraction(elapsed, diaHours, peakMinutes);
        }
        return iob;
    }
}
