using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Billing;
[ApiController]
[Route("api/v1/members")]
[Authorize]
public sealed class MemberMembershipsController : ApiControllerBase
{
    private readonly IMembershipService _membershipService;

    public MemberMembershipsController(IMembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    // FR-MS-09
    [HttpGet("{memberId:long}/memberships")]
    public async Task<IActionResult> GetMemberships(long memberId, CancellationToken cancellationToken)
    {
        var result = await _membershipService.GetMembershipsForMemberAsync(memberId, User, cancellationToken);
        return ToActionResult(result);
    }
}
