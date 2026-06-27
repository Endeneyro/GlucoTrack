using GlucoTrack.Shared.DTOs.Sync;

namespace GlucoTrack.Shared.Calculations;

/// <summary>
/// Определяет параметры кривой действия (DIA, пик) активного болюсного инсулина
/// пользователя. Кривая принадлежит конкретному инсулину (<see cref="UserInsulinDto"/>),
/// а не пользователю; до выбора инсулина — безопасные дефолты.
/// </summary>
public static class InsulinProfileResolver
{
    /// <summary>
    /// Возвращает (DIA, пик) активного болюсного инсулина из списка.
    /// Если активного болюса нет — фолбэк на <paramref name="fallbackDiaHours"/> и дефолтный пик.
    /// </summary>
    public static (double DiaHours, double PeakMinutes) ActiveBolusCurve(
        IEnumerable<UserInsulinDto>? insulins,
        double fallbackDiaHours = InsulinOnBoard.DefaultDiaHours)
    {
        double dia = fallbackDiaHours > 0 ? fallbackDiaHours : InsulinOnBoard.DefaultDiaHours;

        var bolus = insulins?
            .FirstOrDefault(i => i.InsulinType == 0 && i.IsActive && !i.IsDeleted);

        if (bolus is not null)
        {
            if (bolus.DiaHours > 0) dia = bolus.DiaHours;
            double peak = bolus.PeakMinutes > 0 ? bolus.PeakMinutes : InsulinOnBoard.DefaultPeakMinutes;
            return (dia, peak);
        }

        return (dia, InsulinOnBoard.DefaultPeakMinutes);
    }
}
