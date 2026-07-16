using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Users;
[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = RoleNames.Admin)] // FR-USR-03: chi Admin quan ly tai khoan
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // FR-USR-01
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-02
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _userService.ListAsync(query, role, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-03
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    // Cap nhat status (active/locked)
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(
        long id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateStatusAsync(id, request.Status, cancellationToken);
        return ToActionResult(result);
    }

    // Reset mat khau tam cho user
    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, CancellationToken cancellationToken)
    {
        var result = await _userService.ResetPasswordAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-04 (soft-delete)
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _userService.DeleteAsync(id, cancellationToken);

        return result.Succeeded
            ? NoContent()
            : ToActionResult(result);
    }
}
