namespace GymMaster.API.Features.Billing;
public sealed record PaymentHistoryResponse(
    long Id,
    long MembershipId,
    long MemberId,
    string MemberName,
    string MemberEmail,
    long PackageId,
    string PackageName,
    decimal Amount,
    string PaymentMethod,
    string Status,
    string MembershipStatus,
    DateTime PaymentDate,
    DateTime? PaidAt,
    DateTime CreatedAt,
    long CreatedByUserId,
    string? CreatedByName);

public sealed record PaymentSummaryResponse(
    DateOnly? From,
    DateOnly? To,
    int TotalPayments,
    int PaidPayments,
    int PendingPayments,
    decimal Revenue,
    IReadOnlyList<PaymentMethodSummaryResponse> ByMethod,
    IReadOnlyList<DailyRevenueResponse> ByDay);

public sealed record PaymentMethodSummaryResponse(
    string PaymentMethod,
    int Count,
    decimal Amount);

public sealed record DailyRevenueResponse(
    DateOnly Date,
    int Count,
    decimal Amount);
