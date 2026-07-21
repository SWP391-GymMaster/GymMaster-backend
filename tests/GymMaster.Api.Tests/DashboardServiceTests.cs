using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.Api.Tests;

// White-box tests: cover range validation, empty aggregates, aggregation branches,
// time-zone-sensitive check-ins, audit filters, ordering and pagination bounds.
public sealed class DashboardServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"dashboard-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    [Fact]
    public async Task GetSummary_from_after_to_returns_invalid_range()
    {
        await using var db = NewDb();
        var service = new DashboardService(db);
        var now = DateTime.UtcNow;

        var result = await service.GetSummaryAsync(now, now.AddMinutes(-1), default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_RANGE", result.ErrorCode);
        Assert.Equal(422, result.StatusCode);
    }

    [Fact]
    public async Task GetSummary_empty_database_returns_zero_state_and_six_month_series()
    {
        await using var db = NewDb();
        var service = new DashboardService(db);

        var result = await service.GetSummaryAsync(null, null, default);

        Assert.True(result.Succeeded);
        var summary = Assert.IsType<DashboardSummaryResponse>(result.Value);
        Assert.Equal(0m, summary.Revenue);
        Assert.Equal(0, summary.ActiveCount);
        Assert.Equal(0, summary.ExpiredCount);
        Assert.Equal(0, summary.CheckinsByDay.Single().Count);
        Assert.Equal(0m, summary.PendingPaymentAmount);
        Assert.Equal(0, summary.PendingPaymentCount);
        Assert.Equal(6, summary.RevenueByMonth.Count);
        Assert.All(summary.RevenueByMonth, item => Assert.Equal(0m, item.Revenue));
        Assert.Empty(summary.RecentlyExpired);
        Assert.Equal(0, summary.FacilityLoadPercent);
        Assert.Equal(17, summary.PeakHourStart);
        Assert.Equal(19, summary.PeakHourEnd);
    }

    [Fact]
    public async Task GetSummary_aggregates_only_matching_business_states_and_date_range()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        var today = AppClock.Today();
        var user = new User { Id = 1, Email = "member@test.local", FullName = "Nguyen Van Minh" };
        var member = new MemberProfile { Id = 1, UserId = user.Id, User = user };
        var package = new MembershipPackage
        {
            Id = 1,
            Name = "Gold",
            DurationDays = 30,
            Price = 500_000m
        };
        var activeMembership = new Membership
        {
            Id = 1,
            MemberId = member.Id,
            Member = member,
            PackageId = package.Id,
            Package = package,
            StartDate = today,
            EndDate = today.AddDays(30),
            Status = MembershipStatus.Active,
            CreatedByUserId = 99
        };
        var expiredMembership = new Membership
        {
            Id = 2,
            MemberId = member.Id,
            Member = member,
            PackageId = package.Id,
            Package = package,
            StartDate = today.AddDays(-60),
            EndDate = today.AddDays(-30),
            Status = MembershipStatus.Expired,
            CreatedByUserId = 99
        };

        db.Users.Add(user);
        db.MemberProfiles.Add(member);
        db.MembershipPackages.Add(package);
        db.Memberships.AddRange(activeMembership, expiredMembership);
        db.Payments.AddRange(
            new Payment
            {
                Id = 1,
                MembershipId = activeMembership.Id,
                Amount = 300_000m,
                Status = PaymentStatus.Paid,
                PaidAt = now.AddMinutes(-30),
                PaymentMethod = PaymentMethod.Cash,
                CreatedByUserId = 99
            },
            new Payment
            {
                Id = 2,
                MembershipId = activeMembership.Id,
                Amount = 900_000m,
                Status = PaymentStatus.Paid,
                PaidAt = now.AddHours(-3),
                PaymentMethod = PaymentMethod.Cash,
                CreatedByUserId = 99
            },
            new Payment
            {
                Id = 3,
                MembershipId = activeMembership.Id,
                Amount = 50_000m,
                Status = PaymentStatus.Pending,
                PaymentMethod = PaymentMethod.Transfer,
                CreatedByUserId = 99
            });
        db.CheckIns.Add(new CheckIn
        {
            Id = 1,
            MemberId = member.Id,
            CheckInAt = now.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var service = new DashboardService(db);
        var result = await service.GetSummaryAsync(now.AddHours(-2), now, default);

        Assert.True(result.Succeeded);
        var summary = Assert.IsType<DashboardSummaryResponse>(result.Value);
        Assert.Equal(300_000m, summary.Revenue);
        Assert.Equal(1, summary.ActiveCount);
        Assert.Equal(1, summary.ExpiredCount);
        Assert.Equal(1, summary.CheckinsByDay.Single().Count);
        Assert.Equal(50_000m, summary.PendingPaymentAmount);
        Assert.Equal(1, summary.PendingPaymentCount);
        Assert.Single(summary.RecentlyExpired);
        Assert.Equal("NM", summary.RecentlyExpired[0].Initials);
        Assert.Equal("Nguyen Van Minh", summary.RecentlyExpired[0].MemberName);
        Assert.Equal(2, summary.FacilityLoadPercent);
        Assert.Equal(1, summary.NewMembershipsThisMonth);
        Assert.Equal((now.AddHours(7).Hour), summary.PeakHourStart);
    }

    [Fact]
    public async Task GetAuditLogs_applies_filters_orders_descending_and_paginates()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        db.Users.Add(new User
        {
            Id = 7,
            Email = "admin@test.local",
            FullName = "Minh Admin",
            IsDeleted = false
        });
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Id = 1,
                UserId = 7,
                Action = "CREATE_MEAL",
                Entity = "MealLog",
                EntityId = 10,
                CreatedAt = now.AddMinutes(-3)
            },
            new AuditLog
            {
                Id = 2,
                UserId = 7,
                Action = "UPDATE_MEAL",
                Entity = "MealLog",
                EntityId = 11,
                CreatedAt = now.AddMinutes(-1)
            },
            new AuditLog
            {
                Id = 3,
                UserId = null,
                Action = "LOGIN",
                Entity = "User",
                EntityId = 7,
                CreatedAt = now.AddMinutes(-2)
            });
        await db.SaveChangesAsync();

        var service = new DashboardService(db);
        var result = await service.GetAuditLogsAsync(
            userId: 7,
            action: null,
            from: now.AddMinutes(-10),
            to: now,
            search: "MEAL",
            page: 1,
            pageSize: 1,
            cancellationToken: default);

        Assert.True(result.Succeeded);
        var page = Assert.IsType<PagedResult<AuditLogResponse>>(result.Value);
        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(1, page.PageSize);
        var item = Assert.Single(page.Items);
        Assert.Equal(2, item.Id);
        Assert.Equal("Minh Admin", item.UserDisplayName);
    }

    [Fact]
    public async Task GetAuditLogs_clamps_invalid_page_and_page_size()
    {
        await using var db = NewDb();
        var service = new DashboardService(db);

        var result = await service.GetAuditLogsAsync(
            null, null, null, null, null, page: 0, pageSize: 999, default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Page);
        Assert.Equal(100, result.Value.PageSize);
        Assert.Empty(result.Value.Items);
    }
}
