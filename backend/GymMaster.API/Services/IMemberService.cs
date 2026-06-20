using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IMemberService
{
    Task<AuthServiceResult<CreateMemberResponse>> CreateAsync(
        CreateMemberRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<PagedResult<MemberResponse>>> ListAsync(
        string? query, int page, int pageSize, CancellationToken cancellationToken);

    Task<AuthServiceResult<MemberResponse>> GetByIdAsync(
        long id, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AuthServiceResult<Member360Response>> GetProfile360Async(
        long id, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AuthServiceResult<long>> GetCurrentMemberProfileIdAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AuthServiceResult<MemberResponse>> UpdateAsync(
        long id, UpdateMemberRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AuthServiceResult<object>> DeleteAsync(
        long id, CancellationToken cancellationToken);
}
