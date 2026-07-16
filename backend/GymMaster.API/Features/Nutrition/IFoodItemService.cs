using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Nutrition;
public interface IFoodItemService
{
    Task<ServiceResult<PagedResult<FoodItemResponse>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<FoodItemResponse>> AddAsync(
        CreateFoodItemRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
