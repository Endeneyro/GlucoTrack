namespace GlucoTrack.Shared.Entities;

public class UserProfile : EntityBase
{
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Gender { get; set; }        // 0=мужской 1=женский
    public int? DiabetesType { get; set; }  // 1=Тип1 2=Тип2 3=LADA 4=другой
    public int? DiagnosisYear { get; set; }
}
