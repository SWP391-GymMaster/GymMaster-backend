using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;
using GymMaster.API.Features.Account;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Infrastructure;

namespace GymMaster.Api.Tests;

public class AccountServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Principal(long userId, string role = RoleNames.Member)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private static AccountService NewService(GymMasterDbContext db, FakeAvatarStorage? storage = null)
    {
        return new AccountService(db, storage ?? new FakeAvatarStorage(), new NoopAudit());
    }

    private static async Task SeedUsersAsync(GymMasterDbContext db)
    {
        var role = new Role { Id = 1, Name = RoleNames.Member, Description = "member role" };
        var user = new User
        {
            Id = 10,
            Email = "member@gymmaster.local",
            FullName = "Gym Member",
            Phone = "0900000001",
            PasswordHash = "hash",
            Status = UserStatuses.Active,
        };
        var other = new User
        {
            Id = 11,
            Email = "other@gymmaster.local",
            FullName = "Other Member",
            Phone = "0900000002",
            PasswordHash = "hash",
            Status = UserStatuses.Active,
        };

        user.UserRoles.Add(new UserRole { User = user, Role = role });
        other.UserRoles.Add(new UserRole { User = other, Role = role });

        db.Roles.Add(role);
        db.Users.AddRange(user, other);
        await db.SaveChangesAsync();
    }

    private static async Task<User> SeedUserAsync(GymMasterDbContext db, long id, string roleName)
    {
        var role = new Role { Name = roleName, Description = $"{roleName} role" };
        var user = new User
        {
            Id = id,
            Email = $"{roleName}.{id}@gymmaster.local",
            FullName = $"{roleName} user",
            Phone = $"090000{id:D4}",
            PasswordHash = "hash",
            Status = UserStatuses.Active,
        };

        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Roles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static IFormFile FormFile(string contentType, long length)
    {
        var content = new MemoryStream(new byte[length]);
        return new FormFile(content, 0, length, "file", "avatar.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task Update_rejects_duplicate_phone()
    {
        using var db = NewDb();
        await SeedUsersAsync(db);

        var result = await NewService(db).UpdateAsync(
            Principal(10),
            new UpdateMyAccountRequest(null, "0900000002"),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("DUPLICATE", result.ErrorCode);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("0900000001", (await db.Users.FindAsync(10L))!.Phone);
    }

    [Fact]
    public async Task UploadAvatar_rejects_wrong_content_type_before_storage()
    {
        using var db = NewDb();
        await SeedUsersAsync(db);
        var storage = new FakeAvatarStorage();

        var result = await NewService(db, storage).UploadAvatarAsync(
            Principal(10),
            FormFile("image/gif", 1024),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(0, storage.UploadCount);
    }

    [Fact]
    public async Task UploadAvatar_rejects_oversize_file_before_storage()
    {
        using var db = NewDb();
        await SeedUsersAsync(db);
        var storage = new FakeAvatarStorage();

        var result = await NewService(db, storage).UploadAvatarAsync(
            Principal(10),
            FormFile("image/png", 5 * 1024 * 1024 + 1),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(0, storage.UploadCount);
    }

    [Fact]
    public async Task GetProfile_staff_lazy_creates_once_and_reuses_profile()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, 20, RoleNames.Staff);
        var service = NewService(db);

        var first = await service.GetProfileAsync(Principal(user.Id, RoleNames.Staff), default);
        var second = await service.GetProfileAsync(Principal(user.Id, RoleNames.Staff), default);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Single(await db.StaffProfiles.Where(item => item.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task UpdateProfile_staff_updates_personal_profile()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, 21, RoleNames.Staff);
        var dateOfBirth = new DateTime(1995, 3, 5);

        var result = await NewService(db).UpdateProfileAsync(
            Principal(user.Id, RoleNames.Staff),
            new UpdatePersonalProfileRequest(dateOfBirth, " male ", " 1 Nguyen Trai ", " 0909123456 "),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal(dateOfBirth, result.Value!.DateOfBirth);
        Assert.Equal("male", result.Value.Gender);
        Assert.Equal("1 Nguyen Trai", result.Value.Address);
        Assert.Equal("0909123456", result.Value.EmergencyContact);
    }

    [Fact]
    public async Task UpdateProfile_pt_updates_personal_fields_without_touching_specialty()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, 22, RoleNames.Pt);
        db.TrainerProfiles.Add(new TrainerProfile
        {
            UserId = user.Id,
            User = user,
            Specialty = "Strength",
            Bio = "Coach bio"
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).UpdateProfileAsync(
            Principal(user.Id, RoleNames.Pt),
            new UpdatePersonalProfileRequest(null, " female ", " PT address ", " 0909888777 "),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal("female", result.Value!.Gender);
        Assert.Equal("PT address", result.Value.Address);

        var profile = await db.TrainerProfiles.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal("Strength", profile.Specialty);
        Assert.Equal("Coach bio", profile.Bio);
    }

    [Fact]
    public async Task UpdateProfile_pt_without_profile_returns_not_found()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, 23, RoleNames.Pt);

        var result = await NewService(db).UpdateProfileAsync(
            Principal(user.Id, RoleNames.Pt),
            new UpdatePersonalProfileRequest(null, null, "PT address", null),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_member_returns_not_supported()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, 24, RoleNames.Member);

        var result = await NewService(db).UpdateProfileAsync(
            Principal(user.Id, RoleNames.Member),
            new UpdatePersonalProfileRequest(null, null, "Member address", null),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_SUPPORTED", result.ErrorCode);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeAvatarStorage : IAvatarStorage
    {
        public int UploadCount { get; private set; }

        public Task<string> UploadAsync(
            long userId,
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
        {
            UploadCount++;
            return Task.FromResult("https://cdn.example/avatar.webp");
        }
    }
}
