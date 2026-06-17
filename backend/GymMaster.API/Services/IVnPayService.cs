using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IVnPayService
{
    // Tao URL thanh toan VNPay cho 1 membership dang PendingPayment.
    Task<AuthServiceResult<CreateVnPayPaymentResponse>> CreatePaymentUrlAsync(
        CreateVnPayPaymentRequest request,
        string ipAddress,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // Xu ly IPN (server-to-server) — NGUON SU THAT de kich hoat membership.
    // Tra ve dung shape { RspCode, Message } ma VNPay yeu cau.
    Task<VnPayIpnResponse> HandleIpnAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken);

    // Xu ly redirect tu trinh duyet (return URL): verify chu ky + tra trang thai cho FE.
    Task<AuthServiceResult<VnPayPaymentStatusResponse>> HandleReturnAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken);
}
