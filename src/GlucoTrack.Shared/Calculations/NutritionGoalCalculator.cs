namespace GlucoTrack.Shared.Calculations;

/// <summary>Цель по питанию.</summary>
public enum NutritionGoal { Loss = 0, Maintain = 1, Gain = 2 }

/// <summary>Уровень базовой (нетренировочной) активности — NEAT, без учёта тренировок.</summary>
public enum ActivityLevel { Sedentary = 0, Light = 1, Moderate = 2, High = 3, VeryHigh = 4 }

/// <summary>Итоговые цели по калориям и макронутриентам (граммы), плюс разбивка TDEE.</summary>
public record NutritionTargets(
    double Calories, double Protein, double Fat, double Carbs,
    double BaseTdee, double ActiveKcal);

/// <summary>
/// Расчёт целей питания из данных профиля по формуле Миффлина–Сан-Жеора.
///
/// Архитектура «базовый + динамический расход»:
///   • Базовый TDEE = BMR × фактор базовой активности (NEAT, без тренировок).
///   • Итоговые калории = БазовыйTDEE × дельта_цели + активные калории из трекера.
///   • Белок привязан к массе тела, жир — к % от БАЗОВЫХ калорий (тело),
///     а все активные калории идут в углеводы (топливо для тренировок).
///
/// Все коэффициенты вынесены в публичные const — их можно подстраивать.
/// Чистый модуль без I/O; покрыт тестами.
/// </summary>
public static class NutritionGoalCalculator
{
    // ── Изменяемые константы ───────────────────────────────────────────────────
    public const double LossDelta     = 0.80; // дефицит для похудения (верхняя безопасная граница)
    public const double MaintainDelta = 1.00;
    public const double GainDelta     = 1.10; // профицит для набора (минимизирует прирост жира)

    public const double ProteinGramsPerKg = 1.8;  // белок от массы тела
    public const double FatPercentOfBase  = 0.30; // доля жира от БАЗОВЫХ калорий

    private const double KcalPerGramFat         = 9.0;
    private const double KcalPerGramProteinCarb = 4.0;

    /// <summary>Фактор активности для базового TDEE (без тренировок).</summary>
    public static double ActivityFactor(ActivityLevel level) => level switch
    {
        ActivityLevel.Sedentary => 1.2,
        ActivityLevel.Light     => 1.375,
        ActivityLevel.Moderate  => 1.55,
        ActivityLevel.High      => 1.725,
        ActivityLevel.VeryHigh  => 1.9,
        _ => 1.2,
    };

    /// <summary>Множитель калорий по цели.</summary>
    public static double GoalDelta(NutritionGoal goal) => goal switch
    {
        NutritionGoal.Loss => LossDelta,
        NutritionGoal.Gain => GainDelta,
        _ => MaintainDelta,
    };

    /// <summary>Базовый обмен (BMR), ккал/сутки — Миффлин–Сан-Жеор.</summary>
    public static double BasalMetabolicRate(bool isMale, double weightKg, double heightCm, int ageYears)
        => 10.0 * weightKg + 6.25 * heightCm - 5.0 * ageYears + (isMale ? 5.0 : -161.0);

    /// <summary>
    /// Полный расчёт целей. <paramref name="activeKcal"/> — калории из трекера активности
    /// за день (по умолчанию 0); они добавляются к калориям и покрываются углеводами.
    /// </summary>
    public static NutritionTargets Calculate(
        bool isMale, double weightKg, double heightCm, int ageYears,
        ActivityLevel activity, NutritionGoal goal, double activeKcal = 0)
    {
        if (weightKg <= 0) throw new ArgumentOutOfRangeException(nameof(weightKg), "Вес должен быть > 0");
        if (heightCm <= 0) throw new ArgumentOutOfRangeException(nameof(heightCm), "Рост должен быть > 0");
        if (ageYears <= 0) throw new ArgumentOutOfRangeException(nameof(ageYears), "Возраст должен быть > 0");

        double active = Math.Max(0, activeKcal);
        double baseTdee   = BasalMetabolicRate(isMale, weightKg, heightCm, ageYears) * ActivityFactor(activity);
        double baseTarget = baseTdee * GoalDelta(goal);     // базовые целевые калории (без активности)
        double totalCalories = baseTarget + active;

        // Белок — от массы тела; жир — % от БАЗОВЫХ калорий; углеводы — остаток (включая активные).
        double protein = ProteinGramsPerKg * weightKg;
        double fat     = FatPercentOfBase * baseTarget / KcalPerGramFat;
        double carbsKcal = totalCalories - protein * KcalPerGramProteinCarb - fat * KcalPerGramFat;
        double carbs   = Math.Max(0, carbsKcal / KcalPerGramProteinCarb);

        return new NutritionTargets(
            Calories:   Math.Round(totalCalories),
            Protein:    Math.Round(protein),
            Fat:        Math.Round(fat),
            Carbs:      Math.Round(carbs),
            BaseTdee:   Math.Round(baseTdee),
            ActiveKcal: Math.Round(active));
    }

    /// <summary>Возраст в полных годах на дату <paramref name="today"/>.</summary>
    public static int AgeYears(DateOnly birthDate, DateOnly today)
    {
        int age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return age;
    }
}
