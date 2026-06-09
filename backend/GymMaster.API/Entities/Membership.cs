namespace GymMaster.API.Entities;

public sealed class Membership
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public long PackageId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    // 1=PendingPayment, 2=Active, 3=Expired, 4=Cancelled
    public byte Status { get; set; } = MembershipStatus.PendingPayment;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public MemberProfile Member { get; set; } = null!;

    public MembershipPackage Package { get; set; } = null!;
}

public static class MembershipStatus
{
    public const byte Active = 1;
    public const byte Expired = 2;
    public const byte PendingPayment = 3;
    public const byte Cancelled = 4;
}
