using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/trainer-notes")]
[Authorize]
public sealed class TrainerNotesController : ApiControllerBase
{
    private readonly ITrainerNoteService _trainerNoteService;

    public TrainerNotesController(ITrainerNoteService trainerNoteService)
    {
        _trainerNoteService = trainerNoteService;
    }

    // FR-NOTE-02: PT sua ghi chu cua minh.
    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.Pt)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateTrainerNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _trainerNoteService.UpdateAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-NOTE-03: PT xoa ghi chu cua minh.
    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.Pt)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _trainerNoteService.DeleteAsync(id, User, cancellationToken);
        if (result.Succeeded) return NoContent();
        return ToActionResult(result);
    }
}
