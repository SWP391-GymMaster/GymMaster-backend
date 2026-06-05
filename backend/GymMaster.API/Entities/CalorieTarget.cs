namespace GymMaster.API.Entities;

public sealed class CalorieTarget
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public decimal DailyCalories { get; set; }

    public decimal? ProteinG { get; set; }

    public decimal? CarbG { get; set; }

    public decimal? FatG { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
