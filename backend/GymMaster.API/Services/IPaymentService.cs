using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IPaymentService
{
    Task<AuthServiceResult<PagedResult<PaymentHistoryResponse>>> GetAllAsync(
        DateOnly? from,
        DateOnly? to,
        string? status,
        long? memberId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<PaymentHistoryResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<PaymentSummaryResponse>> GetSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);
}
