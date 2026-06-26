namespace GlucoTrack.Shared.Entities;

public class PlannedEvent : EntityBase
{
    public DateTime PlannedAtUtc { get; set; }
    public int EventType { get; set; }
    public string? Note { get; set; }
    public Guid? GroupId { get; set; }
    public bool IsDone { get; set; }
    public int? InsulinSubtype { get; set; }
    public ICollection<PlannedEventMealItem> MealItems { get; set; } = [];
}

public class PlannedEventMealItem
{
    public Guid Id { get; set; }
    public Guid PlannedEventId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Grams { get; set; }
    public int MeasureType { get; set; }
    public double? PieceWeightG { get; set; }

    public PlannedEvent? PlannedEvent { get; set; }
}
