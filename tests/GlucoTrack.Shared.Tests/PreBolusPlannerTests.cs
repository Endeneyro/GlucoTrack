using GlucoTrack.Shared.Events;

namespace GlucoTrack.Shared.Tests;

public class PreBolusPlannerTests
{
    [Fact]
    public void WeightedGi_NullWhenNoCarbs()
    {
        Assert.Null(PreBolusPlanner.WeightedGi(new[] { (70.0, 0.0) }));
        Assert.Null(PreBolusPlanner.WeightedGi(Array.Empty<(double, double)>()));
    }

    [Fact]
    public void WeightedGi_CarbWeighted()
    {
        // ГИ 70 на 30 г + ГИ 50 на 10 г → (70*30 + 50*10)/40 = 65
        Assert.Equal(65.0, PreBolusPlanner.WeightedGi(new[] { (70.0, 30.0), (50.0, 10.0) }));
    }

    // MinutesForGi(gi) = MinutesForGiAndGlucose(gi, 0) — «сахар не известен», нормальная ветка
    [Theory]
    [InlineData(null,  10)]   // нет данных → 10 мин
    [InlineData(55.0,  0)]    // низкий ГИ  → 0 мин (низкий ГИ, нормальный сахар — сразу)
    [InlineData(56.0, 15)]    // граница среднего
    [InlineData(69.0, 15)]    // средний
    [InlineData(70.0, 20)]    // граница высокого
    [InlineData(85.0, 20)]    // высокий
    public void MinutesForGi_Thresholds(double? gi, int expected)
        => Assert.Equal(expected, PreBolusPlanner.MinutesForGi(gi));

    // ── MinutesForGiAndGlucose: гипо-зона ────────────────────────────────────

    [Theory]
    [InlineData(null,  3.5, 0)]   // любой ГИ при гипо → 0 мин
    [InlineData(85.0,  4.0, 0)]   // высокий ГИ, но гипо → 0 мин
    [InlineData(85.0,  4.4, 0)]   // < 4.5 → гипо-ветка
    public void MinutesForGiAndGlucose_Hypo_AlwaysZero(double? gi, double glucose, int expected)
        => Assert.Equal(expected, PreBolusPlanner.MinutesForGiAndGlucose(gi, glucose));

    // ── MinutesForGiAndGlucose: нормальный сахар (4.5–7.0) ──────────────────

    [Theory]
    [InlineData(null,  6.0, 10)]  // неизвестный ГИ
    [InlineData(50.0,  5.5,  0)]  // низкий ГИ → 0 мин
    [InlineData(60.0,  6.0, 15)]  // средний ГИ
    [InlineData(75.0,  6.0, 20)]  // высокий ГИ
    public void MinutesForGiAndGlucose_Normal(double? gi, double glucose, int expected)
        => Assert.Equal(expected, PreBolusPlanner.MinutesForGiAndGlucose(gi, glucose));

    // ── MinutesForGiAndGlucose: повышенный сахар (> 7.0) ────────────────────

    [Theory]
    [InlineData(null,  8.0, 15)]  // неизвестный ГИ → 15 мин
    [InlineData(50.0,  9.0, 10)]  // низкий ГИ, но сахар высок → 10 мин
    [InlineData(60.0, 10.0, 20)]  // средний ГИ
    [InlineData(75.0, 12.0, 25)]  // высокий ГИ + высокий сахар → максимум
    public void MinutesForGiAndGlucose_Elevated(double? gi, double glucose, int expected)
        => Assert.Equal(expected, PreBolusPlanner.MinutesForGiAndGlucose(gi, glucose));

    [Theory]
    [InlineData(null, "")]
    [InlineData(50.0, "ГИ 50 — низкий")]
    [InlineData(60.0, "ГИ 60 — средний")]
    [InlineData(75.0, "ГИ 75 — высокий")]
    public void GiLabel_Text(double? gi, string expected)
    {
        Assert.Equal(expected, PreBolusPlanner.GiLabel(gi));
    }

    [Fact]
    public void InsulinTimeIsPast_DetectsPast()
    {
        var now = new DateTime(2026, 6, 13, 15, 0, 0);
        // еда в 15:02, пред-болюс 5 мин → инсулин в 14:57 < 15:00 → в прошлом
        Assert.True(PreBolusPlanner.InsulinTimeIsPast(now.AddMinutes(2), 5, now));
        // еда в 15:10, пред-болюс 5 мин → инсулин в 15:05 > 15:00 → не в прошлом
        Assert.False(PreBolusPlanner.InsulinTimeIsPast(now.AddMinutes(10), 5, now));
    }

    [Fact]
    public void SuggestedMealTime_RoundsUpToFive()
    {
        var now = new DateTime(2026, 6, 13, 15, 0, 30); // 15:00:30
        // now + 5 мин = 15:05:30 → округление вверх до 15:10
        Assert.Equal(new DateTime(2026, 6, 13, 15, 10, 0), PreBolusPlanner.SuggestedMealTime(now, 5));
    }

    [Fact]
    public void SuggestedMealTime_ExactBoundaryUnchanged()
    {
        var now = new DateTime(2026, 6, 13, 15, 0, 0);
        // now + 5 мин = 15:05:00 ровно → остаётся 15:05
        Assert.Equal(new DateTime(2026, 6, 13, 15, 5, 0), PreBolusPlanner.SuggestedMealTime(now, 5));
    }
}
