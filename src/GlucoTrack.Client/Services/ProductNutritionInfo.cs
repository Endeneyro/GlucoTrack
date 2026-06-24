using GlucoTrack.Shared.DTOs.Sync;

namespace GlucoTrack.Client.Services;

public static class ProductNutritionInfo
{
    private static readonly (string Label, Func<ProductDto, double?> Selector)[] MicroFields =
    {
        ("Вит. A", p => p.VitaminA), ("Вит. C", p => p.VitaminC), ("Вит. D", p => p.VitaminD),
        ("Вит. E", p => p.VitaminE), ("Вит. B1", p => p.VitaminB1), ("Вит. B2", p => p.VitaminB2),
        ("Вит. B6", p => p.VitaminB6), ("Вит. B12", p => p.VitaminB12),
        ("Кальций", p => p.Calcium), ("Калий", p => p.Potassium), ("Натрий", p => p.Sodium),
        ("Магний", p => p.Magnesium), ("Железо", p => p.Iron), ("Цинк", p => p.Zinc),
    };

    public static List<(string Label, double Value)> GetMicronutrients(ProductDto p) =>
        MicroFields
            .Select(f => (f.Label, Value: f.Selector(p)))
            .Where(x => x.Value.HasValue && x.Value.Value > 0)
            .Select(x => (x.Label, x.Value!.Value))
            .ToList();
}
