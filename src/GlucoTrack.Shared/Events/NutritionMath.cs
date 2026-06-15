namespace GlucoTrack.Shared.Events;

/// <summary>
/// Пересчёт порции продукта в граммы и коэффициент к значениям «на 100 г».
/// Единая точка для штучных (MeasureType==1) и весовых продуктов — раньше эта
/// формула дублировалась по страницам и была источником багов со штучным весом.
/// </summary>
public static class NutritionMath
{
    public const int ByWeight = 0;
    public const int ByPiece = 1;

    /// <summary>Фактический вес порции, г. Для штучных: количество × вес одной штуки (по умолчанию 100 г).</summary>
    public static double EffectiveGrams(int measureType, double grams, double? pieceWeightG) =>
        measureType == ByPiece ? grams * (pieceWeightG ?? 100) : grams;

    /// <summary>Коэффициент к значениям «на 100 г».</summary>
    public static double ScaleFactor(double effectiveGrams) => effectiveGrams / 100.0;
}
