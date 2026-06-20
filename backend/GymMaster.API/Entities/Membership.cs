namespace GymMaster.API.Entities;

public sealed class Membership
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public long PackageId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.PendingPayment;

    public long CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public MembershipPackage Package { get; set; } = null!;
}
