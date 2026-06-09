namespace GymMaster.API.Entities;

public sealed class Payment
{
    public long Id { get; set; }

    public long MembershipId { get; set; }

    public decimal Amount { get; set; }

    // 1=Cash, 2=Transfer, 3=Card
    public byte Method { get; set; }

    // 1=Pending, 2=Paid, 3=Refunded
    public byte Status { get; set; } = PaymentStatus.Pending;

    public DateTime? PaidAt { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Membership Membership { get; set; } = null!;
}

public static class PaymentMethod
{
    public const byte Cash = 1;
    public const byte Transfer = 2;
    public const byte Card = 3;
}

public static class PaymentStatus
{
    public const byte Paid = 1;
    public const byte Pending = 2;
    public const byte Refunded = 3;
}
