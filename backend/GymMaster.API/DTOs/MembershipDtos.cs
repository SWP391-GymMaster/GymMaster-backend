namespace GymMaster.API.DTOs;

public sealed record SellMembershipRequest(
    long MemberId,
    long PackageId,
    DateOnly? StartDate);

public sealed record ConfirmPaymentRequest(
    decimal Amount,
    byte Method);

public sealed record RenewMembershipRequest(long PackageId);

public sealed record RenewalRequestRequest(long PackageId);

public sealed record MembershipResponse(
    long Id,
    long MemberId,
    long PackageId,
    string PackageName,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int DaysRemaining,
    bool IsExpiringSoon,
    DateTime CreatedAt);

public sealed record PaymentResponse(
    long Id,
    long MembershipId,
    decimal Amount,
    string Method,
    string Status,
    DateTime? PaidAt);
