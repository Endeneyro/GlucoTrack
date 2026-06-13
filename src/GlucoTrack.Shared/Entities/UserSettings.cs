namespace GlucoTrack.Shared.Entities;

public class UserSettings : EntityBase
{
    public double TargetGlucoseLow { get; set; }
    public double TargetGlucoseHigh { get; set; }
    public double TargetGlucose { get; set; }
    public double XeGrams { get; set; } = 12;
    public double DailyCalories { get; set; }
    public double DailyProtein { get; set; }
    public double DailyFat { get; set; }
    public double DailyCarbs { get; set; }
    public bool DisclaimerAccepted { get; set; }
}
