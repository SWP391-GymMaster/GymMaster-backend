namespace GymMaster.API.Entities;

public sealed class ProgressLog
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;

    public decimal? WeightKg { get; set; }

    public decimal? BodyFatPercent { get; set; }

    public decimal? ChestCm { get; set; }

    public decimal? WaistCm { get; set; }

    public decimal? HipCm { get; set; }

    public string? Note { get; set; }

    public long? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
