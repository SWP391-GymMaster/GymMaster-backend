using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IFoodItemService
{
    Task<AuthServiceResult<PagedResult<FoodItemResponse>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<FoodItemResponse>> AddAsync(
        CreateFoodItemRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
