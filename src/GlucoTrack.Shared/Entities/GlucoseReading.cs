namespace GlucoTrack.Shared.Entities;

public class GlucoseReading : EntityBase
{
    public DateTime MeasuredAtUtc { get; set; }
    public double ValueMmol { get; set; }
}
