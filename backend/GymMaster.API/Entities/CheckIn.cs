namespace GymMaster.API.Entities;

public sealed class CheckIn
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public DateTime CheckInAt { get; set; } = DateTime.UtcNow;

    // null = member tự check-in
    public long? CreatedBy { get; set; }

    public MemberProfile Member { get; set; } = null!;
}
