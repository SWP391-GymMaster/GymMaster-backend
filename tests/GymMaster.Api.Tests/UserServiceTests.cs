using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Features.Users;

namespace GymMaster.Api.Tests;

public class UserServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-users-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static UserService NewService(GymMasterDbContext db) => new(db, new NoopAudit());

    private static async Task<User> SeedUserAsync(GymMasterDbContext db, string roleName)
    {
        var role = new Role { Name = roleName, Description = $"{roleName} role" };
        var user = new User
        {
            Email = $"{roleName}.{Guid.NewGuid():N}@gymmaster.local",
            FullName = $"{roleName} user",
            PasswordHash = "hash",
            Status = UserStatuses.Active
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Roles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Update_rejects_staff_to_admin()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, RoleNames.Staff);

        var result = await NewService(db).UpdateAsync(
            user.Id,
            new UpdateUserRequest(null, null, null, RoleNames.Admin),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("ROLE_TRANSITION_NOT_ALLOWED", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(RoleNames.Staff, await CurrentRoleAsync(db, user.Id));
    }

    [Fact]
    public async Task Update_rejects_admin_to_staff()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, RoleNames.Admin);

        var result = await NewService(db).UpdateAsync(
            user.Id,
            new UpdateUserRequest(null, null, null, RoleNames.Staff),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("ROLE_TRANSITION_NOT_ALLOWED", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(RoleNames.Admin, await CurrentRoleAsync(db, user.Id));
    }

    [Fact]
    public async Task Update_same_role_is_noop_success()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, RoleNames.Member);

        var result = await NewService(db).UpdateAsync(
            user.Id,
            new UpdateUserRequest("Member Updated", null, null, RoleNames.Member),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal(RoleNames.Member, result.Value!.Role);
        Assert.Equal("Member Updated", result.Value.FullName);
        Assert.Equal(RoleNames.Member, await CurrentRoleAsync(db, user.Id));
    }

    [Theory]
    [InlineData(RoleNames.Member, RoleNames.Staff)]
    [InlineData(RoleNames.Staff, RoleNames.Pt)]
    [InlineData(RoleNames.Pt, RoleNames.Member)]
    public async Task Update_rejects_persona_role_transitions(string currentRole, string requestedRole)
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, currentRole);

        var result = await NewService(db).UpdateAsync(
            user.Id,
            new UpdateUserRequest(null, null, null, requestedRole),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("ROLE_TRANSITION_NOT_ALLOWED", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(currentRole, await CurrentRoleAsync(db, user.Id));
    }

    [Fact]
    public async Task Update_staff_personal_fields_creates_staff_profile()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, RoleNames.Staff);
        var dateOfBirth = new DateTime(1996, 4, 12);

        var result = await NewService(db).UpdateAsync(
            user.Id,
            new UpdateUserRequest(
                null,
                null,
                null,
                null,
                dateOfBirth,
                " female ",
                " 12 Le Loi ",
                " 0900000009 "),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal(dateOfBirth, result.Value!.DateOfBirth);
        Assert.Equal("female", result.Value.Gender);
        Assert.Equal("12 Le Loi", result.Value.Address);
        Assert.Equal("0900000009", result.Value.EmergencyContact);

        var profile = await db.StaffProfiles.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal("12 Le Loi", profile.Address);
    }

    [Fact]
    public async Task Update_member_personal_fields_are_rejected_in_users_door()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, RoleNames.Member);

        var result = await NewService(db).UpdateAsync(
            user.Id,
            new UpdateUserRequest(null, null, null, null, Address: "Member Address"),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.False(await db.StaffProfiles.AnyAsync());
    }

    private static async Task<string> CurrentRoleAsync(GymMasterDbContext db, long userId)
    {
        var user = await db.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .SingleAsync(item => item.Id == userId);

        return user.UserRoles.Select(item => item.Role.Name).Single();
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }
}
