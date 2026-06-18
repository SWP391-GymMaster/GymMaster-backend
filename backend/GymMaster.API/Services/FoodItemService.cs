using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Services;

public sealed class FoodItemService : IFoodItemService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public FoodItemService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-FOOD-01
    public async Task<AuthServiceResult<PagedResult<FoodItemResponse>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var foodItems = _dbContext.FoodItems.Where(item => item.IsActive);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var keyword = query.Trim();
            foodItems = foodItems.Where(item => item.Name.Contains(keyword));
        }

        var totalItems = await foodItems.CountAsync(cancellationToken);
        var items = await foodItems
            .OrderBy(item => item.Name)
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

        return AuthServiceResult<PagedResult<FoodItemResponse>>.Success(result);
    }

    // FR-FOOD-02
    public async Task<AuthServiceResult<FoodItemResponse>> AddAsync(
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

        if (await _dbContext.FoodItems.AnyAsync(item => item.Name == name, cancellationToken))
        {
            return Fail<FoodItemResponse>("DUPLICATE", "Mon nay da ton tai.", StatusCodes.Status409Conflict);
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

        return AuthServiceResult<FoodItemResponse>.Success(
            ToResponse(foodItem),
            StatusCodes.Status201Created);
    }

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

    private static AuthServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }
}
