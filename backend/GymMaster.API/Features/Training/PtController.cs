using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;
using GymMaster.API.Features.CheckIns;

namespace GymMaster.API.Features.Training;
[ApiController]
[Route("api/v1/pt")]
[Authorize(Roles = RoleNames.Pt)]
public sealed class PtController : ApiControllerBase
{
    private readonly IAssignmentService _assignmentService;
    private readonly ICheckInService _checkInService;

    public PtController(IAssignmentService assignmentService, ICheckInService checkInService)
    {
        _assignmentService = assignmentService;
        _checkInService = checkInService;
    }

    // AC-03: PT chi thay danh sach member duoc phan cong cho minh.
    [HttpGet("members")]
    public async Task<IActionResult> GetAssignedMembers(CancellationToken cancellationToken)
    {
        var result = await _assignmentService.GetAssignedMembersAsync(User, cancellationToken);
        return ToActionResult(result);
    }

    // PT check-in cho hoi vien duoc phan cong cho minh (ownership gac trong service).
    [HttpPost("members/{memberId:long}/checkins")]
    public async Task<IActionResult> CheckInAssignedMember(long memberId, CancellationToken cancellationToken)
    {
        var result = await _checkInService.CreateForAssignedMemberAsync(memberId, User, cancellationToken);
        return ToActionResult(result);
    }

    // Trang thai check-in HOM NAY cua cac hoi vien duoc phan cong cho PT.
    [HttpGet("checkins/today")]
    public async Task<IActionResult> GetTodayCheckIns(CancellationToken cancellationToken)
    {
        var result = await _checkInService.ListTodayForTrainerAsync(User, cancellationToken);
        return ToActionResult(result);
    }
}
