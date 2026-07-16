using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Trainers;
[ApiController]
[Route("api/v1/trainers")]
[Authorize] // Dang nhap bat buoc; tung action gioi han role rieng.
public sealed class TrainersController : ApiControllerBase
{
    private readonly ITrainerService _trainerService;

    public TrainersController(ITrainerService trainerService)
    {
        _trainerService = trainerService;
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)] // FR-PT-PROF-01: chi Admin quan ly ho so PT
    public async Task<IActionResult> Create(
        [FromBody] CreateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _trainerService.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> List(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _trainerService.ListAsync(query, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = RoleNames.Pt)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await _trainerService.GetCurrentAsync(User, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _trainerService.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _trainerService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }
}
