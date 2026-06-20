namespace GymMaster.API.Entities;

public sealed class ExerciseCatalog
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? MuscleGroup { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
