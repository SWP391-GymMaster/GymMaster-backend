namespace GymMaster.API.Entities;

public sealed class TrainerAssignment
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public long TrainerId { get; set; }

    public TrainerProfile Trainer { get; set; } = null!;

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? EndDate { get; set; }

    // DB: TINYINT — 1 = Active, 2 = Ended
    public byte Status { get; set; } = AssignmentStatuses.Active;

    public long CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public static class AssignmentStatuses
{
    public const byte Active = 1;
    public const byte Ended = 2;
}
