using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Trainers;
public interface ITrainerService
{
    Task<ServiceResult<CreateTrainerResponse>> CreateAsync(
        CreateTrainerRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<PagedResult<TrainerResponse>>> ListAsync(
        string? query, int page, int pageSize, CancellationToken cancellationToken);

    Task<ServiceResult<TrainerResponse>> GetByIdAsync(
        long id, CancellationToken cancellationToken);

    Task<ServiceResult<TrainerResponse>> GetCurrentAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<ServiceResult<TrainerResponse>> UpdateAsync(
        long id, UpdateTrainerRequest request, CancellationToken cancellationToken);
}
