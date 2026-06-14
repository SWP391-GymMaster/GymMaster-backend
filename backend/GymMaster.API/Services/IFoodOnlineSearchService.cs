using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IFoodOnlineSearchService
{
    // Tim mon tu Open Food Facts. Loi/timeout/tu khoa qua ngan -> tra rong (FE tu lui ve kho noi bo).
    Task<IReadOnlyList<OnlineFoodItemResponse>> SearchAsync(string? query, CancellationToken cancellationToken);
}
