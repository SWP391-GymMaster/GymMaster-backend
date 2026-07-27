using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using GymMaster.API.Common;
using GymMaster.API.Features.Dashboard;

namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Nghiệp vụ trung tâm cho API 01–06: mục tiêu calo, nhật ký bữa ăn,
/// tổng kết một ngày và lịch sử nhiều ngày.
/// Service kiểm tra hội viên/quyền truy cập, làm việc trực tiếp với GymMasterDbContext,
/// tính calo/macro và ghi Audit Log cho các thao tác thay đổi dữ liệu.
/// </summary>
public sealed class NutritionService : INutritionService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public NutritionService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // API 01 — POST /api/v1/members/{id}/calorie-target
    // Kiểm tra hội viên + quyền + dữ liệu, sau đó UPSERT theo (MemberId, EffectiveDate).
    // Tạo mới trả 201, cập nhật trả 200; cả hai đều ghi SET_CALORIE_TARGET.
    public async Task<ServiceResult<CalorieTargetResponse>> SetTargetAsync(
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

        if (!await CanAccessAsync(principal, profile, cancellationToken))
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

        return ServiceResult<CalorieTargetResponse>.Success(ToResponse(target), statusCode);
    }

    // API 02 — GET /api/v1/members/{id}/calorie-target
    // Lấy mục tiêu có EffectiveDate <= hôm nay và mới nhất; không có trả 404 NO_TARGET.
    public async Task<ServiceResult<CalorieTargetResponse>> GetTargetAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(memberId, cancellationToken);

        if (profile is null)
        {
            return Fail<CalorieTargetResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!await CanAccessAsync(principal, profile, cancellationToken))
        {
            return Fail<CalorieTargetResponse>("FORBIDDEN", "Ban khong co quyen xem muc tieu calo.", StatusCodes.Status403Forbidden);
        }

        var target = await _dbContext.CalorieTargets
            .Where(t => t.MemberId == memberId && t.EffectiveDate <= Today())
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return Fail<CalorieTargetResponse>("NO_TARGET", "Hoi vien chua dat muc tieu calo.", StatusCodes.Status404NotFound);
        }

        return ServiceResult<CalorieTargetResponse>.Success(ToResponse(target));
    }

    // API 05 — POST /api/v1/meal-logs
    // Gộp FoodItemId trùng trong request; xác minh tất cả món active;
    // tạo MealLog mới hoặc cộng dồn vào bữa cùng MemberId/LogDate/MealType;
    // tính CaloriesPerUnit × Quantity và ghi CREATE_MEAL_LOG.
    public async Task<ServiceResult<MealLogResponse>> CreateMealLogAsync(
        CreateMealLogRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(request.MemberId, cancellationToken);

        if (profile is null)
        {
            return Fail<MealLogResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!await CanAccessAsync(principal, profile, cancellationToken))
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

        return ServiceResult<MealLogResponse>.Success(
            ToResponse(mealLog),
            StatusCodes.Status201Created);
    }

    // API 06 — GET /api/v1/meal-logs
    // Lấy MealLog kèm MealLogItem + FoodItem theo hội viên/ngày,
    // sắp xếp bữa theo MealType và map thành danh sách MealLogResponse.
    public async Task<ServiceResult<IReadOnlyList<MealLogResponse>>> GetMealLogsAsync(
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

        if (!await CanAccessAsync(principal, profile, cancellationToken))
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

        return ServiceResult<IReadOnlyList<MealLogResponse>>.Success(
            mealLogs.Select(ToResponse).ToList());
    }

    // API 03 — GET /api/v1/members/{id}/calorie-summary
    // Cộng calo/macro của mọi món trong ngày, lấy mục tiêu hiệu lực tại ngày đó
    // rồi tính Remaining = Target - Consumed. Không có mục tiêu thì Target/Remaining null.
    public async Task<ServiceResult<CalorieSummaryResponse>> GetSummaryAsync(
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

        if (!await CanAccessAsync(principal, profile, cancellationToken))
        {
            return Fail<CalorieSummaryResponse>("FORBIDDEN", "Ban khong co quyen xem tong ket calo.", StatusCodes.Status403Forbidden);
        }

        var logDate = date ?? Today();
        var items = await _dbContext.MealLogs
            .Where(item => item.MemberId == memberId && item.LogDate == logDate)
            .SelectMany(item => item.Items)
            .Select(item => new
            {
                item.Calories,
                item.Quantity,
                P = item.FoodItem.ProteinG,
                C = item.FoodItem.CarbG,
                F = item.FoodItem.FatG
            })
            .ToListAsync(cancellationToken);

        var consumed = items.Sum(item => item.Calories);
        var consumedProtein = items.Sum(item => (item.P ?? 0) * item.Quantity);
        var consumedCarb = items.Sum(item => (item.C ?? 0) * item.Quantity);
        var consumedFat = items.Sum(item => (item.F ?? 0) * item.Quantity);

        var target = await GetTargetForDateAsync(memberId, logDate, cancellationToken);

        return ServiceResult<CalorieSummaryResponse>.Success(
            new CalorieSummaryResponse(
                logDate,
                consumed,
                target?.DailyCalories,
                target is null ? null : target.DailyCalories - consumed,
                consumedProtein,
                consumedCarb,
                consumedFat,
                target?.ProteinG,
                target?.CarbG,
                target?.FatG,
                target?.ProteinG is null ? null : target.ProteinG - consumedProtein,
                target?.CarbG is null ? null : target.CarbG - consumedCarb,
                target?.FatG is null ? null : target.FatG - consumedFat));
    }

    // API 04 — GET /api/v1/members/{id}/calorie-history
    // Mặc định sinh đủ 7 ngày; ngày không có bữa vẫn trả Consumed=0.
    // Mỗi ngày dùng mục tiêu mới nhất đã có hiệu lực tại chính ngày đó.
    public async Task<ServiceResult<IReadOnlyList<CalorieSummaryResponse>>> GetHistoryAsync(
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

        if (!await CanAccessAsync(principal, profile, cancellationToken))
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

        return ServiceResult<IReadOnlyList<CalorieSummaryResponse>>.Success(results);
    }

    // Tìm mục tiêu mới nhất có hiệu lực tại một ngày cụ thể; dùng cho summary.
    private async Task<CalorieTarget?> GetTargetForDateAsync(
        long memberId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CalorieTargets
            .Where(item => item.MemberId == memberId && item.EffectiveDate <= date)
            .OrderByDescending(item => item.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Chỉ coi hội viên tồn tại khi cả MemberProfile và User chưa bị soft-delete.
    private Task<MemberProfile?> FindMemberAsync(long memberId, CancellationToken cancellationToken)
    {
        return _dbContext.MemberProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == memberId && !item.IsDeleted && !item.User.IsDeleted, cancellationToken);
    }

    // Luật truy cập dùng chung cho cả 6 API:
    // Admin/Staff xem mọi hội viên; Member chỉ chính mình;
    // PT chỉ hội viên có TrainerAssignment active với PT đó.
    private async Task<bool> CanAccessAsync(
        ClaimsPrincipal principal,
        MemberProfile profile,
        CancellationToken cancellationToken)
    {
        if (principal.IsInRole(RoleNames.Admin) || principal.IsInRole(RoleNames.Staff))
        {
            return true;
        }

        if (principal.IsInRole(RoleNames.Member))
        {
            return GetActorId(principal) == profile.UserId;
        }

        // PT chi truy cap duoc hoi vien dang duoc phan cong active cho minh (giong WorkoutPlanService).
        if (principal.IsInRole(RoleNames.Pt))
        {
            var actorId = GetActorId(principal);
            if (actorId is null)
            {
                return false;
            }

            var trainerProfileId = await _dbContext.TrainerProfiles
                .Where(item => item.UserId == actorId && !item.IsDeleted)
                .Select(item => (long?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (trainerProfileId is null)
            {
                return false;
            }

            return await _dbContext.TrainerAssignments.AnyAsync(
                item => item.TrainerId == trainerProfileId
                    && item.MemberId == profile.Id
                    && item.Status == AssignmentStatuses.Active,
                cancellationToken);
        }

        return false;
    }

    // Map entity mục tiêu sang DTO; không trả trực tiếp entity EF Core.
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

    // Map một bữa sang DTO, nhân macro theo Quantity và cộng TotalCalories.
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
        return AppClock.Today();
    }

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static ServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return ServiceResult<T>.Failure(code, message, statusCode);
    }
}
