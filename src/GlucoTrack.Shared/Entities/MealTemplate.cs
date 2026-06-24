namespace GlucoTrack.Shared.Entities;

public class MealTemplate : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public bool HasImage { get; set; }
    public ICollection<MealTemplateItem> Items { get; set; } = [];
}

public class MealTemplateItem
{
    public Guid Id { get; set; }
    public Guid MealTemplateId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Grams { get; set; }
    public int MeasureType { get; set; }
    public double? PieceWeightG { get; set; }

    public MealTemplate? MealTemplate { get; set; }
}
