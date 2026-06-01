using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface ITrainerService
{
    Task<AuthServiceResult<TrainerResponse>> CreateAsync(
        CreateTrainerRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<PagedResult<TrainerResponse>>> ListAsync(
        int page, int pageSize, CancellationToken cancellationToken);

    Task<AuthServiceResult<TrainerResponse>> GetByIdAsync(
        long id, CancellationToken cancellationToken);

    Task<AuthServiceResult<TrainerResponse>> UpdateAsync(
        long id, UpdateTrainerRequest request, CancellationToken cancellationToken);
}
