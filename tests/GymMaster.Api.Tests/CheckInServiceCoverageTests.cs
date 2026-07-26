using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.CheckIns;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace GymMaster.Api.Tests;

// Bo sung coverage CheckInService: member tu check-in, PT check-in ho, list, ownership, membership, khoa TK.
public class CheckInServiceCoverageTests
{
    private static GymMasterDbContext NewDb()
        => new(new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-chkcov-{Guid.NewGuid()}").Options);

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static CheckInService NewService(GymMasterDbContext db, bool enforceMembership = false, int maxPerDay = 2)
        => new(db, new NoopAudit(), Options.Create(new CheckInOptions
        {
            EnforceMembership = enforceMembership,
            OncePerDay = false,
            MaxPerDay = maxPerDay,
        }));

    private static ClaimsPrincipal Principal(long userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal Member(long userId) => Principal(userId, RoleNames.Member);
    private static ClaimsPrincipal Staff(long userId = 99) => Principal(userId, RoleNames.Staff);
    private static ClaimsPrincipal Pt(long userId) => Principal(userId, RoleNames.Pt);

    // userId 10 <-> memberProfile 1 (mac dinh). status active.
    private static void SeedMember(GymMasterDbContext db, long userId = 10, long memberId = 1,
        string status = UserStatuses.Active, string? phone = null, string fullName = "Member A")
    {
        db.Users.Add(new User { Id = userId, FullName = fullName, Phone = phone, Status = status, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = memberId, UserId = userId, IsDeleted = false });
    }

    // ---------- CreateAsync: member tu check-in ----------

    [Fact] // Member tu check-in cho chinh minh (khong truyen MemberId) -> 201, CreatedBy null, source member
    public async Task Member_self_checkin_without_id_resolves_self()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateAsync(new CreateCheckInRequest(), Member(10), default);

        Assert.True(result.Succeeded);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("member", result.Value!.Source);
        Assert.Null((await db.CheckIns.SingleAsync()).CreatedBy);
    }

    [Fact] // Member co gang check-in cho member khac -> FORBIDDEN
    public async Task Member_cannot_checkin_for_someone_else()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        SeedMember(db, userId: 20, memberId: 2);
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateAsync(
            new CreateCheckInRequest { MemberId = 2 }, Member(10), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // Staff check-in ho -> CreatedBy = staff userId, source front-desk
    public async Task Staff_checkin_sets_createdby_and_frontdesk_source()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateAsync(
            new CreateCheckInRequest { MemberId = 1 }, Staff(99), default);

        Assert.True(result.Succeeded);
        Assert.Equal("front-desk", result.Value!.Source);
        Assert.Equal(99, (await db.CheckIns.SingleAsync()).CreatedBy);
    }

    [Fact] // Tra cuu theo MemberCode (= SDT)
    public async Task Checkin_resolves_member_by_code_phone()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1, phone: "0911222333");
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateAsync(
            new CreateCheckInRequest { MemberCode = " 0911222333 " }, Staff(), default);

        Assert.True(result.Succeeded);
    }

    [Fact] // Khong tim thay member -> MEMBER_NOT_FOUND
    public async Task Checkin_member_not_found()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(
            new CreateCheckInRequest { MemberId = 999 }, Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("MEMBER_NOT_FOUND", result.ErrorCode);
    }

    [Fact] // Tai khoan khoa -> ACCOUNT_LOCKED
    public async Task Checkin_blocked_when_account_locked()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1, status: UserStatuses.Locked);
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateAsync(
            new CreateCheckInRequest { MemberId = 1 }, Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("ACCOUNT_LOCKED", result.ErrorCode);
    }

    // ---------- Membership enforcement ----------

    [Fact] // EnforceMembership, khong co goi Active -> NO_ACTIVE_MEMBERSHIP
    public async Task Checkin_no_active_membership()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        await db.SaveChangesAsync();

        var result = await NewService(db, enforceMembership: true).CreateAsync(
            new CreateCheckInRequest { MemberId = 1 }, Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NO_ACTIVE_MEMBERSHIP", result.ErrorCode);
    }

    [Fact] // EnforceMembership, co goi cho thanh toan -> PAYMENT_PENDING
    public async Task Checkin_payment_pending_membership()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        db.Memberships.Add(new Membership
        {
            Id = 1, MemberId = 1, PackageId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Status = MembershipStatus.PendingPayment, CreatedByUserId = 99
        });
        await db.SaveChangesAsync();

        var result = await NewService(db, enforceMembership: true).CreateAsync(
            new CreateCheckInRequest { MemberId = 1 }, Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("PAYMENT_PENDING", result.ErrorCode);
    }

    [Fact] // EnforceMembership, co goi Active con han -> cho qua
    public async Task Checkin_passes_with_active_membership()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        db.Memberships.Add(new Membership
        {
            Id = 1, MemberId = 1, PackageId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Status = MembershipStatus.Active, CreatedByUserId = 99
        });
        await db.SaveChangesAsync();

        var result = await NewService(db, enforceMembership: true).CreateAsync(
            new CreateCheckInRequest { MemberId = 1 }, Staff(), default);

        Assert.True(result.Succeeded);
    }

    // ---------- ListAsync ----------

    [Fact] // List loc theo memberId + kem ten hoi vien
    public async Task List_filters_by_member_and_includes_name()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1, fullName: "Nguyen Van List");
        SeedMember(db, userId: 20, memberId: 2, fullName: "Other");
        await db.SaveChangesAsync();
        var svc = NewService(db);
        await svc.CreateAsync(new CreateCheckInRequest { MemberId = 1 }, Staff(), default);
        await svc.CreateAsync(new CreateCheckInRequest { MemberId = 2 }, Staff(), default);

        var result = await svc.ListAsync(null, 1, default);

        Assert.True(result.Succeeded);
        var row = Assert.Single(result.Value!);
        Assert.Equal("Nguyen Van List", row.MemberName);
    }

    [Fact] // List loc theo ngay hom nay -> lay duoc ban ghi vua tao
    public async Task List_filters_by_today_date()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        await db.SaveChangesAsync();
        var svc = NewService(db);
        await svc.CreateAsync(new CreateCheckInRequest { MemberId = 1 }, Staff(), default);

        var today = AppClock.Today();
        var result = await svc.ListAsync(today, null, default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    // ---------- ListByMemberAsync (ownership) ----------

    [Fact] // Member xem check-in cua chinh minh
    public async Task ListByMember_member_sees_own()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        await db.SaveChangesAsync();
        await NewService(db).CreateAsync(new CreateCheckInRequest { MemberId = 1 }, Staff(), default);

        var result = await NewService(db).ListByMemberAsync(1, Member(10), default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Fact] // Member khac -> FORBIDDEN
    public async Task ListByMember_other_member_forbidden()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        SeedMember(db, userId: 20, memberId: 2);
        await db.SaveChangesAsync();

        var result = await NewService(db).ListByMemberAsync(1, Member(20), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // Member khong ton tai -> MEMBER_NOT_FOUND
    public async Task ListByMember_not_found()
    {
        using var db = NewDb();
        var result = await NewService(db).ListByMemberAsync(999, Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("MEMBER_NOT_FOUND", result.ErrorCode);
    }

    // ---------- CreateForAssignedMemberAsync (PT) ----------

    private static void SeedTrainer(GymMasterDbContext db, long userId, long trainerId)
    {
        db.Users.Add(new User { Id = userId, FullName = "PT", Status = UserStatuses.Active, IsDeleted = false });
        db.TrainerProfiles.Add(new TrainerProfile { Id = trainerId, UserId = userId, IsDeleted = false });
    }

    private static void SeedAssignment(GymMasterDbContext db, long trainerId, long memberId, byte status = AssignmentStatuses.Active)
        => db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerId, MemberId = memberId, Status = status });

    [Fact] // PT check-in cho hoi vien duoc phan cong -> 201
    public async Task PtCheckin_assigned_member_succeeds()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        SeedTrainer(db, userId: 50, trainerId: 5);
        SeedAssignment(db, trainerId: 5, memberId: 1);
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateForAssignedMemberAsync(1, Pt(50), default);

        Assert.True(result.Succeeded);
        Assert.Equal(50, (await db.CheckIns.SingleAsync()).CreatedBy);
    }

    [Fact] // PT check-in cho hoi vien KHONG duoc phan cong -> FORBIDDEN
    public async Task PtCheckin_unassigned_member_forbidden()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        SeedTrainer(db, userId: 50, trainerId: 5);
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateForAssignedMemberAsync(1, Pt(50), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // Nguoi goi khong phai PT (khong co ho so trainer) -> TRAINER_NOT_FOUND
    public async Task PtCheckin_no_trainer_profile()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateForAssignedMemberAsync(1, Pt(50), default);

        Assert.False(result.Succeeded);
        Assert.Equal("TRAINER_NOT_FOUND", result.ErrorCode);
    }

    // ---------- ListTodayForTrainerAsync ----------

    [Fact] // PT xem check-in hom nay cua hoi vien duoc phan cong
    public async Task ListTodayForTrainer_returns_assigned_checkins()
    {
        using var db = NewDb();
        SeedMember(db, userId: 10, memberId: 1);
        SeedTrainer(db, userId: 50, trainerId: 5);
        SeedAssignment(db, trainerId: 5, memberId: 1);
        await db.SaveChangesAsync();
        await NewService(db).CreateForAssignedMemberAsync(1, Pt(50), default);

        var result = await NewService(db).ListTodayForTrainerAsync(Pt(50), default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Fact] // PT khong co phan cong nao -> danh sach rong
    public async Task ListTodayForTrainer_empty_when_no_assignment()
    {
        using var db = NewDb();
        SeedTrainer(db, userId: 50, trainerId: 5);
        await db.SaveChangesAsync();

        var result = await NewService(db).ListTodayForTrainerAsync(Pt(50), default);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
    }

    [Fact] // Khong co ho so PT -> TRAINER_NOT_FOUND
    public async Task ListTodayForTrainer_no_profile()
    {
        using var db = NewDb();
        var result = await NewService(db).ListTodayForTrainerAsync(Pt(50), default);

        Assert.False(result.Succeeded);
        Assert.Equal("TRAINER_NOT_FOUND", result.ErrorCode);
    }
}
