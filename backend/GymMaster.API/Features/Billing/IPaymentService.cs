using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Billing;
public interface IPaymentService
{
    Task<ServiceResult<PagedResult<PaymentHistoryResponse>>> GetAllAsync(
        DateOnly? from,
        DateOnly? to,
        string? status,
        long? memberId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<PaymentHistoryResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<PaymentSummaryResponse>> GetSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);
}
