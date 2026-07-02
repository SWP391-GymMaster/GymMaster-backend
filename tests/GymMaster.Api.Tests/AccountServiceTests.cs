using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

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
