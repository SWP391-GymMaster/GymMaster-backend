namespace GymMaster.API.Entities;

public enum MembershipStatus : byte
{
    PendingPayment = 0,
    Active = 1,
    Expired = 2,
    Cancelled = 3
}

public enum PaymentMethod : byte
{
    Cash = 1,
    Transfer = 2,
    Card = 3
}

public enum PaymentStatus : byte
{
    Pending = 0,
    Paid = 1,
    Refunded = 2
}
