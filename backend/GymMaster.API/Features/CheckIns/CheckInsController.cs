using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.CheckIns;
[ApiController]
[Route("api/v1/checkins")]
[Authorize]
public sealed class CheckInsController : ApiControllerBase
{
    private readonly ICheckInService _checkInService;

    public CheckInsController(ICheckInService checkInService)
    {
        _checkInService = checkInService;
    }

    // FR-CHK-01: POST /api/v1/checkins  (Staff/Admin tac nghiep, Member tu check-in)
    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff},{RoleNames.Member}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCheckInRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _checkInService.CreateAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    // GET /api/v1/checkins?date=&memberId=  (Admin, Staff)
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? date,
        [FromQuery] long? memberId,
        CancellationToken cancellationToken)
    {
        var result = await _checkInService.ListAsync(date, memberId, cancellationToken);
        return ToActionResult(result);
    }

    // GET /api/v1/members/{id}/checkins  (Admin, Staff, PT, Member self — ownership trong service)
    [HttpGet("/api/v1/members/{id:long}/checkins")]
    public async Task<IActionResult> ListByMember(long id, CancellationToken cancellationToken)
    {
        var result = await _checkInService.ListByMemberAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }
}
