namespace GlucoTrack.Shared.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}
