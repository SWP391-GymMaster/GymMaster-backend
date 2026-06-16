namespace GymMaster.API.Entities;

// Spec 004 — check_ins: ghi nhan 1 luot den phong tap.
// Map theo schema DB THAT: cot CreatedBy (nullable) = null khi member tu check-in (FR-CHK-06).
public sealed class CheckIn
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public DateTime CheckInAt { get; set; } = DateTime.UtcNow;

    public long? CreatedBy { get; set; }
}
