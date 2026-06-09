using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IMembershipService
{
    Task<AuthServiceResult<MembershipResponse>> SellAsync(
        SellMembershipRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<MembershipResponse>> ConfirmPaymentAsync(
        long id,
        ConfirmPaymentRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<MembershipResponse>> RenewAsync(
        long id,
        RenewMembershipRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<MembershipResponse>> CreateRenewalRequestAsync(
        RenewalRequestRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<MembershipResponse>>> GetMembershipsForMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<MembershipResponse>>> GetAllAsync(
        string? status,
        int? page,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
