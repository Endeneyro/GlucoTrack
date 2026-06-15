namespace GlucoTrack.Shared.Events;

/// <summary>
/// Тайминг пред-болюса: за сколько минут до еды колоть инсулин (по гликемическому индексу),
/// и проверка/подбор времени приёма, чтобы укол не оказался в прошлом.
/// </summary>
public static class PreBolusPlanner
{
    /// <summary>Средневзвешенный по углеводам ГИ продуктов приёма; null, если ГИ нигде не задан.</summary>
    public static double? WeightedGi(IEnumerable<(double Gi, double Carbs)> items)
    {
        double num = 0, den = 0;
        foreach (var (gi, carbs) in items)
        {
            if (carbs <= 0) continue;
            num += gi * carbs;
            den += carbs;
        }
        return den > 0 ? num / den : null;
    }

    /// <summary>
    /// Минут до еды для пред-болюса с учётом текущего уровня глюкозы и ГИ.
    ///
    /// При низком сахаре (&lt;4.5) пауза всегда 0 — колоть заранее опасно (гипо до первого куска).
    /// При повышенном сахаре (&gt;7.0) пауза увеличивается даже при низком ГИ.
    /// currentGlucose = 0 означает «не указан» — используется только ГИ.
    /// </summary>
    public static int MinutesForGiAndGlucose(double? weightedGi, double currentGlucose = 0)
    {
        // Гипо или близко к ней — еда нужна немедленно, любая пауза опасна
        if (currentGlucose > 0 && currentGlucose < 4.5) return 0;

        // Повышенный сахар (>7.0) — пауза максимальная по ГИ
        if (currentGlucose > 7.0)
            return weightedGi switch
            {
                null  => 15,
                >= 70 => 25,
                >= 56 => 20,
                _     => 10
            };

        // Нормальный или чуть пониженный сахар (4.5–7.0) — стандартная таблица по ГИ
        return weightedGi switch
        {
            null  => 10,
            >= 70 => 20,
            >= 56 => 15,
            _     => 0
        };
    }

    /// <summary>Минут до еды для пред-болюса только по ГИ (без учёта сахара). Используется когда сахар не введён.</summary>
    public static int MinutesForGi(double? weightedGi) => MinutesForGiAndGlucose(weightedGi, 0);

    /// <summary>Подпись ГИ для интерфейса; пустая строка, если ГИ не задан.</summary>
    public static string GiLabel(double? weightedGi) => weightedGi switch
    {
        null      => "",
        >= 70     => $"ГИ {weightedGi:F0} — высокий",
        >= 56     => $"ГИ {weightedGi:F0} — средний",
        _         => $"ГИ {weightedGi:F0} — низкий"
    };

    /// <summary>Время инъекции = время еды − минуты пред-болюса.</summary>
    public static DateTime InsulinTime(DateTime mealTime, int minutes) => mealTime.AddMinutes(-minutes);

    /// <summary>Время пред-болюса уже прошло (не успеть сделать укол вовремя).</summary>
    public static bool InsulinTimeIsPast(DateTime mealTime, int minutes, DateTime now) =>
        InsulinTime(mealTime, minutes) < now;

    /// <summary>
    /// Ближайшее время приёма, при котором пред-болюс остаётся в будущем:
    /// сейчас + минуты пред-болюса, округлённое вверх до 5 минут (шаг колеса времени).
    /// </summary>
    public static DateTime SuggestedMealTime(DateTime now, int minutes)
    {
        var t = now.AddMinutes(minutes);
        var rem = t.Minute % 5;
        if (rem != 0 || t.Second > 0 || t.Millisecond > 0)
            t = t.AddMinutes(5 - rem);
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Kind);
    }
}
