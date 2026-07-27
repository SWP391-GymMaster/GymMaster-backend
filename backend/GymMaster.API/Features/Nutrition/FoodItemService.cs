using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using GymMaster.API.Common;
using GymMaster.API.Features.Dashboard;

namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Nghiệp vụ kho món ăn cho API 07–08.
/// Service đọc claim để quyết định kho full/free, truy vấn FoodItem,
/// validate món tự nhập và gọi IAuditService khi tạo món mới.
/// </summary>
public sealed class FoodItemService : IFoodItemService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    // Nguoi CHUA co goi tap active (member free / khach) chi duoc xem 20 mon dau de dung thu.
    // Co goi tap active (hoac Admin/Staff/PT) => xem toan bo kho mon.
    private const int FreeFoodLimit = 20;

    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public FoodItemService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // API 07 — GET /api/v1/food-items
    // 1) Chuẩn hóa page/pageSize.
    // 2) Xác định tài khoản được xem toàn kho hay chỉ 20 món dùng thử.
    // 3) Chỉ lấy món active, lọc tên không phân biệt dấu/hoa thường.
    // 4) Sắp xếp A–Z, phân trang và map entity sang FoodItemResponse.
    public async Task<ServiceResult<PagedResult<FoodItemResponse>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        // Phan loai quyen: co goi tap active (hoac Admin/Staff/PT) => full kho;
        // member chua co goi / khach => CHI tap 20 mon co dinh (mon ngoai 20 KHONG tim thay).
        var hasFullAccess = await HasFullFoodAccessAsync(principal, cancellationToken);

        var foodItems = _dbContext.FoodItems.Where(item => item.IsActive);

        if (!hasFullAccess)
        {
            // Tap FREE co dinh = 20 mon dau (theo ten A->Z). Gioi han universe TRUOC khi loc tu khoa,
            // nen nguoi chua co goi chi xem/tim duoc trong 20 mon nay; mon thu 21+ bi khoa (can mua goi).
            var freeFoodIds = _dbContext.FoodItems
                .Where(item => item.IsActive)
                .OrderBy(item => item.Name)
                .Take(FreeFoodLimit)
                .Select(item => item.Id);

            foodItems = foodItems.Where(item => freeFoodIds.Contains(item.Id));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var keyword = query.Trim();
            // Tim khong phan biet DAU + hoa/thuong: go "com"/"thit" van ra "Cơm"/"Thịt".
            foodItems = foodItems.Where(item =>
                EF.Functions.Collate(item.Name, "Latin1_General_100_CI_AI").Contains(keyword));
        }

        foodItems = foodItems.OrderBy(item => item.Name);

        var totalItems = await foodItems.CountAsync(cancellationToken);
        var items = await foodItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var result = new PagedResult<FoodItemResponse>(
            items.Select(ToResponse).ToList(),
            page,
            pageSize,
            totalItems,
            totalPages);

        return ServiceResult<PagedResult<FoodItemResponse>>.Success(result);
    }

    // Quyền toàn kho:
    // - Admin/Staff/PT: luôn true.
    // - Member: phải tìm được MemberProfile và có Membership Active chưa hết hạn.
    private async Task<bool> HasFullFoodAccessAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!principal.IsInRole(RoleNames.Member))
        {
            return true;
        }

        var userId = GetActorId(principal);
        if (userId is null)
        {
            return false;
        }

        var memberId = await _dbContext.MemberProfiles
            .Where(mp => mp.UserId == userId.Value)
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

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(value, out var id) ? id : null;
    }

    // API 08 — POST /api/v1/food-items
    // 1) Chuẩn hóa tên/đơn vị và validate calo/macro.
    // 2) Nếu trùng tên thì trả món cũ với 200 (find-or-create).
    // 3) Nếu mới thì INSERT food_items, ghi CREATE_FOOD và trả 201.
    public async Task<ServiceResult<FoodItemResponse>> AddAsync(
        CreateFoodItemRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var name = Normalize(request.Name);
        var unit = Normalize(request.Unit);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit))
        {
            return Fail<FoodItemResponse>("VALIDATION_ERROR", "Vui long nhap ten mon va don vi.", StatusCodes.Status400BadRequest);
        }

        if (request.CaloriesPerUnit < 0 ||
            request.ProteinG < 0 ||
            request.CarbG < 0 ||
            request.FatG < 0)
        {
            return Fail<FoodItemResponse>("VALIDATION_ERROR", "Thong tin dinh duong khong hop le.", StatusCodes.Status400BadRequest);
        }

        // Find-or-Create: trung ten thi tra mon co san (200) thay vi bao loi 409.
        // Phuc vu luu mon tu tim kiem online ma khong lam crash khi trung ten.
        var existing = await _dbContext.FoodItems
            .FirstOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (existing is not null)
        {
            return ServiceResult<FoodItemResponse>.Success(ToResponse(existing), StatusCodes.Status200OK);
        }

        var foodItem = new FoodItem
        {
            Name = name,
            Unit = unit,
            CaloriesPerUnit = request.CaloriesPerUnit,
            ProteinG = request.ProteinG,
            CarbG = request.CarbG,
            FatG = request.FatG,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.FoodItems.Add(foodItem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CREATE_FOOD", "FoodItem", foodItem.Id, null, cancellationToken);

        return ServiceResult<FoodItemResponse>.Success(
            ToResponse(foodItem),
            StatusCodes.Status201Created);
    }

    // Chỉ trả DTO cần thiết cho API, không để controller trả thẳng EF entity.
    private static FoodItemResponse ToResponse(FoodItem foodItem)
    {
        return new FoodItemResponse(
            foodItem.Id,
            foodItem.Name,
            foodItem.Unit,
            foodItem.CaloriesPerUnit,
            foodItem.ProteinG,
            foodItem.CarbG,
            foodItem.FatG,
            foodItem.IsActive);
    }

    private static string Normalize(string value)
    {
        return value.Trim();
    }

    private static ServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return ServiceResult<T>.Failure(code, message, statusCode);
    }
}
