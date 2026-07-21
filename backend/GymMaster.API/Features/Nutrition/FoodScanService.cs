using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using GymMaster.API.Common;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Infrastructure;

namespace GymMaster.API.Features.Nutrition;
/// <summary>
/// Tinh nang Quet anh mon an bang AI (Gemini). Tach rieng khoi FoodItemService de KHONG dung
/// cham code mon an co san. Tu kiem tra "co goi tap active" bang DbContext.
/// </summary>
public sealed class FoodScanService : IFoodScanService
{
    private const string SearchCollation = "Latin1_General_100_CI_AI";
    private const string UpgradeMessage =
        "Tinh nang quet anh chi danh cho hoi vien co goi tap. Vui long dang ky goi tap de su dung.";

    private readonly GymMasterDbContext _dbContext;
    private readonly IFoodImageAnalyzer _imageAnalyzer;
    private readonly IAuditService _auditService;
    private readonly GeminiOptions _options;

    public FoodScanService(
        GymMasterDbContext dbContext,
        IFoodImageAnalyzer imageAnalyzer,
        IAuditService auditService,
        IOptions<GeminiOptions> options)
    {
        _dbContext = dbContext;
        _imageAnalyzer = imageAnalyzer;
        _auditService = auditService;
        _options = options.Value;
    }

    public async Task<ServiceResult<FoodScanResponse>> ScanImageAsync(
        IFormFile? image,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);
        if (actorId is null || !principal.IsInRole(RoleNames.Member) ||
            !await HasActivePackageAsync(actorId.Value, cancellationToken))
        {
            return ServiceResult<FoodScanResponse>.Failure(
                "MEMBERSHIP_REQUIRED", UpgradeMessage, StatusCodes.Status403Forbidden);
        }

        if (image is null ||
            image.Length == 0 ||
            image.Length > _options.MaxImageBytes ||
            image.ContentType is not ("image/jpeg" or "image/png"))
        {
            return ServiceResult<FoodScanResponse>.Failure(
                "INVALID_FILE", "Anh phai la JPG/PNG va dung luong khong qua 5MB.",
                StatusCodes.Status422UnprocessableEntity);
        }

        await using var stream = image.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var imageBytes = buffer.ToArray();

        var detection = await _imageAnalyzer.DetectFoodsAsync(imageBytes, image.ContentType, cancellationToken);
        if (!detection.Succeeded)
        {
            return ServiceResult<FoodScanResponse>.Failure(
                detection.ErrorCode!, detection.ErrorMessage!, StatusCodes.Status502BadGateway);
        }

        var items = new List<FoodScanItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var detected in detection.Value!)
        {
            var name = detected.FoodName.Trim();
            if (name.Length == 0 || !seen.Add(name))
            {
                continue;
            }

            var dbFood = await FindDatabaseMatchAsync(name, cancellationToken);
            if (dbFood is not null)
            {
                items.Add(new FoodScanItem(name, detected.Confidence, "Database", false, ToScannedFood(dbFood), null, detected.EstimatedGrams));
            }
            else
            {
                items.Add(new FoodScanItem(
                    name, detected.Confidence, "AI", true, null,
                    new FoodNutritionDraft(name, "g", 100, detected.Calories,
                        detected.ProteinG, detected.CarbsG, detected.FatG, "AI"),
                    detected.EstimatedGrams));
            }
        }

        return ServiceResult<FoodScanResponse>.Success(new FoodScanResponse(items));
    }

    public async Task<ServiceResult<FoodNutritionDraft>> EstimateNutritionAsync(
        EstimateFoodNutritionRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);
        if (actorId is null || !principal.IsInRole(RoleNames.Member) ||
            !await HasActivePackageAsync(actorId.Value, cancellationToken))
        {
            return ServiceResult<FoodNutritionDraft>.Failure(
                "MEMBERSHIP_REQUIRED", UpgradeMessage, StatusCodes.Status403Forbidden);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 150)
        {
            return ServiceResult<FoodNutritionDraft>.Failure(
                "VALIDATION_ERROR", "Ten thuc pham phai co tu 2 den 150 ky tu.",
                StatusCodes.Status400BadRequest);
        }

        var estimate = await _imageAnalyzer.EstimateNutritionAsync(name, cancellationToken);
        if (!estimate.Succeeded)
        {
            return ServiceResult<FoodNutritionDraft>.Failure(
                estimate.ErrorCode!, estimate.ErrorMessage!, StatusCodes.Status502BadGateway);
        }

        var value = estimate.Value!;
        return ServiceResult<FoodNutritionDraft>.Success(new FoodNutritionDraft(
            value.FoodName,
            "100g",
            100,
            value.Calories,
            value.ProteinG,
            value.CarbsG,
            value.FatG,
            "AI"));
    }

    public async Task<ServiceResult<ScannedFood>> ConfirmAiFoodAsync(
        ConfirmAiFoodRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);
        if (actorId is null || !principal.IsInRole(RoleNames.Member) ||
            !await HasActivePackageAsync(actorId.Value, cancellationToken))
        {
            return ServiceResult<ScannedFood>.Failure(
                "MEMBERSHIP_REQUIRED", UpgradeMessage, StatusCodes.Status403Forbidden);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return ServiceResult<ScannedFood>.Failure(
                "VALIDATION_ERROR", "Vui long nhap ten mon hop le.", StatusCodes.Status400BadRequest);
        }

        if (request.CaloriesPerUnit < 0 || request.ProteinG < 0 || request.CarbsG < 0 || request.FatG < 0)
        {
            return ServiceResult<ScannedFood>.Failure(
                "VALIDATION_ERROR", "Thong tin dinh duong khong hop le.", StatusCodes.Status400BadRequest);
        }

        var existing = await _dbContext.FoodItems.FirstOrDefaultAsync(
            item => EF.Functions.Collate(item.Name, SearchCollation) == name, cancellationToken);
        if (existing is not null)
        {
            return ServiceResult<ScannedFood>.Success(ToScannedFood(existing));
        }

        var foodItem = new FoodItem
        {
            Name = name,
            Unit = "g",
            ServingSize = 100,
            CaloriesPerUnit = request.CaloriesPerUnit,
            ProteinG = request.ProteinG,
            CarbG = request.CarbsG,
            FatG = request.FatG,
            Source = "AI",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.FoodItems.Add(foodItem);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync("CONFIRM_AI_FOOD", "FoodItem", foodItem.Id, new { foodItem.Source }, cancellationToken);

        return ServiceResult<ScannedFood>.Success(ToScannedFood(foodItem), StatusCodes.Status201Created);
    }

    private async Task<bool> HasActivePackageAsync(long userId, CancellationToken cancellationToken)
    {
        var memberId = await _dbContext.MemberProfiles
            .Where(mp => mp.UserId == userId)
            .Select(mp => (long?)mp.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (memberId is null)
        {
            return false;
        }

        var today = AppClock.Today();
        return await _dbContext.Memberships.AnyAsync(
            m => m.MemberId == memberId.Value && m.Status == MembershipStatus.Active && m.EndDate >= today,
            cancellationToken);
    }

    private async Task<FoodItem?> FindDatabaseMatchAsync(string name, CancellationToken cancellationToken)
    {
        var exact = await _dbContext.FoodItems.AsNoTracking().FirstOrDefaultAsync(
            item => item.IsActive && EF.Functions.Collate(item.Name, SearchCollation) == name, cancellationToken);
        if (exact is not null)
        {
            return exact;
        }

        return await _dbContext.FoodItems.AsNoTracking()
            .Where(item => item.IsActive && EF.Functions.Collate(item.Name, SearchCollation).Contains(name))
            .OrderBy(item => item.Name.Length)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ScannedFood ToScannedFood(FoodItem f)
        => new(f.Id, f.Name, f.Unit, f.ServingSize, f.CaloriesPerUnit, f.ProteinG, f.CarbG, f.FatG, f.Source);

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(value, out var id) ? id : null;
    }
}
