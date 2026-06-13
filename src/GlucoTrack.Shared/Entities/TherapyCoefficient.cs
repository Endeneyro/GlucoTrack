namespace GlucoTrack.Shared.Entities;

public class TherapyCoefficient : EntityBase
{
    public TimeOnly FromTime { get; set; }
    public TimeOnly ToTime { get; set; }
    /// <summary>IC — insulin-to-carb ratio: grams of carbs per 1 IU</summary>
    public double InsulinToCarbRatio { get; set; }
    /// <summary>ISF — insulin sensitivity factor: mmol/L per 1 IU</summary>
    public double InsulinSensitivityFactor { get; set; }
}
