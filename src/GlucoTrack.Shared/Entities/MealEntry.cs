namespace GlucoTrack.Shared.Entities;

public enum MealType { Breakfast, Lunch, Dinner, Snack }

public class MealEntry : EntityBase
{
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public MealType MealType { get; set; }
    public Guid ProductId { get; set; }
    public double Grams { get; set; }
    public Guid? LinkedEventId { get; set; }
}
