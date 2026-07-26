using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Billing;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Infrastructure;
using GymMaster.API.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace GymMaster.Api.Tests;

// Bo sung coverage VnPayService: HandleReturnAsync + cac nhanh loi cua CreatePaymentUrlAsync.
public class VnPayServiceCoverageTests
{
    private const decimal Price = 500_000m;
    private const string Secret = "TEST_HASH_SECRET";

    private static GymMasterDbContext NewDb()
        => new(new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"vnpaycov-{Guid.NewGuid()}").Options);

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static VnPayService NewService(GymMasterDbContext db, bool configured = true)
        => new(db, new NoopAudit(), Options.Create(new VnPayOptions
        {
            TmnCode = configured ? "TESTCODE" : "",
            HashSecret = configured ? Secret : "",
            ReturnUrl = "http://localhost/return",
        }));

    private static ClaimsPrincipal Principal(long userId, string role)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    // member profile(1, user 10) + package(1) + membership(1) trang thai truyen vao.
    private static async Task SeedAsync(GymMasterDbContext db, MembershipStatus status = MembershipStatus.PendingPayment)
    {
        var today = AppClock.Today();
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.MembershipPackages.Add(new MembershipPackage { Id = 1, Name = "Goi 1 thang", DurationDays = 30, Price = Price, IsActive = true });
        db.Memberships.Add(new Membership
        {
            Id = 1, MemberId = 1, PackageId = 1, StartDate = today, EndDate = today.AddDays(30),
            Status = status, CreatedByUserId = 99, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static string Sign(IReadOnlyDictionary<string, string> data, string secret)
    {
        var library = new VnPayLibrary();
        foreach (var (key, value) in data)
        {
            library.AddRequestData(key, value);
        }
        var url = library.CreateRequestUrl(string.Empty, secret);
        const string marker = "vnp_SecureHash=";
        return url[(url.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
    }

    private static Dictionary<string, string> SuccessParams(long paymentId, decimal amount)
        => new()
        {
            ["vnp_TmnCode"] = "TESTCODE",
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
            ["vnp_TxnRef"] = paymentId.ToString(),
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
        };

    private static Dictionary<string, string> Signed(Dictionary<string, string> p, string secret)
    {
        p["vnp_SecureHash"] = Sign(p, secret);
        return p;
    }

    // ---------- CreatePaymentUrlAsync: nhanh loi ----------

    [Fact]
    public async Task Create_forbidden_when_no_actor()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Anonymous(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task Create_not_configured()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db, configured: false).CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Principal(99, RoleNames.Staff), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VNPAY_NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public async Task Create_membership_not_found()
    {
        using var db = NewDb();

        var result = await NewService(db).CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(999), "127.0.0.1", Principal(99, RoleNames.Staff), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Create_invalid_membership_state()
    {
        using var db = NewDb();
        await SeedAsync(db, status: MembershipStatus.Active);

        var result = await NewService(db).CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Principal(99, RoleNames.Staff), default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_MEMBERSHIP_STATE", result.ErrorCode);
    }

    [Fact] // Member khac (khong phai chu ho so) -> FORBIDDEN
    public async Task Create_forbidden_for_other_member()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Principal(20, RoleNames.Member), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // Chu ho so tu thanh toan -> OK
    public async Task Create_owner_member_succeeds()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Principal(10, RoleNames.Member), default);

        Assert.True(result.Succeeded);
        Assert.Contains("vnp_SecureHash", result.Value!.PayUrl);
    }

    // ---------- HandleReturnAsync ----------

    private static async Task<long> CreatePaymentAsync(GymMasterDbContext db)
    {
        var created = await NewService(db).CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Principal(99, RoleNames.Staff), default);
        return created.Value!.PaymentId;
    }

    [Fact] // Chu ky sai -> INVALID_SIGNATURE
    public async Task Return_invalid_signature()
    {
        using var db = NewDb();
        await SeedAsync(db);
        var paymentId = await CreatePaymentAsync(db);
        var p = SuccessParams(paymentId, Price);
        p["vnp_SecureHash"] = "deadbeef"; // sai chu ky

        var result = await NewService(db).HandleReturnAsync(p, default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
    }

    [Fact] // Khong tim thay giao dich
    public async Task Return_payment_not_found()
    {
        using var db = NewDb();
        var p = Signed(SuccessParams(999999, Price), Secret);

        var result = await NewService(db).HandleReturnAsync(p, default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact] // So tien khong khop
    public async Task Return_amount_mismatch()
    {
        using var db = NewDb();
        await SeedAsync(db);
        var paymentId = await CreatePaymentAsync(db);
        var p = Signed(SuccessParams(paymentId, 999m), Secret);

        var result = await NewService(db).HandleReturnAsync(p, default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_AMOUNT", result.ErrorCode);
    }

    [Fact] // Thanh cong -> finalize, membership Active, payment Paid
    public async Task Return_success_finalizes_payment()
    {
        using var db = NewDb();
        await SeedAsync(db);
        var paymentId = await CreatePaymentAsync(db);
        var p = Signed(SuccessParams(paymentId, Price), Secret);

        var result = await NewService(db).HandleReturnAsync(p, default);

        Assert.True(result.Succeeded);
        Assert.Equal(PaymentStatus.Paid.ToString(), result.Value!.Status);
        var membership = await db.Memberships.FindAsync(1L);
        Assert.Equal(MembershipStatus.Active, membership!.Status);
    }

    [Fact] // HandleIpn: thieu chu ky -> "97"
    public async Task Ipn_missing_signature_returns_97()
    {
        using var db = NewDb();
        var result = await NewService(db).HandleIpnAsync(new Dictionary<string, string>(), default);

        Assert.Equal("97", result.RspCode);
    }
}
