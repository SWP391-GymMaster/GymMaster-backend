using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/trainers")]
[Authorize(Roles = RoleNames.Admin)] // FR-PT-PROF-01: chi Admin quan ly ho so PT
public sealed class TrainersController : ApiControllerBase
{
    private readonly ITrainerService _trainerService;

    public TrainersController(ITrainerService trainerService)
    {
        _trainerService = trainerService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _trainerService.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _trainerService.ListAsync(query, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _trainerService.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _trainerService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }
}
