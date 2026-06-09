namespace GymMaster.API.Entities;

public sealed class WorkoutExercise
{
    public long Id { get; set; }

    public long WorkoutPlanId { get; set; }

    public WorkoutPlan WorkoutPlan { get; set; } = null!;

    public long ExerciseId { get; set; }

    public ExerciseCatalog Exercise { get; set; } = null!;

    public short SortOrder { get; set; }

    public byte? Sets { get; set; }

    public short? Reps { get; set; }

    public decimal? WeightKg { get; set; }

    public short? DurationMinutes { get; set; }

    public short? RestSeconds { get; set; }

    public string? Note { get; set; }
}
