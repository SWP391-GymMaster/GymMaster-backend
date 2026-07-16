using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Members;
public interface IMemberService
{
    Task<ServiceResult<CreateMemberResponse>> CreateAsync(
        CreateMemberRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<PagedResult<MemberResponse>>> ListAsync(
        string? query, int page, int pageSize, CancellationToken cancellationToken);

    Task<ServiceResult<MemberResponse>> GetByIdAsync(
        long id, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<ServiceResult<long>> GetOrCreateCurrentProfileAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<ServiceResult<long>> GetCurrentMemberProfileIdAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<ServiceResult<MemberResponse>> UpdateAsync(
        long id, UpdateMemberRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<ServiceResult<object>> DeleteAsync(
        long id, CancellationToken cancellationToken);
}
