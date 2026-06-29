using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Options;
using GymMaster.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace GymMaster.Api.Tests;

// Test luong thanh toan online VNPay (sandbox) bang EF Core InMemory.
public class VnPayServiceTests
{
    private const decimal Price = 500_000m;
    private const string Secret = "TEST_HASH_SECRET";

    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-vnpay-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static VnPayService NewService(GymMasterDbContext db)
    {
        var options = Options.Create(new VnPayOptions
        {
            TmnCode = "TESTCODE",
            HashSecret = Secret,
            ReturnUrl = "http://localhost/return"
        });
        return new VnPayService(db, new NoopAudit(), options);
    }

    private static ClaimsPrincipal Staff(long userId = 99)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Staff)
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private static async Task<long> SeedPendingMembershipAsync(GymMasterDbContext db)
    {
        var today = AppClock.Today();
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 1,
            Name = "Goi 1 thang",
            DurationDays = 30,
            Price = Price,
            IsActive = true
        });
        db.Memberships.Add(new Membership
        {
            Id = 1,
            MemberId = 1,
            PackageId = 1,
            StartDate = today,
            EndDate = today.AddDays(30),
            Status = MembershipStatus.PendingPayment,
            CreatedByUserId = 99,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return 1;
    }

    private static async Task<(long activeId, long pendingId, DateOnly oldEnd)> SeedActiveAndPendingRenewalAsync(GymMasterDbContext db)
    {
        var today = AppClock.Today();
        var oldEnd = today.AddDays(10);
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 1,
            Name = "Goi 1 thang",
            DurationDays = 30,
            Price = Price,
            IsActive = true
        });
        db.Memberships.AddRange(
            new Membership
            {
                Id = 1,
                MemberId = 1,
                PackageId = 1,
                StartDate = today.AddDays(-20),
                EndDate = oldEnd,
                Status = MembershipStatus.Active,
                CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new Membership
            {
                Id = 2,
                MemberId = 1,
                PackageId = 1,
                StartDate = today,
                EndDate = today.AddDays(30),
                Status = MembershipStatus.PendingPayment,
                CreatedByUserId = 10,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
        return (1, 2, oldEnd);
    }

    // Ky 1 bo tham so giong het cach VNPay ky (de gia lap callback hop le trong test).
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
    {
        return new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = "TESTCODE",
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
            ["vnp_TxnRef"] = paymentId.ToString(),
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00"
        };
    }

    private static Dictionary<string, string> Signed(Dictionary<string, string> p, string secret)
    {
        p["vnp_SecureHash"] = Sign(p, secret);
        return p;
    }

    private static string HmacSha512(string secret, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    [Fact] // Tao URL -> co BaseUrl + chu ky; tao payment Pending dung so tien goi.
    public async Task CreatePaymentUrl_returns_signed_url_and_pending_payment()
    {
        using var db = NewDb();
        await SeedPendingMembershipAsync(db);
        var service = NewService(db);

        var result = await service.CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Contains("sandbox.vnpayment.vn", result.Value!.PayUrl);
        Assert.Contains("vnp_SecureHash=", result.Value.PayUrl);
        Assert.Equal(Price, result.Value.Amount);

        var payment = await db.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(Price, payment.Amount);
    }

    [Fact] // IPN hop le + thanh cong -> membership Active, payment Paid, tra "00".
    public async Task Ipn_valid_success_activates_membership()
    {
        using var db = NewDb();
        await SeedPendingMembershipAsync(db);
        var service = NewService(db);
        var paymentId = (await service.CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Staff(), default)).Value!.PaymentId;

        var response = await service.HandleIpnAsync(Signed(SuccessParams(paymentId, Price), Secret), default);

        Assert.Equal("00", response.RspCode);
        Assert.Equal(MembershipStatus.Active, (await db.Memberships.SingleAsync()).Status);
        var payment = await db.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.NotNull(payment.PaidAt);
    }

    [Fact] // VNPay thanh cong khi da co Active -> noi han, kich hoat don moi, huy Active cu.
    public async Task Ipn_valid_success_with_existing_active_rolls_over_and_activates_pending()
    {
        using var db = NewDb();
        var (activeId, pendingId, oldEnd) = await SeedActiveAndPendingRenewalAsync(db);
        var service = NewService(db);
        var paymentId = (await service.CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(pendingId), "127.0.0.1", Staff(), default)).Value!.PaymentId;

        var response = await service.HandleIpnAsync(Signed(SuccessParams(paymentId, Price), Secret), default);

        Assert.Equal("00", response.RspCode);
        Assert.Equal(MembershipStatus.Cancelled, (await db.Memberships.SingleAsync(item => item.Id == activeId)).Status);
        var pending = await db.Memberships.SingleAsync(item => item.Id == pendingId);
        Assert.Equal(MembershipStatus.Active, pending.Status);
        Assert.Equal(oldEnd.AddDays(30), pending.EndDate);
        Assert.Equal(1, await db.Memberships.CountAsync(item => item.MemberId == 1 && item.Status == MembershipStatus.Active));
        Assert.Equal(PaymentStatus.Paid, (await db.Payments.SingleAsync()).Status);
    }

    [Fact] // TxnRef moi co timestamp van map ve dung Payment.Id.
    public async Task Ipn_valid_success_with_timestamped_txnref_activates_membership()
    {
        using var db = NewDb();
        await SeedPendingMembershipAsync(db);
        var service = NewService(db);
        var paymentId = (await service.CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Staff(), default)).Value!.PaymentId;

        var query = SuccessParams(paymentId, Price);
        query["vnp_TxnRef"] = $"GM{paymentId}T20260625160000000";

        var response = await service.HandleIpnAsync(Signed(query, Secret), default);

        Assert.Equal("00", response.RspCode);
        Assert.Equal(MembershipStatus.Active, (await db.Memberships.SingleAsync()).Status);
        Assert.Equal(PaymentStatus.Paid, (await db.Payments.SingleAsync()).Status);
    }

    [Fact] // IPN lan 2 -> idempotent, tra "02".
    public async Task Ipn_second_time_is_already_confirmed()
    {
        using var db = NewDb();
        await SeedPendingMembershipAsync(db);
        var service = NewService(db);
        var paymentId = (await service.CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Staff(), default)).Value!.PaymentId;
        await service.HandleIpnAsync(Signed(SuccessParams(paymentId, Price), Secret), default);

        var second = await service.HandleIpnAsync(Signed(SuccessParams(paymentId, Price), Secret), default);

        Assert.Equal("02", second.RspCode);
    }

    [Fact] // Sai chu ky -> "97", khong kich hoat.
    public async Task Ipn_invalid_signature_rejected()
    {
        using var db = NewDb();
        await SeedPendingMembershipAsync(db);
        var service = NewService(db);
        var paymentId = (await service.CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Staff(), default)).Value!.PaymentId;

        var query = SuccessParams(paymentId, Price);
        query["vnp_SecureHash"] = "deadbeef"; // chu ky gia

        var response = await service.HandleIpnAsync(query, default);

        Assert.Equal("97", response.RspCode);
        Assert.Equal(MembershipStatus.PendingPayment, (await db.Memberships.SingleAsync()).Status);
    }

    [Fact] // Chu ky dung nhung so tien lech gia -> "04".
    public async Task Ipn_amount_mismatch_rejected()
    {
        using var db = NewDb();
        await SeedPendingMembershipAsync(db);
        var service = NewService(db);
        var paymentId = (await service.CreatePaymentUrlAsync(
            new CreateVnPayPaymentRequest(1), "127.0.0.1", Staff(), default)).Value!.PaymentId;

        // So tien sai, nhung van ky lai dung => qua duoc verify chu ky, chi truot o buoc so tien.
        var query = Signed(SuccessParams(paymentId, 100_000m), Secret);

        var response = await service.HandleIpnAsync(query, default);

        Assert.Equal("04", response.RspCode);
        Assert.Equal(MembershipStatus.PendingPayment, (await db.Memberships.SingleAsync()).Status);
    }

    [Fact] // VnPayLibrary: chu ky dung -> valid; sua tham so -> invalid.
    public void Signature_roundtrip_detects_tampering()
    {
        var data = new Dictionary<string, string>
        {
            ["vnp_Amount"] = "50000000",
            ["vnp_TxnRef"] = "1",
            ["vnp_ResponseCode"] = "00"
        };
        var hash = Sign(data, Secret);

        var verifier = new VnPayLibrary();
        foreach (var (key, value) in data)
        {
            verifier.AddResponseData(key, value);
        }
        Assert.True(verifier.ValidateSignature(hash, Secret));

        var tampered = new VnPayLibrary();
        tampered.AddResponseData("vnp_Amount", "99999999"); // doi so tien
        tampered.AddResponseData("vnp_TxnRef", "1");
        tampered.AddResponseData("vnp_ResponseCode", "00");
        Assert.False(tampered.ValidateSignature(hash, Secret));
    }

    [Fact] // VNPAY PHP/Java demo ky tren query da sort va URL-encode.
    public void CreateRequestUrl_signs_encoded_sorted_data()
    {
        var library = new VnPayLibrary();
        library.AddRequestData("vnp_ReturnUrl", "http://localhost:3000/member/membership/vnpay-return");
        library.AddRequestData("vnp_OrderInfo", "Thanh toan goi tap membership 9");
        library.AddRequestData("vnp_TmnCode", "TESTCODE");

        var url = library.CreateRequestUrl("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", Secret);
        var hash = url[(url.IndexOf("vnp_SecureHash=", StringComparison.Ordinal) + "vnp_SecureHash=".Length)..];
        var encodedSignData =
            "vnp_OrderInfo=Thanh+toan+goi+tap+membership+9" +
            "&vnp_ReturnUrl=http%3A%2F%2Flocalhost%3A3000%2Fmember%2Fmembership%2Fvnpay-return" +
            "&vnp_TmnCode=TESTCODE";

        Assert.Contains("vnp_ReturnUrl=http%3A%2F%2Flocalhost%3A3000%2Fmember%2Fmembership%2Fvnpay-return", url);
        Assert.Equal(HmacSha512(Secret, encodedSignData), hash);
    }
}
