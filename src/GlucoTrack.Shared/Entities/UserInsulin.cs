namespace GlucoTrack.Shared.Entities;

public class UserInsulin : EntityBase
{
    public string Name { get; set; } = "";
    public int InsulinType { get; set; }    // 0=болюс 1=базал
    public double? TypicalDose { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }

    // Параметры кривой действия (для экспоненциальной модели IOB)
    public int Brand { get; set; }                  // InsulinBrand (0=Other)
    public int PeakMinutes { get; set; } = 75;      // время пика активности, мин
    public double DiaHours { get; set; } = 4.0;     // длительность действия, ч
}
