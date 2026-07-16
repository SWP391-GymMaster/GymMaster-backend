using GymMaster.API.Common;
namespace GymMaster.API.Features.Users;
public interface IUserService
{
    Task<ServiceResult<CreateUserResponse>> CreateAsync(
        CreateUserRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<PagedResult<AdminUserResponse>>> ListAsync(
        string? query, string? role, int page, int pageSize, CancellationToken cancellationToken);

    Task<ServiceResult<AdminUserResponse>> GetByIdAsync(
        long id, CancellationToken cancellationToken);

    Task<ServiceResult<AdminUserResponse>> UpdateAsync(
        long id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AdminUserResponse>> UpdateStatusAsync(
        long id, string status, CancellationToken cancellationToken);

    Task<ServiceResult<ResetUserPasswordResponse>> ResetPasswordAsync(
        long id, CancellationToken cancellationToken);

    Task<ServiceResult<object>> DeleteAsync(
        long id, CancellationToken cancellationToken);
}
