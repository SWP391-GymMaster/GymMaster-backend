using System.Text.Json.Serialization;

namespace GymMaster.API.DTOs;

// Member/Staff yeu cau tao link thanh toan cho 1 membership dang PendingPayment.
public sealed record CreateVnPayPaymentRequest(long MembershipId);

// Tra ve link de FE redirect sang trang VNPay.
public sealed record CreateVnPayPaymentResponse(
    long PaymentId,
    long MembershipId,
    decimal Amount,
    string PayUrl);

// Trang thai 1 payment (cho FE poll sau khi tu VNPay redirect ve).
public sealed record VnPayPaymentStatusResponse(
    long PaymentId,
    long MembershipId,
    string Status,
    string MembershipStatus,
    DateTime? PaidAt);

// Response tra cho VNPay o endpoint IPN — VNPay yeu cau dung dang { RspCode, Message }.
public sealed record VnPayIpnResponse(
    [property: JsonPropertyName("RspCode")] string RspCode,
    [property: JsonPropertyName("Message")] string Message);
