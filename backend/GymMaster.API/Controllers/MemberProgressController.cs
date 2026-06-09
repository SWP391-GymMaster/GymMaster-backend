using GymMaster.API.DTOs;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/members")]
[Authorize]
public sealed class MemberProgressController : ApiControllerBase
{
    private readonly IProgressService _progressService;

    public MemberProgressController(IProgressService progressService)
    {
        _progressService = progressService;
    }

    // FR-PROG-01
    [HttpPost("{id:long}/progress")]
    public async Task<IActionResult> Record(
        long id,
        [FromBody] RecordProgressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _progressService.RecordAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-PROG-03
    [HttpGet("{id:long}/progress")]
    public async Task<IActionResult> GetTimeline(long id, CancellationToken cancellationToken)
    {
        var result = await _progressService.GetTimelineAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-360-01
    [HttpGet("{id:long}/profile-360")]
    public async Task<IActionResult> GetProfile360(long id, CancellationToken cancellationToken)
    {
        var result = await _progressService.GetProfile360Async(id, User, cancellationToken);
        return ToActionResult(result);
    }
}
