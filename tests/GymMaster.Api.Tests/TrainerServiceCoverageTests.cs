using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Features.Trainers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Bo sung coverage TrainerService: List, GetById, Update + cac nhanh loi cua Create.
public class TrainerServiceCoverageTests
{
    private static GymMasterDbContext NewDb()
        => new(new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"trainercov-{Guid.NewGuid()}").Options);

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static TrainerService NewService(GymMasterDbContext db) => new(db, new NoopAudit());

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static CreateTrainerRequest Req(
        string? fullName = "PT Nguyen",
        string? email = "pt@gym.local",
        string? phone = "0933000001",
        string? password = null,
        string? specialty = "Yoga",
        int? years = 3)
        => new(fullName, email, phone, password, specialty, "bio", "male", new DateTime(1990, 1, 1), years);

    private static async Task<long> SeedTrainerAsync(GymMasterDbContext db)
    {
        var created = await NewService(db).CreateAsync(Req(), default);
        return created.Value!.Trainer.Id;
    }

    // ---------- CreateAsync: nhanh loi ----------

    [Fact]
    public async Task Create_invalid_email()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Req(email: "khongco-a-cong"), default);
        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Create_short_password()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Req(password: "123"), default);
        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Create_negative_years_experience()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Req(years: -1), default);
        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Create_person_validation_error_specialty_too_long()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Req(specialty: new string('x', 200)), default);
        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Create_duplicate_phone()
    {
        using var db = NewDb();
        await NewService(db).CreateAsync(Req(email: "one@gym.local", phone: "0933999999"), default);
        var result = await NewService(db).CreateAsync(Req(email: "two@gym.local", phone: "0933999999"), default);
        Assert.False(result.Succeeded);
        Assert.Equal("DUPLICATE", result.ErrorCode);
    }

    // ---------- ListAsync ----------

    [Fact]
    public async Task List_returns_trainers_and_filters_by_query()
    {
        using var db = NewDb();
        await NewService(db).CreateAsync(Req(fullName: "Alpha PT", email: "alpha@gym.local", phone: "0933000010", specialty: "Boxing"), default);
        await NewService(db).CreateAsync(Req(fullName: "Beta PT", email: "beta@gym.local", phone: "0933000011", specialty: "Yoga"), default);

        var all = await NewService(db).ListAsync(null, 1, 10, default);
        Assert.Equal(2, all.Value!.Total);

        var byName = await NewService(db).ListAsync("Alpha", 1, 10, default);
        Assert.Equal(1, byName.Value!.Total);

        var bySpecialty = await NewService(db).ListAsync("Yoga", 1, 10, default);
        Assert.Equal(1, bySpecialty.Value!.Total);
    }

    [Fact]
    public async Task List_normalizes_invalid_paging()
    {
        using var db = NewDb();
        await SeedTrainerAsync(db);

        var result = await NewService(db).ListAsync(null, 0, 9999, default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Page);
        Assert.Equal(20, result.Value.PageSize);
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetById_found()
    {
        using var db = NewDb();
        var id = await SeedTrainerAsync(db);

        var result = await NewService(db).GetByIdAsync(id, default);

        Assert.True(result.Succeeded);
        Assert.Equal(id, result.Value!.Id);
    }

    [Fact]
    public async Task GetById_not_found()
    {
        using var db = NewDb();
        var result = await NewService(db).GetByIdAsync(999, default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    // ---------- GetCurrentAsync ----------

    [Fact]
    public async Task GetCurrent_unauthorized()
    {
        using var db = NewDb();
        var result = await NewService(db).GetCurrentAsync(Anonymous(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task Update_changes_specialty_and_experience()
    {
        using var db = NewDb();
        var id = await SeedTrainerAsync(db);

        var result = await NewService(db).UpdateAsync(
            id,
            new UpdateTrainerRequest("Powerlifting", "bio moi", "female", new DateTime(1992, 5, 5), 8, "Da Nang", "0900"),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal("Powerlifting", result.Value!.Specialty);
        Assert.Equal(8, result.Value.YearsOfExperience);
    }

    [Fact]
    public async Task Update_not_found()
    {
        using var db = NewDb();
        var result = await NewService(db).UpdateAsync(
            999, new UpdateTrainerRequest(null, null, null, null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Update_negative_years_rejected()
    {
        using var db = NewDb();
        var id = await SeedTrainerAsync(db);

        var result = await NewService(db).UpdateAsync(
            id, new UpdateTrainerRequest(null, null, null, null, -5), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Update_person_validation_error()
    {
        using var db = NewDb();
        var id = await SeedTrainerAsync(db);

        var result = await NewService(db).UpdateAsync(
            id, new UpdateTrainerRequest(new string('x', 200), null, null, null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }
}
