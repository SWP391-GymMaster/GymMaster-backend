namespace GymMaster.API.Entities;

public sealed class MembershipPackage
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DurationDays { get; set; }

    public decimal Price { get; set; }

    // 1=Active, 2=Inactive
    public byte Status { get; set; } = 1;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class MembershipPackageStatus
{
    public const byte Active = 1;
    public const byte Inactive = 2;
}
