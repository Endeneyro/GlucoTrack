namespace GlucoTrack.Shared.Entities;

public enum InsulinType { Bolus, Basal }

public class InsulinInjection : EntityBase
{
    public DateTime InjectedAtUtc { get; set; }
    public double Units { get; set; }
    public InsulinType InsulinType { get; set; }
    public double? Carbs { get; set; }
    public double? GlucoseBefore { get; set; }
}
