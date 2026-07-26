using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Features.Members;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Bo sung coverage MemberService: UpdateAsync, GetByIdAsync, GetCurrentMemberProfileIdAsync.
public class MemberServiceCoverageTests
{
    private static GymMasterDbContext NewDb()
        => new(new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"membercov-{Guid.NewGuid()}").Options);

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static MemberService NewService(GymMasterDbContext db) => new(db, new NoopAudit());

    private static ClaimsPrincipal Principal(long userId, string role)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    // Seed member: user(id) + profile(profileId). Tra ve profileId.
    private static async Task<(long userId, long profileId)> SeedMemberAsync(
        GymMasterDbContext db, long userId, string email = "m@gym.local", string? phone = null)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Member)
            ?? db.Roles.Add(new Role { Name = RoleNames.Member, Description = "member" }).Entity;
        var user = new User
        {
            Id = userId, Email = email, Phone = phone, FullName = "Member X",
            PasswordHash = "hash", Status = UserStatuses.Active,
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        var profile = new MemberProfile { UserId = userId, User = user, JoinedAt = DateTime.UtcNow };
        db.Users.Add(user);
        db.MemberProfiles.Add(profile);
        await db.SaveChangesAsync();
        return (userId, profile.Id);
    }

    // ---------- CreateAsync: nhanh loi con thieu ----------

    [Fact] // Thieu ho ten -> VALIDATION_ERROR
    public async Task Create_missing_fullname_validation_error()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(
            new CreateMemberRequest("", "a@gym.local", null, null, null, null, null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact] // PersonValidation: SDT qua dai
    public async Task Create_person_validation_error()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(
            new CreateMemberRequest("Ten", "a@gym.local", new string('9', 40), null, null, null, null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // ---------- UpdateAsync ----------

    [Fact] // Admin cap nhat ho so -> thanh cong
    public async Task Update_by_admin_updates_fields()
    {
        using var db = NewDb();
        var (_, profileId) = await SeedMemberAsync(db, 10);

        var result = await NewService(db).UpdateAsync(
            profileId,
            new UpdateMemberRequest("Ten Moi", "0912000000", new DateTime(1990, 1, 1), "female", "Ha Noi", "0900"),
            Principal(99, RoleNames.Admin),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal("Ten Moi", result.Value!.FullName);
        Assert.Equal("Ha Noi", result.Value.Address);
    }

    [Fact] // Member tu cap nhat ho so cua chinh minh
    public async Task Update_by_owner_member_succeeds()
    {
        using var db = NewDb();
        var (userId, profileId) = await SeedMemberAsync(db, 10);

        var result = await NewService(db).UpdateAsync(
            profileId,
            new UpdateMemberRequest("Self Updated", null, null, null, null, null),
            Principal(userId, RoleNames.Member),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal("Self Updated", result.Value!.FullName);
    }

    [Fact] // Member khac -> FORBIDDEN
    public async Task Update_by_other_member_forbidden()
    {
        using var db = NewDb();
        var (_, profileId) = await SeedMemberAsync(db, 10);

        var result = await NewService(db).UpdateAsync(
            profileId,
            new UpdateMemberRequest("Hack", null, null, null, null, null),
            Principal(20, RoleNames.Member),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // Khong tim thay ho so -> NOT_FOUND
    public async Task Update_not_found()
    {
        using var db = NewDb();
        var result = await NewService(db).UpdateAsync(
            999, new UpdateMemberRequest("x", null, null, null, null, null),
            Principal(99, RoleNames.Admin), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact] // Ngay sinh tuong lai -> VALIDATION_ERROR
    public async Task Update_validation_error()
    {
        using var db = NewDb();
        var (_, profileId) = await SeedMemberAsync(db, 10);

        var result = await NewService(db).UpdateAsync(
            profileId,
            new UpdateMemberRequest(null, null, DateTime.UtcNow.AddYears(1), null, null, null),
            Principal(99, RoleNames.Admin), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact] // SDT trung nguoi khac -> DUPLICATE
    public async Task Update_duplicate_phone_conflict()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, 10, email: "a@gym.local", phone: "0912000001");
        var (_, profileId2) = await SeedMemberAsync(db, 20, email: "b@gym.local");

        var result = await NewService(db).UpdateAsync(
            profileId2,
            new UpdateMemberRequest(null, "0912000001", null, null, null, null),
            Principal(99, RoleNames.Admin), default);

        Assert.False(result.Succeeded);
        Assert.Equal("DUPLICATE", result.ErrorCode);
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetById_admin_sees_any()
    {
        using var db = NewDb();
        var (_, profileId) = await SeedMemberAsync(db, 10);

        var result = await NewService(db).GetByIdAsync(profileId, Principal(99, RoleNames.Admin), default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task GetById_other_member_forbidden()
    {
        using var db = NewDb();
        var (_, profileId) = await SeedMemberAsync(db, 10);

        var result = await NewService(db).GetByIdAsync(profileId, Principal(20, RoleNames.Member), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task GetById_not_found()
    {
        using var db = NewDb();
        var result = await NewService(db).GetByIdAsync(999, Principal(99, RoleNames.Admin), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    // ---------- GetCurrentMemberProfileIdAsync ----------

    [Fact]
    public async Task GetCurrent_returns_profile_id()
    {
        using var db = NewDb();
        var (userId, profileId) = await SeedMemberAsync(db, 10);

        var result = await NewService(db).GetCurrentMemberProfileIdAsync(Principal(userId, RoleNames.Member), default);

        Assert.True(result.Succeeded);
        Assert.Equal(profileId, result.Value);
    }

    [Fact]
    public async Task GetCurrent_unauthorized()
    {
        using var db = NewDb();
        var result = await NewService(db).GetCurrentMemberProfileIdAsync(Anonymous(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrent_not_found_when_no_profile()
    {
        using var db = NewDb();
        var result = await NewService(db).GetCurrentMemberProfileIdAsync(Principal(777, RoleNames.Member), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact] // GetOrCreate: non-member khong co ho so -> NOT_FOUND
    public async Task GetOrCreate_non_member_without_profile_not_found()
    {
        using var db = NewDb();
        var result = await NewService(db).GetOrCreateCurrentProfileAsync(Principal(500, RoleNames.Staff), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }
}
