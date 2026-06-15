namespace GlucoTrack.Shared.Entities;

public class UserInsulin : EntityBase
{
    public string Name { get; set; } = "";
    public int InsulinType { get; set; }    // 0=болюс 1=базал
    public double? TypicalDose { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}
