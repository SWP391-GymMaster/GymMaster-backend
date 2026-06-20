namespace GymMaster.API.Entities;

public sealed class TrainerNote
{
    public long Id { get; set; }

    public long TrainerId { get; set; }

    public TrainerProfile Trainer { get; set; } = null!;

    public long MemberId { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public DateOnly NoteDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public string Content { get; set; } = string.Empty;

    public long? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
