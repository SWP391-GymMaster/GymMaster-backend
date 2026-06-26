namespace GymMaster.API.Entities;

public sealed class MembershipPackage
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public short DurationDays { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    // 0 = goi thuong, 1 = goi co ho tro PT (cot SupportsPT, them o 008_package_supports_pt.sql).
    // Quyen PT cua member suy ra dong tu goi dang active (spec 003 FR-PKG-03/04).
    public bool SupportsPT { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
