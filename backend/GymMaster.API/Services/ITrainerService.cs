using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface ITrainerService
{
    Task<AuthServiceResult<TrainerResponse>> CreateAsync(
        CreateTrainerRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<PagedResult<TrainerResponse>>> ListAsync(
        string? query, int page, int pageSize, CancellationToken cancellationToken);

    Task<AuthServiceResult<TrainerResponse>> GetByIdAsync(
        long id, CancellationToken cancellationToken);

    Task<AuthServiceResult<TrainerResponse>> GetCurrentAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AuthServiceResult<TrainerResponse>> UpdateAsync(
        long id, UpdateTrainerRequest request, CancellationToken cancellationToken);
}
