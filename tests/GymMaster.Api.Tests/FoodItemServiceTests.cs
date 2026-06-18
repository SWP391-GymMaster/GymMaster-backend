using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Test spec 007 - them mon an: Find-or-Create (trung ten thi tra mon co san, khong crash).
public class FoodItemServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Member(long userId = 10)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Member),
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    [Fact] // Them mon moi -> tao moi
    public async Task AddFood_new_name_creates()
    {
        using var db = NewDb();
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.AddAsync(
            new CreateFoodItemRequest("Chuoi", "qua", 89, 1.1m, 23m, 0.3m), Member(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Chuoi", result.Value!.Name);
        Assert.Equal(1, await db.FoodItems.CountAsync());
    }

    [Fact] // Find-or-Create: trung ten -> tra mon co san, khong loi, khong tao dong moi
    public async Task AddFood_duplicate_name_returns_existing()
    {
        using var db = NewDb();
        db.FoodItems.Add(new FoodItem { Id = 5, Name = "Tao", Unit = "qua", CaloriesPerUnit = 52, IsActive = true });
        await db.SaveChangesAsync();
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.AddAsync(
            new CreateFoodItemRequest("Tao", "qua", 52, null, null, null), Member(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.Value!.Id);               // tra mon co san
        Assert.Equal(1, await db.FoodItems.CountAsync()); // khong tao dong moi
    }
}
