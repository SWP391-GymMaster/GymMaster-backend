using System.Security.Claims;
using System.Text.Json;
using GymMaster.API.Data;
using GymMaster.API.Features.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace GymMaster.Api.Tests;

// AuditService: ghi AuditLog cho moi mutating action quan trong (PATTERNS BAT BUOC).
public class AuditServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static IHttpContextAccessor Accessor(ClaimsPrincipal? user)
    {
        var ctx = new DefaultHttpContext();
        if (user is not null)
        {
            ctx.User = user;
        }
        return new HttpContextAccessor { HttpContext = ctx };
    }

    private static ClaimsPrincipal UserWith(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));

    [Fact] // LogAsync ghi day du field va lay actor tu claim NameIdentifier
    public async Task LogAsync_persists_audit_with_actor_from_nameidentifier()
    {
        using var db = NewDb();
        var user = UserWith(new Claim(ClaimTypes.NameIdentifier, "42"));
        var service = new AuditService(db, Accessor(user));

        await service.LogAsync("CREATE_MEMBER", "MemberProfile", 7, new { name = "Minh" }, default);

        var log = Assert.Single(db.AuditLogs);
        Assert.Equal(42, log.UserId);
        Assert.Equal("CREATE_MEMBER", log.Action);
        Assert.Equal("MemberProfile", log.Entity);
        Assert.Equal(7, log.EntityId);
        Assert.NotNull(log.Metadata);
        Assert.Contains("Minh", log.Metadata!);
        Assert.True(log.CreatedAt <= DateTime.UtcNow);
    }

    [Fact] // metadata null -> khong serialize, luu null
    public async Task LogAsync_stores_null_metadata_when_metadata_is_null()
    {
        using var db = NewDb();
        var service = new AuditService(db, Accessor(UserWith(new Claim(ClaimTypes.NameIdentifier, "1"))));

        await service.LogAsync("DELETE_X", "X", 1, null, default);

        Assert.Null(Assert.Single(db.AuditLogs).Metadata);
    }

    [Fact] // metadata la object -> serialize dung JSON
    public async Task LogAsync_serializes_metadata_object_to_json()
    {
        using var db = NewDb();
        var service = new AuditService(db, Accessor(UserWith(new Claim(ClaimTypes.NameIdentifier, "1"))));

        await service.LogAsync("UPDATE", "Membership", 3, new { from = "Active", to = "Expired" }, default);

        var meta = Assert.Single(db.AuditLogs).Metadata!;
        using var doc = JsonDocument.Parse(meta);
        Assert.Equal("Active", doc.RootElement.GetProperty("from").GetString());
        Assert.Equal("Expired", doc.RootElement.GetProperty("to").GetString());
    }

    [Fact] // Khong co NameIdentifier nhung co Sub -> lay tu Sub
    public async Task LogAsync_falls_back_to_sub_claim()
    {
        using var db = NewDb();
        var user = UserWith(new Claim(JwtRegisteredClaimNames.Sub, "88"));
        var service = new AuditService(db, Accessor(user));

        await service.LogAsync("A", "E", 1, null, default);

        Assert.Equal(88, Assert.Single(db.AuditLogs).UserId);
    }

    [Fact] // Khong co HttpContext -> actor null (vd job chay nen)
    public async Task LogAsync_actor_null_when_no_httpcontext()
    {
        using var db = NewDb();
        var service = new AuditService(db, new HttpContextAccessor { HttpContext = null });

        await service.LogAsync("A", "E", 1, null, default);

        Assert.Null(Assert.Single(db.AuditLogs).UserId);
    }

    [Fact] // Claim khong parse duoc thanh so -> actor null
    public async Task LogAsync_actor_null_when_claim_not_numeric()
    {
        using var db = NewDb();
        var user = UserWith(new Claim(ClaimTypes.NameIdentifier, "not-a-number"));
        var service = new AuditService(db, Accessor(user));

        await service.LogAsync("A", "E", 1, null, default);

        Assert.Null(Assert.Single(db.AuditLogs).UserId);
    }
}
