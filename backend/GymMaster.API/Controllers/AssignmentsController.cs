using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/assignments")]
[Authorize]
public sealed class AssignmentsController : ApiControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    // FR-PT-01 / FR-PT-02: Admin phan cong PT cho Member.
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Assign(
        [FromBody] AssignTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assignmentService.AssignAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    // Lay danh sach member co the phan cong PT (Admin only).
    [HttpGet("candidates/members")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetCandidateMembers(
        [FromQuery] string? query,
        [FromQuery] bool includeAssigned = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _assignmentService.GetCandidateMembersAsync(query, includeAssigned, cancellationToken);
        return ToActionResult(result);
    }

    // Lay danh sach PT co the duoc phan cong (Admin only).
    [HttpGet("candidates/trainers")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetCandidateTrainers(
        [FromQuery] string? query,
        [FromQuery] string? specialty,
        CancellationToken cancellationToken = default)
    {
        var result = await _assignmentService.GetCandidateTrainersAsync(query, specialty, cancellationToken);
        return ToActionResult(result);
    }
}
