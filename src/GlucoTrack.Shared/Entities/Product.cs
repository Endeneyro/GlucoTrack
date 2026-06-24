namespace GlucoTrack.Shared.Entities;

public enum ProductCategory
{
    Other = 0, Meat = 1, Fish = 2, Dairy = 3, Grains = 4,
    Vegetables = 5, Fruits = 6, Bakery = 7, Drinks = 8,
    Nuts = 9, Sweets = 10, Eggs = 11, Legumes = 12,
    FermentedDairy = 13, Sausages = 14, Alcohol = 15, Pasta = 16, SemiFinished = 17,
    Mushrooms = 18, Poultry = 19, Sauces = 20
}

public enum ProductOwnerType { Base = 0, Private = 1, Shared = 2 }
public enum ProductMeasureType { ByWeight = 0, ByPiece = 1 }

public class Product
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = string.Empty;
    public ProductCategory Category { get; set; }
    public ProductOwnerType OwnerType { get; set; }
    public ProductMeasureType MeasureType { get; set; }
    public double? PieceWeightG { get; set; }
    public double DefaultServingG { get; set; } = 100;

    public bool IsComposite { get; set; }
    public double? TotalYieldG { get; set; }

    public string? Manufacturer { get; set; }
    public bool HasImage { get; set; }

    // Set when this product is a resilient copy of a Shared product, created automatically
    // when a user favorites someone else's shared product (so it survives if the
    // original owner deletes their product or un-shares it).
    public Guid? ClonedFromProductId { get; set; }

    public bool IsVerified { get; set; }
    public string? Barcode { get; set; }
    public int LikesCount { get; set; }
    public int DislikesCount { get; set; }

    public int? GlycemicIndex { get; set; }  // 0–100

    // Nutrients per 100g
    public double CaloriesPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double FatPer100g { get; set; }
    public double CarbsPer100g { get; set; }
    public double FiberPer100g { get; set; }
    // Vitamins (мг, если не указано иное)
    public double? VitaminA  { get; set; }  // мг
    public double? VitaminC  { get; set; }  // мг
    public double? VitaminD  { get; set; }  // мкг
    public double? VitaminB1 { get; set; }  // мг (Тиамин)
    public double? VitaminB2 { get; set; }  // мг (Рибофлавин)
    public double? VitaminB3 { get; set; }  // мг (Ниацин/PP)
    public double? VitaminB4 { get; set; }  // мг (Холин)
    public double? VitaminB5 { get; set; }  // мг (Пантотеновая к-та)
    public double? VitaminB6 { get; set; }  // мг (Пиридоксин)
    public double? VitaminB9 { get; set; }  // мкг (Фолат/B9)
    public double? VitaminB12 { get; set; } // мкг
    public double? VitaminE  { get; set; }  // мг
    public double? VitaminK  { get; set; }  // мкг
    public double? VitaminH  { get; set; }  // мкг (Биотин)
    // Minerals
    public double? Calcium    { get; set; }  // мг
    public double? Phosphorus { get; set; }  // мг
    public double? Potassium  { get; set; }  // мг
    public double? Sodium     { get; set; }  // мг
    public double? Chlorine   { get; set; }  // мг
    public double? Magnesium  { get; set; }  // мг
    public double? Sulfur     { get; set; }  // мг
    public double? Iron       { get; set; }  // мг
    public double? Zinc       { get; set; }  // мг
    public double? Selenium   { get; set; }  // мкг
    public double? Iodine     { get; set; }  // мкг
    public double? Manganese  { get; set; }  // мг
    public double? Copper     { get; set; }  // мкг
    public double? Fluorine   { get; set; }  // мкг
    public double? Chromium   { get; set; }  // мкг

    public ICollection<ProductIngredient> Ingredients { get; set; } = [];
}

public class ProductIngredient
{
    public Guid Id { get; set; }
    public Guid CompositeProductId { get; set; }
    public Guid IngredientProductId { get; set; }
    public double Grams { get; set; }

    public Product? CompositeProduct { get; set; }
    public Product? IngredientProduct { get; set; }
}

public class ProductReaction
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public int Reaction { get; set; } // 1=like, -1=dislike
    public DateTime CreatedAtUtc { get; set; }
}

public class ProductHide
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class ProductUsage
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public int UseCount { get; set; }
    public DateTime LastUsedAt { get; set; }
}
