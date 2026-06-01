namespace GymMaster.API.Entities;

// AUDIT-01: ghi nhan moi hanh dong mutating quan trong (actor, action, entity, time).
public sealed class AuditLog
{
    public long Id { get; set; }

    public long? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
