namespace GymMaster.API.Features.Billing;
public sealed record SellMembershipRequest(
    long MemberId,
    long PackageId,
    DateOnly? StartDate);

// Method nhan ten phuong thuc dang chuoi ("cash"/"transfer"/"card"), parse sang enum.
public sealed record ConfirmPaymentRequest(
    decimal Amount,
    string Method);

public sealed record RenewMembershipRequest(
    long PackageId,
    string Method);

public sealed record RenewalRequestRequest(long PackageId);

// Bao response theo dung spec 003 §6.
public sealed record SellMembershipResponse(MembershipResponse Membership);

public sealed record ConfirmPaymentResult(
    MembershipResponse Membership,
    PaymentBrief Payment,
    string Status);

public sealed record PaymentBrief(
    long Id,
    long MembershipId,
    DateTime? PaidAt);

public sealed record MembershipResponse(
    long Id,
    long MemberId,
    long PackageId,
    string PackageName,
    bool SupportsPT,
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
