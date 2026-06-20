namespace GymMaster.API.Entities;

public sealed class WorkoutPlan
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public long TrainerId { get; set; }

    public TrainerProfile Trainer { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Goal { get; set; }

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? EndDate { get; set; }

    // DB: TINYINT — 1 = Active, 2 = Completed, 3 = Cancelled
    public byte Status { get; set; } = WorkoutPlanStatuses.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<WorkoutExercise> Exercises { get; set; } = new List<WorkoutExercise>();
}

public static class WorkoutPlanStatuses
{
    public const byte Active = 1;
    public const byte Completed = 2;
    public const byte Cancelled = 3;
}
