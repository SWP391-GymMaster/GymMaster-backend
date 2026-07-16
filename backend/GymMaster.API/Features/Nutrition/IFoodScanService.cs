using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Nutrition;
public interface IFoodScanService
{
    // FR-IMG-01/02: quet anh -> danh sach mon (DB match hoac nhap AI). Yeu cau goi tap active.
    Task<ServiceResult<FoodScanResponse>> ScanImageAsync(
        IFormFile? image,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // FR-IMG-03: luu mon AI sau khi user xac nhan.
    Task<ServiceResult<ScannedFood>> ConfirmAiFoodAsync(
        ConfirmAiFoodRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
