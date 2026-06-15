using GlucoTrack.Shared.DTOs.Sync;

namespace GlucoTrack.Client.Models;

public static class PlannedEventMeta
{
    public static string Icon(PlannedEventType t) => t switch
    {
        PlannedEventType.Meal     => "🍽",
        PlannedEventType.Glucose  => "📈",
        PlannedEventType.Insulin  => "💉",
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
