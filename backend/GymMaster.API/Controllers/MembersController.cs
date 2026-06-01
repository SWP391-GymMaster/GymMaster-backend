using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/members")]
[Authorize]
public sealed class MembersController : ApiControllerBase
{
    private readonly IMemberService _memberService;

    public MembersController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    // FR-MEM-01
    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _memberService.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-02
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> List(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _memberService.ListAsync(query, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-05: ownership check trong service (Member chi xem cua minh).
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _memberService.GetByIdAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-03
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _memberService.UpdateAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-04 (soft-delete)
    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _memberService.DeleteAsync(id, cancellationToken);

        return result.Succeeded
            ? NoContent()
            : ToActionResult(result);
    }
}
