using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Hợp đồng ba nghiệp vụ AI của FoodScanController.
/// Việc gọi Gemini được che phía sau interface IFoodImageAnalyzer trong implementation.
/// </summary>
public interface IFoodScanService
{
    // API 09: quét ảnh -> danh sách món đã match DB hoặc bản nháp AI cần xác nhận.
    Task<ServiceResult<FoodScanResponse>> ScanImageAsync(
        IFormFile? image,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // API 10: ước lượng dinh dưỡng từ tên; chỉ trả draft, không tự lưu DB.
    Task<ServiceResult<FoodNutritionDraft>> EstimateNutritionAsync(
        EstimateFoodNutritionRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // API 11: lưu món AI sau khi Member xác nhận hoặc trả món cũ nếu đã tồn tại.
    Task<ServiceResult<ScannedFood>> ConfirmAiFoodAsync(
        ConfirmAiFoodRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
