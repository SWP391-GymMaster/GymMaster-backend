namespace GymMaster.API.Entities;

public sealed class TrainerProfile
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public string? Specialty { get; set; }

    public string? Bio { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int? YearsOfExperience { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
