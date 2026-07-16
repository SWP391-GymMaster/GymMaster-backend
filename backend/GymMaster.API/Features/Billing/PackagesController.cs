using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Billing;
[ApiController]
[Route("api/v1/packages")]
[Authorize]
public sealed class PackagesController : ApiControllerBase
{
    private readonly IMembershipPackageService _packageService;

    public PackagesController(IMembershipPackageService packageService)
    {
        _packageService = packageService;
    }

    // FR-PKG-01
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _packageService.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    // FR-PKG-01
    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _packageService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    // FR-PKG-03
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _packageService.ListAsync(User, cancellationToken);
        return ToActionResult(result);
    }
}
