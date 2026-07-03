using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.Api.Tests;

public class TrainerServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Pt(long userId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Pt),
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private static TrainerService NewService(GymMasterDbContext db)
        => new(db, new NoopAudit());

    [Fact]
    public async Task GetCurrent_returns_own_trainer_profile_for_pt()
    {
        using var db = NewDb();
        var user = new User
        {
            Id = 20,
            Email = "pt@gymmaster.local",
            FullName = "GymMaster PT",
            PasswordHash = "hash",
            Status = UserStatuses.Active
        };
        db.Users.Add(user);
        db.TrainerProfiles.Add(new TrainerProfile
        {
            Id = 7,
            UserId = user.Id,
            Specialty = "Strength",
            Bio = "Coach bio",
            YearsOfExperience = 5,
            IsDeleted = false
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCurrentAsync(Pt(user.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.Value!.Id);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("pt@gymmaster.local", result.Value.Email);
        Assert.Equal("Strength", result.Value.Specialty);
    }

    [Fact]
    public async Task GetCurrent_returns_not_found_when_pt_has_no_profile()
    {
        using var db = NewDb();
        db.Users.Add(new User
        {
            Id = 21,
            Email = "pt2@gymmaster.local",
            FullName = "GymMaster PT 2",
            PasswordHash = "hash",
            Status = UserStatuses.Active
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCurrentAsync(Pt(21), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal("Khong tim thay ho so huan luyen vien.", result.ErrorMessage);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }
}
