using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Hợp đồng nghiệp vụ kho món ăn.
/// FoodItemsController phụ thuộc interface này thay vì phụ thuộc trực tiếp FoodItemService.
/// </summary>
public interface IFoodItemService
{
    // API 07: tìm món active, áp quyền full/free và trả kết quả phân trang.
    Task<ServiceResult<PagedResult<FoodItemResponse>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // API 08: validate và tạo món thủ công hoặc trả món có sẵn khi trùng tên.
    Task<ServiceResult<FoodItemResponse>> AddAsync(
        CreateFoodItemRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
