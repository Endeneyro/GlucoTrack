using GlucoTrack.Shared.DTOs.Sync;

namespace GlucoTrack.Client.Services;

public static class ProductNutritionInfo
{
    public record NutrientItem(string Label, double Value, string Color);

    private static readonly (string Label, string Color, Func<ProductDto, double?> Selector)[] VitaminFields =
    {
        ("A",   "#F97316", p => p.VitaminA),
        ("C",   "#FACC15", p => p.VitaminC),
        ("D",   "#84CC16", p => p.VitaminD),
        ("E",   "#22C55E", p => p.VitaminE),
        ("K",   "#10B981", p => p.VitaminK),
        ("H",   "#F472B6", p => p.VitaminH),
        ("B1",  "#EF4444", p => p.VitaminB1),
        ("B2",  "#FB923C", p => p.VitaminB2),
        ("B3",  "#FBBF24", p => p.VitaminB3),
        ("B4",  "#A3E635", p => p.VitaminB4),
        ("B5",  "#4ADE80", p => p.VitaminB5),
        ("B6",  "#2DD4BF", p => p.VitaminB6),
        ("B9",  "#F43F5E", p => p.VitaminB9),
        ("B12", "#FB7185", p => p.VitaminB12),
    };

    private static readonly (string Label, string Color, Func<ProductDto, double?> Selector)[] MacroFields =
    {
        ("Ca", "#2563EB", p => p.Calcium),
        ("P",  "#3B82F6", p => p.Phosphorus),
        ("K",  "#60A5FA", p => p.Potassium),
        ("Na", "#1D4ED8", p => p.Sodium),
        ("Cl", "#0EA5E9", p => p.Chlorine),
        ("Mg", "#38BDF8", p => p.Magnesium),
        ("S",  "#1E40AF", p => p.Sulfur),
    };

    private static readonly (string Label, string Color, Func<ProductDto, double?> Selector)[] MicroFields =
    {
        ("Fe", "#9333EA", p => p.Iron),
        ("Zn", "#A855F7", p => p.Zinc),
        ("Mn", "#D946EF", p => p.Manganese),
        ("Cu", "#E879F9", p => p.Copper),
        ("Se", "#C084FC", p => p.Selenium),
        ("I",  "#7E22CE", p => p.Iodine),
        ("F",  "#6D28D9", p => p.Fluorine),
        ("Cr", "#8B5CF6", p => p.Chromium),
    };

    public static List<NutrientItem> GetVitamins(ProductDto p) => Build(VitaminFields, p);
    public static List<NutrientItem> GetMacroelements(ProductDto p) => Build(MacroFields, p);
    public static List<NutrientItem> GetMicroelements(ProductDto p) => Build(MicroFields, p);

    private static List<NutrientItem> Build((string Label, string Color, Func<ProductDto, double?> Selector)[] fields, ProductDto p) =>
        fields
            .Select(f => (f.Label, f.Color, Value: f.Selector(p)))
            .Where(x => x.Value.HasValue && x.Value.Value > 0)
            .Select(x => new NutrientItem(x.Label, x.Value!.Value, x.Color))
            .ToList();
}
