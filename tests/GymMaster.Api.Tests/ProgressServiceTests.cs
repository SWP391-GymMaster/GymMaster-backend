using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Test spec 006: ghi tien do (gom bodyFatPct) + tong hop Member 360.
public class ProgressServiceTests
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

    private static ProgressService NewService(GymMasterDbContext db)
        => new(db, new NoopAudit(), new NutritionService(db, new NoopAudit()));

    private static void SeedMember(GymMasterDbContext db)
    {
        db.Users.Add(new User { Id = 10, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
    }

    [Fact] // FR-PROG-01: ghi tien do luu + tra lai can nang va % mo (bodyFatPct)
    public async Task Record_progress_saves_weight_and_body_fat()
    {
        using var db = NewDb();
        SeedMember(db);
        await db.SaveChangesAsync();

        var result = await NewService(db).RecordAsync(
            1, new RecordProgressRequest(DateTime.UtcNow.AddDays(-1), 70m, 18m, null, null, null, null),
            Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(70m, result.Value!.WeightKg);
        Assert.Equal(18m, result.Value.BodyFatPct);
    }

    [Fact] // §7: ngay do o tuong lai -> INVALID_MEASUREMENT
    public async Task Record_progress_future_date_is_rejected()
    {
        using var db = NewDb();
        SeedMember(db);
        await db.SaveChangesAsync();

        var result = await NewService(db).RecordAsync(
            1, new RecordProgressRequest(DateTime.UtcNow.AddDays(5), 70m, null, null, null, null, null),
            Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_MEASUREMENT", result.ErrorCode);
    }

    [Fact] // FR-360-01: 360 tong hop ho so + membership + check-in + tien do + dinh duong
    public async Task Profile360_aggregates_membership_checkin_progress()
    {
        using var db = NewDb();
        SeedMember(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var pkg = new MembershipPackage { Id = 1, Name = "Goi 1 thang", DurationDays = 30, Price = 500_000, IsActive = true };
        db.MembershipPackages.Add(pkg);
        db.Memberships.Add(new Membership
        {
            Id = 1,
            MemberId = 1,
            PackageId = 1,
            Package = pkg,
            StartDate = today,
            EndDate = today.AddDays(30),
            Status = MembershipStatus.Active,
            CreatedByUserId = 99,
        });
        db.Payments.Add(new Payment
        {
            MembershipId = 1,
            Amount = 500_000,
            PaymentMethod = PaymentMethod.Cash,
            Status = PaymentStatus.Paid,
            PaidAt = DateTime.UtcNow,
            CreatedByUserId = 99,
        });
        db.CheckIns.Add(new CheckIn { MemberId = 1, CheckInAt = DateTime.UtcNow });
        db.ProgressLogs.Add(new ProgressLog { MemberId = 1, MeasuredAt = DateTime.UtcNow, WeightKg = 70m });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetProfile360Async(1, Staff(), default);

        Assert.True(result.Succeeded);
        var data = result.Value!;
        Assert.Equal(1, data.Member.Id);
        Assert.NotNull(data.CurrentMembership);
        Assert.Equal("Active", data.CurrentMembership!.Status);
        Assert.Equal("Paid", data.CurrentMembership.PaymentStatus);
        Assert.Single(data.RecentCheckIns);
        Assert.Single(data.ProgressTimeline);
        Assert.NotNull(data.NutritionSummary);
        Assert.Null(data.AssignedPT);
    }
}
