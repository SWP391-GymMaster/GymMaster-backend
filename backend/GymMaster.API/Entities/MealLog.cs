namespace GymMaster.API.Entities;

public sealed class MealLog
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public DateOnly LogDate { get; set; }

    public MealType MealType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MealLogItem> Items { get; set; } = [];
}
