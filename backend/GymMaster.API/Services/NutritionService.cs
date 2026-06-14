using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Services;

public sealed class NutritionService : INutritionService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public NutritionService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-CAL-TGT-01
    public async Task<AuthServiceResult<CalorieTargetResponse>> SetTargetAsync(
        long memberId,
        SetCalorieTargetRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(memberId, cancellationToken);

        if (profile is null)
        {
            return Fail<CalorieTargetResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<CalorieTargetResponse>("FORBIDDEN", "Ban khong co quyen dat muc tieu calo.", StatusCodes.Status403Forbidden);
        }

        if (request.DailyCalories <= 0 ||
            request.ProteinG < 0 ||
            request.CarbG < 0 ||
            request.FatG < 0)
        {
            return Fail<CalorieTargetResponse>("INVALID_TARGET", "Muc tieu calo khong hop le.", StatusCodes.Status422UnprocessableEntity);
        }

        var effectiveDate = request.EffectiveDate ?? Today();
        var target = await _dbContext.CalorieTargets.FirstOrDefaultAsync(
            item => item.MemberId == memberId && item.EffectiveDate == effectiveDate,
            cancellationToken);

        var statusCode = StatusCodes.Status200OK;
        if (target is null)
        {
            target = new CalorieTarget
            {
                MemberId = memberId,
                EffectiveDate = effectiveDate,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.CalorieTargets.Add(target);
            statusCode = StatusCodes.Status201Created;
        }

        target.DailyCalories = request.DailyCalories;
        target.ProteinG = request.ProteinG;
        target.CarbG = request.CarbG;
        target.FatG = request.FatG;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "SET_CALORIE_TARGET",
            "CalorieTarget",
            target.Id,
            new { memberId, effectiveDate },
            cancellationToken);

        return AuthServiceResult<CalorieTargetResponse>.Success(ToResponse(target), statusCode);
    }

    // FR-MEAL-01 / FR-MEAL-02 / FR-MEAL-03
    public async Task<AuthServiceResult<MealLogResponse>> CreateMealLogAsync(
        CreateMealLogRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(request.MemberId, cancellationToken);

        if (profile is null)
        {
            return Fail<MealLogResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<MealLogResponse>("FORBIDDEN", "Ban khong co quyen ghi bua an nay.", StatusCodes.Status403Forbidden);
        }

        if (request.Items is null ||
            request.Items.Count == 0 ||
            request.Items.Any(item => item.Quantity <= 0))
        {
            return Fail<MealLogResponse>("INVALID_QUANTITY", "Khau phan khong hop le.", StatusCodes.Status422UnprocessableEntity);
        }

        if (!Enum.IsDefined(typeof(MealType), request.MealType))
        {
            return Fail<MealLogResponse>("VALIDATION_ERROR", "Loai bua an khong hop le.", StatusCodes.Status422UnprocessableEntity);
        }

        var groupedItems = request.Items
            .GroupBy(item => item.FoodItemId)
            .ToDictionary(item => item.Key, item => item.Sum(value => value.Quantity));

        var foodIds = groupedItems.Keys.ToList();
        var foods = await _dbContext.FoodItems
            .Where(item => foodIds.Contains(item.Id) && item.IsActive)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        if (foods.Count != foodIds.Count)
        {
            return Fail<MealLogResponse>("FOOD_NOT_FOUND", "Khong tim thay mon an.", StatusCodes.Status404NotFound);
        }

        var mealType = (MealType)request.MealType;
        var mealLog = await _dbContext.MealLogs
            .Include(item => item.Items)
            .ThenInclude(item => item.FoodItem)
            .FirstOrDefaultAsync(
                item => item.MemberId == request.MemberId &&
                    item.LogDate == request.LogDate &&
                    item.MealType == mealType,
                cancellationToken);

        if (mealLog is null)
        {
            mealLog = new MealLog
            {
                MemberId = request.MemberId,
                LogDate = request.LogDate,
                MealType = mealType,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.MealLogs.Add(mealLog);
        }

        foreach (var (foodId, quantity) in groupedItems)
        {
            var food = foods[foodId];
            var calories = food.CaloriesPerUnit * quantity;
            var existingItem = mealLog.Items.FirstOrDefault(item => item.FoodItemId == foodId);

            if (existingItem is null)
            {
                mealLog.Items.Add(new MealLogItem
                {
                    FoodItemId = foodId,
                    FoodItem = food,
                    Quantity = quantity,
                    Calories = calories
                });
            }
            else
            {
                existingItem.Quantity += quantity;
                existingItem.Calories += calories;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "CREATE_MEAL_LOG",
            "MealLog",
            mealLog.Id,
            new { memberId = request.MemberId, request.LogDate },
            cancellationToken);

        return AuthServiceResult<MealLogResponse>.Success(
            ToResponse(mealLog),
            StatusCodes.Status201Created);
    }

    // 🆕 API_CONTRACT_Y: doc nhat ky bua an theo member + ngay.
    public async Task<AuthServiceResult<IReadOnlyList<MealLogResponse>>> GetMealLogsAsync(
        long memberId,
        DateOnly? date,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(memberId, cancellationToken);

        if (profile is null)
        {
            return Fail<IReadOnlyList<MealLogResponse>>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<IReadOnlyList<MealLogResponse>>("FORBIDDEN", "Ban khong co quyen xem nhat ky bua an.", StatusCodes.Status403Forbidden);
        }

        var logDate = date ?? Today();
        var mealLogs = await _dbContext.MealLogs
            .Include(item => item.Items)
            .ThenInclude(item => item.FoodItem)
            .Where(item => item.MemberId == memberId && item.LogDate == logDate)
            .OrderBy(item => item.MealType)
            .ToListAsync(cancellationToken);

        return AuthServiceResult<IReadOnlyList<MealLogResponse>>.Success(
            mealLogs.Select(ToResponse).ToList());
    }

    // FR-CAL-01
    public async Task<AuthServiceResult<CalorieSummaryResponse>> GetSummaryAsync(
        long memberId,
        DateOnly? date,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(memberId, cancellationToken);

        if (profile is null)
        {
            return Fail<CalorieSummaryResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<CalorieSummaryResponse>("FORBIDDEN", "Ban khong co quyen xem tong ket calo.", StatusCodes.Status403Forbidden);
        }

        var logDate = date ?? Today();
        var consumed = await _dbContext.MealLogs
            .Where(item => item.MemberId == memberId && item.LogDate == logDate)
            .SelectMany(item => item.Items)
            .Select(item => (decimal?)item.Calories)
            .SumAsync(cancellationToken) ?? 0;

        var target = await GetTargetForDateAsync(memberId, logDate, cancellationToken);

        return AuthServiceResult<CalorieSummaryResponse>.Success(
            new CalorieSummaryResponse(
                logDate,
                consumed,
                target,
                target is null ? null : target - consumed));
    }

    // FR-CAL-01
    public async Task<AuthServiceResult<IReadOnlyList<CalorieSummaryResponse>>> GetHistoryAsync(
        long memberId,
        DateOnly? from,
        DateOnly? to,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(memberId, cancellationToken);

        if (profile is null)
        {
            return Fail<IReadOnlyList<CalorieSummaryResponse>>(
                "NOT_FOUND",
                "Khong tim thay hoi vien.",
                StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<IReadOnlyList<CalorieSummaryResponse>>(
                "FORBIDDEN",
                "Ban khong co quyen xem lich su calo.",
                StatusCodes.Status403Forbidden);
        }

        var endDate = to ?? Today();
        var startDate = from ?? endDate.AddDays(-6);

        if (startDate > endDate)
        {
            return Fail<IReadOnlyList<CalorieSummaryResponse>>(
                "VALIDATION_ERROR",
                "Khoang ngay khong hop le.",
                StatusCodes.Status422UnprocessableEntity);
        }

        var mealLogs = await _dbContext.MealLogs
            .Include(item => item.Items)
            .Where(item => item.MemberId == memberId && item.LogDate >= startDate && item.LogDate <= endDate)
            .ToListAsync(cancellationToken);

        var consumedByDate = mealLogs
            .GroupBy(item => item.LogDate)
            .ToDictionary(
                item => item.Key,
                item => item.SelectMany(log => log.Items).Sum(logItem => logItem.Calories));

        var targets = await _dbContext.CalorieTargets
            .Where(item => item.MemberId == memberId && item.EffectiveDate <= endDate)
            .OrderBy(item => item.EffectiveDate)
            .ToListAsync(cancellationToken);

        var results = new List<CalorieSummaryResponse>();
        for (var current = startDate; current <= endDate; current = current.AddDays(1))
        {
            consumedByDate.TryGetValue(current, out var consumed);
            var target = targets
                .Where(item => item.EffectiveDate <= current)
                .OrderByDescending(item => item.EffectiveDate)
                .FirstOrDefault()
                ?.DailyCalories;

            results.Add(new CalorieSummaryResponse(
                current,
                consumed,
                target,
                target is null ? null : target - consumed));
        }

        return AuthServiceResult<IReadOnlyList<CalorieSummaryResponse>>.Success(results);
    }

    private async Task<decimal?> GetTargetForDateAsync(
        long memberId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CalorieTargets
            .Where(item => item.MemberId == memberId && item.EffectiveDate <= date)
            .OrderByDescending(item => item.EffectiveDate)
            .Select(item => (decimal?)item.DailyCalories)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<MemberProfile?> FindMemberAsync(long memberId, CancellationToken cancellationToken)
    {
        return _dbContext.MemberProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == memberId && !item.IsDeleted && !item.User.IsDeleted, cancellationToken);
    }

    private static bool CanAccess(ClaimsPrincipal principal, MemberProfile profile)
    {
        if (principal.IsInRole(RoleNames.Admin) || principal.IsInRole(RoleNames.Staff))
        {
            return true;
        }

        if (principal.IsInRole(RoleNames.Member))
        {
            return GetActorId(principal) == profile.UserId;
        }

        return false;
    }

    private static CalorieTargetResponse ToResponse(CalorieTarget target)
    {
        return new CalorieTargetResponse(
            target.Id,
            target.MemberId,
            target.EffectiveDate,
            target.DailyCalories,
            target.ProteinG,
            target.CarbG,
            target.FatG);
    }

    private static MealLogResponse ToResponse(MealLog mealLog)
    {
        var items = mealLog.Items
            .OrderBy(item => item.Id)
            .Select(item => new MealItemResponse(
                item.Id,
                item.FoodItemId,
                item.FoodItem.Name,
                item.Quantity,
                item.Calories,
                item.FoodItem.ProteinG * item.Quantity,
                item.FoodItem.CarbG * item.Quantity,
                item.FoodItem.FatG * item.Quantity))
            .ToList();

        return new MealLogResponse(
            mealLog.Id,
            mealLog.MemberId,
            mealLog.LogDate,
            mealLog.MealType.ToString(),
            items,
            items.Sum(item => item.Calories));
    }

    private static DateOnly Today()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static AuthServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }
}
