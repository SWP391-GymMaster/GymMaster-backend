using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Account;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Bo sung coverage AccountService: happy path Update/UploadAvatar/GetProfile + nhanh unauthorized/loi storage.
public class AccountServiceCoverageTests
{
    private static GymMasterDbContext NewDb()
        => new(new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-acccov-{Guid.NewGuid()}").Options);

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class OkAvatarStorage : IAvatarStorage
    {
        public Task<string> UploadAsync(long userId, Stream content, string contentType, CancellationToken ct)
            => Task.FromResult("https://cdn.example/new-avatar.webp");
    }

    private sealed class ThrowingAvatarStorage : IAvatarStorage
    {
        private readonly string _code;
        public ThrowingAvatarStorage(string code) => _code = code;
        public Task<string> UploadAsync(long userId, Stream content, string contentType, CancellationToken ct)
            => throw new AvatarStorageException(_code, "loi upload");
    }

    private static AccountService NewService(GymMasterDbContext db, IAvatarStorage? storage = null)
        => new(db, storage ?? new OkAvatarStorage(), new NoopAudit());

    private static ClaimsPrincipal Principal(long userId, string role)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static async Task<User> SeedUserAsync(GymMasterDbContext db, long id, string roleName)
    {
        var role = new Role { Name = roleName, Description = $"{roleName} role" };
        var user = new User
        {
            Id = id,
            Email = $"{roleName}.{id}@gymmaster.local",
            FullName = $"{roleName} user",
            Phone = $"09111{id:D4}",
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
        => new FormFile(new MemoryStream(new byte[length]), 0, length, "file", "avatar.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

    // ---------- UpdateAsync ----------

    [Fact] // Cap nhat ten + SDT thanh cong
    public async Task Update_changes_name_and_phone()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, 10, RoleNames.Staff);

        var result = await NewService(db).UpdateAsync(
            Principal(10, RoleNames.Staff),
            new UpdateMyAccountRequest("  Ten Moi ", " 0912345678 "),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal("Ten Moi", result.Value!.FullName);
        var saved = await db.Users.FindAsync(10L);
        Assert.Equal("0912345678", saved!.Phone);
    }

    [Fact] // Khong co user (token sai) -> UNAUTHORIZED
    public async Task Update_unauthorized_when_no_user()
    {
        using var db = NewDb();
        var result = await NewService(db).UpdateAsync(Anonymous(), new UpdateMyAccountRequest("x", null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact] // PersonValidation: ten qua dai -> VALIDATION_ERROR
    public async Task Update_validation_error_when_name_too_long()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Staff);

        var result = await NewService(db).UpdateAsync(
            Principal(10, RoleNames.Staff),
            new UpdateMyAccountRequest(new string('a', 200), null),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact] // SDT trung nguoi khac -> DUPLICATE
    public async Task Update_duplicate_phone_conflict()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Staff);
        await SeedUserAsync(db, 11, RoleNames.Staff); // phone 091110011

        var result = await NewService(db).UpdateAsync(
            Principal(10, RoleNames.Staff),
            new UpdateMyAccountRequest(null, "091110011"),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("DUPLICATE", result.ErrorCode);
    }

    // ---------- UploadAvatarAsync ----------

    [Fact] // Upload hop le -> luu AvatarUrl
    public async Task UploadAvatar_success_sets_url()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Member);

        var result = await NewService(db).UploadAvatarAsync(
            Principal(10, RoleNames.Member),
            FormFile("image/png", 1024),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal("https://cdn.example/new-avatar.webp", result.Value!.AvatarUrl);
    }

    [Fact] // Khong co user -> UNAUTHORIZED
    public async Task UploadAvatar_unauthorized()
    {
        using var db = NewDb();
        var result = await NewService(db).UploadAvatarAsync(Anonymous(), FormFile("image/png", 10), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact] // File rong -> VALIDATION_ERROR
    public async Task UploadAvatar_empty_file_rejected()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Member);

        var result = await NewService(db).UploadAvatarAsync(
            Principal(10, RoleNames.Member), FormFile("image/png", 0), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact] // Storage bao chua cau hinh -> 500
    public async Task UploadAvatar_not_configured_maps_to_500()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Member);

        var result = await NewService(db, new ThrowingAvatarStorage("CLOUDINARY_NOT_CONFIGURED"))
            .UploadAvatarAsync(Principal(10, RoleNames.Member), FormFile("image/png", 100), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact] // Loi storage khac -> 502
    public async Task UploadAvatar_other_storage_error_maps_to_502()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Member);

        var result = await NewService(db, new ThrowingAvatarStorage("UPLOAD_FAILED"))
            .UploadAvatarAsync(Principal(10, RoleNames.Member), FormFile("image/png", 100), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
    }

    // ---------- GetProfileAsync ----------

    [Fact] // Admin/Staff -> tra ve profile (lazy-create)
    public async Task GetProfile_staff_returns_profile()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Staff);

        var result = await NewService(db).GetProfileAsync(Principal(10, RoleNames.Staff), default);

        Assert.True(result.Succeeded);
    }

    [Fact] // Member -> NOT_SUPPORTED (dung /members/me)
    public async Task GetProfile_member_not_supported()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Member);

        var result = await NewService(db).GetProfileAsync(Principal(10, RoleNames.Member), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_SUPPORTED", result.ErrorCode);
    }

    [Fact] // PT chua co ho so -> NOT_FOUND
    public async Task GetProfile_pt_without_profile_not_found()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Pt);

        var result = await NewService(db).GetProfileAsync(Principal(10, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact] // Khong co user -> UNAUTHORIZED
    public async Task GetProfile_unauthorized()
    {
        using var db = NewDb();
        var result = await NewService(db).GetProfileAsync(Anonymous(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    // ---------- UpdateProfileAsync ----------

    [Fact] // Khong co user -> UNAUTHORIZED
    public async Task UpdateProfile_unauthorized()
    {
        using var db = NewDb();
        var result = await NewService(db).UpdateProfileAsync(
            Anonymous(), new UpdatePersonalProfileRequest(null, null, null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact] // Ngay sinh tuong lai -> VALIDATION_ERROR
    public async Task UpdateProfile_validation_error_future_dob()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Staff);

        var result = await NewService(db).UpdateProfileAsync(
            Principal(10, RoleNames.Staff),
            new UpdatePersonalProfileRequest(DateTime.UtcNow.AddYears(1), null, null, null),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact] // Member cap nhat profile qua door nay -> NOT_SUPPORTED
    public async Task UpdateProfile_member_not_supported()
    {
        using var db = NewDb();
        await SeedUserAsync(db, 10, RoleNames.Member);

        var result = await NewService(db).UpdateProfileAsync(
            Principal(10, RoleNames.Member),
            new UpdatePersonalProfileRequest(null, "male", null, null),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_SUPPORTED", result.ErrorCode);
    }
}
