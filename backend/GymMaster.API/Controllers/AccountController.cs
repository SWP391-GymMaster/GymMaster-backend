using GymMaster.API.DTOs;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/users/me")]
[Authorize]
public sealed class AccountController : ApiControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateMyAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.UpdateAsync(User, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.UploadAvatarAsync(User, file, cancellationToken);
        return ToActionResult(result);
    }
}
