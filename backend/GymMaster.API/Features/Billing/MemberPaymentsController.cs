using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Billing;
[ApiController]
[Route("api/v1/members")]
[Authorize]
public sealed class MemberPaymentsController : ApiControllerBase
{
    private readonly IPaymentService _paymentService;

    public MemberPaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("{memberId:long}/payments")]
    public async Task<IActionResult> ListByMember(
        long memberId,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.GetByMemberAsync(memberId, User, cancellationToken);
        return ToActionResult(result);
    }
}
