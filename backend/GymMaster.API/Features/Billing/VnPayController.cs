using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Billing;
[ApiController]
[Route("api/v1/payments/vnpay")]
public sealed class VnPayController : ApiControllerBase
{
    private readonly IVnPayService _vnPayService;

    public VnPayController(IVnPayService vnPayService)
    {
        _vnPayService = vnPayService;
    }

    // Member (so huu) hoac Staff tao link thanh toan cho 1 membership PendingPayment.
    [HttpPost("create-url")]
    [Authorize]
    public async Task<IActionResult> CreateUrl(
        [FromBody] CreateVnPayPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _vnPayService.CreatePaymentUrlAsync(request, ipAddress, User, cancellationToken);
        return ToActionResult(result);
    }

    // IPN: VNPay goi server-to-server. Khong auth (bao ve bang chu ky). Tra dung shape { RspCode, Message }.
    [HttpGet("ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> Ipn(CancellationToken cancellationToken)
    {
        var query = ToDictionary();
        var result = await _vnPayService.HandleIpnAsync(query, cancellationToken);
        return Ok(result);
    }

    // Return URL: VNPay redirect trinh duyet ve. Verify chu ky + tra trang thai cho FE.
    [HttpGet("return")]
    [AllowAnonymous]
    public async Task<IActionResult> Return(CancellationToken cancellationToken)
    {
        var query = ToDictionary();
        var result = await _vnPayService.HandleReturnAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    private IReadOnlyDictionary<string, string> ToDictionary()
    {
        return Request.Query.ToDictionary(item => item.Key, item => item.Value.ToString());
    }
}
