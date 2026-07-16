using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Training;
[ApiController]
[Route("api/v1/workout-plans")]
[Authorize]
public sealed class WorkoutPlansController : ApiControllerBase
{
    private readonly IWorkoutPlanService _workoutPlanService;

    public WorkoutPlansController(IWorkoutPlanService workoutPlanService)
    {
        _workoutPlanService = workoutPlanService;
    }

    // FR-WP-02: PT sua giao an + bai tap cua minh.
    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.Pt)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateWorkoutPlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workoutPlanService.UpdateAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-WP-03: PT xoa giao an cua minh.
    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.Pt)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _workoutPlanService.DeleteAsync(id, User, cancellationToken);
        if (result.Succeeded) return NoContent();
        return ToActionResult(result);
    }
}
