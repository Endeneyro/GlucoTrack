namespace GlucoTrack.Shared.Entities;

public class InviteCode
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public Guid? UsedByUserId { get; set; }
}
