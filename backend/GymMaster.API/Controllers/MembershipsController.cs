using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/memberships")]
[Authorize]
public sealed class MembershipsController : ApiControllerBase
{
    private readonly IMembershipService _membershipService;

    public MembershipsController(IMembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    // FR-MS-01
    [HttpPost("sell")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> Sell(
        [FromBody] SellMembershipRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.SellAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MS-02
    [HttpPost("{id:long}/payment")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> ConfirmPayment(
        long id,
        [FromBody] ConfirmPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.ConfirmPaymentAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MS-03
    [HttpPost("{id:long}/renew")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> Renew(
        long id,
        [FromBody] RenewMembershipRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.RenewAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MS-04
    [HttpPost("renewal-request")]
    [Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> CreateRenewalRequest(
        [FromBody] RenewalRequestRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.CreateRenewalRequestAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MS-08 — huy membership (Member: don/goi cua minh; Staff/Admin: bat ky).
    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = $"{RoleNames.Member},{RoleNames.Staff},{RoleNames.Admin}")]
    public async Task<IActionResult> Cancel(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.CancelAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }

    // 🆕 API_CONTRACT_Y: roster tat ca membership (Admin/Staff).
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int? page,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.GetAllAsync(status, page, User, cancellationToken);
        return ToActionResult(result);
    }
}
