using GlucoTrack.Shared.Entities;

namespace GlucoTrack.Shared.Calculations;

/// <summary>
/// Пресеты кривой действия по препарату инсулина. Время пика взято из клинических
/// данных / практики сообщества OpenAPS; DIA остаётся индивидуальным (измеряется/настраивается).
/// </summary>
public static class InsulinCurvePresets
{
    /// <summary>Рекомендуемое время пика активности (мин) для препарата.</summary>
    public static int PeakMinutes(InsulinBrand brand) => brand switch
    {
        InsulinBrand.Fiasp     => 55,
        InsulinBrand.Lyumjev   => 45,
        InsulinBrand.Humalog   => 75,
        InsulinBrand.NovoRapid => 75,
        InsulinBrand.Apidra    => 65,
        _                      => (int)InsulinOnBoard.DefaultPeakMinutes, // прочий ультракороткий
    };
}
