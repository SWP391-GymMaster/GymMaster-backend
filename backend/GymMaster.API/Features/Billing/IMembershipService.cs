using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Billing;
public interface IMembershipService
{
    Task<ServiceResult<SellMembershipResponse>> SellAsync(
        SellMembershipRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<ConfirmPaymentResult>> ConfirmPaymentAsync(
        long id,
        ConfirmPaymentRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<MembershipResponse>> RenewAsync(
        long id,
        RenewMembershipRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<MembershipResponse>> CreateRenewalRequestAsync(
        RenewalRequestRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<MembershipResponse>>> GetMembershipsForMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<PagedResult<MembershipResponse>>> GetAllAsync(
        string? status,
        int? page,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<MembershipResponse>> CancelAsync(
        long id,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
