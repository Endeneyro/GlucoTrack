namespace GlucoTrack.Client.Models;

public record PlannedEventDto(
    Guid Id,
    DateTime PlannedAtUtc,
    int EventType,
    string? Note,
    Guid? GroupId,
    List<PlannedMealItem>? MealItems,
    bool IsDone,
    DateTime UpdatedAtUtc,
    bool IsDeleted
);

public record PlannedMealItem(Guid ProductId, string ProductName, double Grams);

public enum PlannedEventType
{
    Meal     = 0,
    Glucose  = 1,
    Insulin  = 2,
    Weight   = 3,
    Activity = 4
}

public static class PlannedEventMeta
{
    public static string Icon(PlannedEventType t) => t switch
    {
        PlannedEventType.Meal     => "🍽",
        PlannedEventType.Glucose  => "📈",
        PlannedEventType.Insulin  => "💊",
        PlannedEventType.Weight   => "⚖️",
        PlannedEventType.Activity => "🏃",
        _ => "📌"
    };

    public static string Label(PlannedEventType t) => t switch
    {
        PlannedEventType.Meal     => "Приём пищи",
        PlannedEventType.Glucose  => "Измерение сахара",
        PlannedEventType.Insulin  => "Инсулин",
        PlannedEventType.Weight   => "Взвешивание",
        PlannedEventType.Activity => "Физическая активность",
        _ => "Событие"
    };

    public static string ToastMessage(PlannedEventType t) => t switch
    {
        PlannedEventType.Meal     => "Время приёма пищи!",
        PlannedEventType.Glucose  => "Время измерить уровень сахара",
        PlannedEventType.Insulin  => "Время укола инсулина",
        PlannedEventType.Weight   => "Время взвеситься",
        PlannedEventType.Activity => "Время активности!",
        _ => "Напоминание"
    };

    public static bool NeedsForm(PlannedEventType t) => t switch
    {
        PlannedEventType.Activity => false,
        _ => true
    };
}
