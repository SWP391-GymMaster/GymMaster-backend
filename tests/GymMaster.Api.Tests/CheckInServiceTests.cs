using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Options;
using GymMaster.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace GymMaster.Api.Tests;

// FR-CHK-03: gioi han so lan check-in trong ngay (mac dinh 2 lan/ngay).
public class CheckInServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Staff(long userId = 99)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Staff),
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static CheckInService NewService(GymMasterDbContext db, int maxPerDay)
        => new(db, new NoopAudit(), Options.Create(new CheckInOptions
        {
            EnforceMembership = false,
            OncePerDay = false,
            MaxPerDay = maxPerDay,
        }));

    private static void SeedMember(GymMasterDbContext db)
    {
        db.Users.Add(new User { Id = 10, FullName = "Member A", Status = UserStatuses.Active, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
    }

    private static Task<AuthServiceResult<CheckInResponse>> CheckIn(CheckInService service)
        => service.CreateAsync(new CreateCheckInRequest { MemberId = 1 }, Staff(), default);

    [Fact] // Mac dinh 2 lan/ngay: lan 3 bi chan DAILY_LIMIT_REACHED
    public async Task CheckIn_blocks_third_time_in_same_day()
    {
        using var db = NewDb();
        SeedMember(db);
        await db.SaveChangesAsync();
        var service = NewService(db, maxPerDay: 2);

        var first = await CheckIn(service);
        var second = await CheckIn(service);
        var third = await CheckIn(service);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.False(third.Succeeded);
        Assert.Equal("DAILY_LIMIT_REACHED", third.ErrorCode);
        Assert.Equal(2, await db.CheckIns.CountAsync(c => c.MemberId == 1));
    }

    [Fact] // MaxPerDay <= 0 => khong gioi han
    public async Task CheckIn_unlimited_when_max_per_day_is_zero()
    {
        using var db = NewDb();
        SeedMember(db);
        await db.SaveChangesAsync();
        var service = NewService(db, maxPerDay: 0);

        await CheckIn(service);
        await CheckIn(service);
        var third = await CheckIn(service);

        Assert.True(third.Succeeded);
        Assert.Equal(3, await db.CheckIns.CountAsync(c => c.MemberId == 1));
    }
}
