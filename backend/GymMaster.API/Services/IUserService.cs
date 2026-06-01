using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IUserService
{
    Task<AuthServiceResult<CreateUserResponse>> CreateAsync(
        CreateUserRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<PagedResult<AdminUserResponse>>> ListAsync(
        string? query, string? role, int page, int pageSize, CancellationToken cancellationToken);

    Task<AuthServiceResult<AdminUserResponse>> GetByIdAsync(
        long id, CancellationToken cancellationToken);

    Task<AuthServiceResult<AdminUserResponse>> UpdateAsync(
        long id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<object>> DeleteAsync(
        long id, CancellationToken cancellationToken);
}
