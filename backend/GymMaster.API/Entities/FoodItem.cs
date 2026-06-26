namespace GymMaster.API.Entities;

public sealed class FoodItem
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal CaloriesPerUnit { get; set; }

    public decimal? ProteinG { get; set; }

    public decimal? CarbG { get; set; }

    public decimal? FatG { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Bo sung cho tinh nang Quet anh AI: mon AI luu Source="AI", ServingSize=100.
    public decimal ServingSize { get; set; } = 100;

    public string Source { get; set; } = "Admin";
}
