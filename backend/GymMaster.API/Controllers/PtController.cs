using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/pt")]
[Authorize(Roles = RoleNames.Pt)]
public sealed class PtController : ApiControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public PtController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    // AC-03: PT chi thay danh sach member duoc phan cong cho minh.
    [HttpGet("members")]
    public async Task<IActionResult> GetAssignedMembers(CancellationToken cancellationToken)
    {
        var result = await _assignmentService.GetAssignedMembersAsync(User, cancellationToken);
        return ToActionResult(result);
    }
}
