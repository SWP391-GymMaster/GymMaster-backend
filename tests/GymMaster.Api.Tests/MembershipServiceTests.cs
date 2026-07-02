using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Test logic 3 luong tien cua spec 003 bang EF Core InMemory (khong can DB that).
public class MembershipServiceTests
{
    private const decimal Price = 500_000m;
    private const short Days = 30;

    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static MembershipService NewService(GymMasterDbContext db) => new(db, new NoopAudit());

    private static ClaimsPrincipal Staff(long userId = 99)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test");
        return new ClaimsPrincipal(identity);
    }

    // Principal co role member (de test phan quyen huy - member chi huy cua minh).
    private static ClaimsPrincipal Member(long userId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Member),
            }, "test");
        return new ClaimsPrincipal(identity);
    }

    // Principal co role staff (huy bat ky).
    private static ClaimsPrincipal StaffRole(long userId = 99)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Staff),
            }, "test");
        return new ClaimsPrincipal(identity);
    }

    private static async Task<(long memberId, long packageId)> SeedAsync(GymMasterDbContext db)
    {
        db.Users.Add(new User
        {
            Id = 10,
            Email = "member@gymmaster.local",
            FullName = "GymMaster Member",
            PasswordHash = "hash",
            Status = UserStatuses.Active,
        });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 1,
            Name = "Goi 1 thang",
            DurationDays = Days,
            Price = Price,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return (1, 1);
    }

    private static async Task<long> AddPackageAsync(GymMasterDbContext db, long id, bool supportsPT)
    {
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = id,
            Name = supportsPT ? "Goi PT" : "Goi thuong",
            DurationDays = Days,
            Price = Price,
            IsActive = true,
            SupportsPT = supportsPT,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    [Fact] // FR-MS-01: ban goi -> PendingPayment, EndDate = StartDate + DurationDays
    public async Task Sell_creates_pending_membership_with_correct_end_date()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var start = AppClock.Today();

        var result = await NewService(db).SellAsync(
            new SellMembershipRequest(memberId, packageId, start), Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("PendingPayment", result.Value!.Membership.Status);
        Assert.Equal(start.AddDays(Days), result.Value.Membership.EndDate);
    }

    [Fact] // §7: ban goi voi ngay bat dau qua khu -> INVALID_START_DATE
    public async Task Sell_with_past_start_date_is_rejected()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var past = AppClock.Today().AddDays(-1);

        var result = await NewService(db).SellAsync(
            new SellMembershipRequest(memberId, packageId, past), Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_START_DATE", result.ErrorCode);
    }

    [Fact] // FR-MS-02: ghi thanh toan -> Active + luu DUNG phuong thuc (khong hardcode Cash)
    public async Task ConfirmPayment_activates_and_keeps_request_method()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var sold = await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default);

        var result = await service.ConfirmPaymentAsync(
            sold.Value!.Membership.Id, new ConfirmPaymentRequest(Price, "transfer"), Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Active", result.Value!.Status);
        Assert.Equal(PaymentMethod.Transfer, (await db.Payments.SingleAsync()).PaymentMethod);
    }

    [Fact] // §7: tra it hon gia goi -> INSUFFICIENT_AMOUNT
    public async Task ConfirmPayment_with_insufficient_amount_is_rejected()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var sold = await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default);

        var result = await service.ConfirmPaymentAsync(
            sold.Value!.Membership.Id, new ConfirmPaymentRequest(100_000m, "cash"), Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("INSUFFICIENT_AMOUNT", result.ErrorCode);
    }

    [Fact] // FR-PAY-01: tra tien lan 2 cho cung membership -> DUPLICATE_PAYMENT (409)
    public async Task ConfirmPayment_twice_is_rejected_as_duplicate()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var sold = await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default);
        var membershipId = sold.Value!.Membership.Id;
        await service.ConfirmPaymentAsync(membershipId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        var second = await service.ConfirmPaymentAsync(
            membershipId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        Assert.False(second.Succeeded);
        Assert.Equal("DUPLICATE_PAYMENT", second.ErrorCode);
    }

    [Fact] // FR-MS-03: gia han noi tiep EndDate + ghi dung method.
    public async Task Renew_extends_end_date_and_records_request_method()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var membershipId = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default))
            .Value!.Membership.Id;
        await service.ConfirmPaymentAsync(membershipId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);
        var endBefore = (await db.Memberships.SingleAsync(x => x.Id == membershipId)).EndDate;

        var result = await service.RenewAsync(
            membershipId, new RenewMembershipRequest(packageId, "card"), Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Active", result.Value!.Status);
        Assert.Equal(endBefore.AddDays(Days), result.Value.EndDate);
        Assert.Equal(PaymentMethod.Card, (await db.Payments.OrderBy(p => p.Id).LastAsync()).PaymentMethod);
    }

    [Fact] // Gia han goi da Cancelled -> tu choi.
    public async Task Renew_cancelled_membership_is_rejected()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var membershipId = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default))
            .Value!.Membership.Id;

        var membership = await db.Memberships.SingleAsync(x => x.Id == membershipId);
        membership.Status = MembershipStatus.Cancelled;
        await db.SaveChangesAsync();

        var result = await service.RenewAsync(
            membershipId, new RenewMembershipRequest(packageId, "cash"), Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("MEMBERSHIP_CANCELLED", result.ErrorCode);
    }

    [Fact] // GET /memberships tra ve PagedResult { items, totalItems, pageSize 20 }
    public async Task GetAll_returns_paged_result()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var today = AppClock.Today();
        for (var i = 0; i < 3; i++)
        {
            db.Memberships.Add(new Membership
            {
                MemberId = memberId,
                PackageId = packageId,
                StartDate = today,
                EndDate = today.AddDays(Days),
                Status = MembershipStatus.Cancelled,
                CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow.AddMinutes(i),
            });
        }
        await db.SaveChangesAsync();

        var result = await service.GetAllAsync(null, null, Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value!.Total);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(3, result.Value.Items.Count);
    }

    private async Task<long> SeedPendingMembershipAsync(GymMasterDbContext db, MembershipService service)
    {
        var (memberId, packageId) = await SeedAsync(db);
        return (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default))
            .Value!.Membership.Id;
    }

    [Fact] // FR-MS-08: member huy don Pending cua CHINH MINH -> Cancelled
    public async Task Cancel_pending_by_owner_member_succeeds()
    {
        using var db = NewDb();
        var service = NewService(db);
        var membershipId = await SeedPendingMembershipAsync(db, service);

        var result = await service.CancelAsync(membershipId, Member(10), default); // UserId chu so huu = 10

        Assert.True(result.Succeeded);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(MembershipStatus.Cancelled, (await db.Memberships.SingleAsync(x => x.Id == membershipId)).Status);
    }

    [Fact] // FR-MS-08: member huy goi Active cua chinh minh (vd doi sang goi PT) - khong hoan tien
    public async Task Cancel_active_by_owner_member_succeeds()
    {
        using var db = NewDb();
        var service = NewService(db);
        var membershipId = await SeedPendingMembershipAsync(db, service);
        await service.ConfirmPaymentAsync(membershipId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        var result = await service.CancelAsync(membershipId, Member(10), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Cancelled", result.Value!.Status);
    }

    [Fact] // FR-MS-08: member KHONG huy duoc membership cua nguoi khac -> FORBIDDEN
    public async Task Cancel_by_non_owner_member_is_forbidden()
    {
        using var db = NewDb();
        var service = NewService(db);
        var membershipId = await SeedPendingMembershipAsync(db, service);

        var result = await service.CancelAsync(membershipId, Member(999), default); // UserId khac chu so huu (10)

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // FR-MS-08: Staff huy duoc membership bat ky (goi Active)
    public async Task Cancel_active_by_staff_succeeds()
    {
        using var db = NewDb();
        var service = NewService(db);
        var membershipId = await SeedPendingMembershipAsync(db, service);
        await service.ConfirmPaymentAsync(membershipId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        var result = await service.CancelAsync(membershipId, StaffRole(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Cancelled", result.Value!.Status);
    }

    [Fact] // FR-MS-08: khong huy lai goi da Cancelled -> CANNOT_CANCEL
    public async Task Cancel_already_cancelled_is_rejected()
    {
        using var db = NewDb();
        var service = NewService(db);
        var membershipId = await SeedPendingMembershipAsync(db, service);
        await service.CancelAsync(membershipId, StaffRole(), default);

        var second = await service.CancelAsync(membershipId, StaffRole(), default);

        Assert.False(second.Succeeded);
        Assert.Equal("CANNOT_CANCEL", second.ErrorCode);
    }

    [Fact] // Gap E: thanh toan cho don da Cancelled -> MEMBERSHIP_CANCELLED (khong phai DUPLICATE_PAYMENT)
    public async Task ConfirmPayment_on_cancelled_returns_membership_cancelled()
    {
        using var db = NewDb();
        var service = NewService(db);
        var membershipId = await SeedPendingMembershipAsync(db, service);
        await service.CancelAsync(membershipId, StaffRole(), default);

        var result = await service.ConfirmPaymentAsync(
            membershipId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("MEMBERSHIP_CANCELLED", result.ErrorCode);
    }

    [Fact] // Chong thu 2 lan: da co payment Pending (vd VNPay khoi tao) -> staff confirm TAI DUNG dong do, khong de moi
    public async Task ConfirmPayment_reuses_existing_pending_payment_no_duplicate_row()
    {
        using var db = NewDb();
        var service = NewService(db);
        var membershipId = await SeedPendingMembershipAsync(db, service);

        // Gia lap VNPay da tao 1 payment Pending (method Transfer) cho don nay.
        db.Payments.Add(new Payment
        {
            MembershipId = membershipId,
            Amount = Price,
            PaymentMethod = PaymentMethod.Transfer,
            Status = PaymentStatus.Pending,
            CreatedByUserId = 10,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Staff thu tay (cash) xen vao luong online.
        var result = await service.ConfirmPaymentAsync(
            membershipId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Active", result.Value!.Status);

        // Chi 1 dong Payment cho don (tai dung, khong de them) va da Paid voi method thu tay.
        var payment = await db.Payments.SingleAsync(p => p.MembershipId == membershipId);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(PaymentMethod.Cash, payment.PaymentMethod);
    }

    [Fact] // Gia han som: thanh toan don Pending khi dang co Active -> noi han va huy Active cu.
    public async Task ConfirmPayment_with_existing_active_rolls_over_end_date_and_cancels_old_active()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var today = AppClock.Today();
        var activeId = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, today), Staff(), default)).Value!.Membership.Id;
        await service.ConfirmPaymentAsync(activeId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);
        var oldEnd = (await db.Memberships.SingleAsync(item => item.Id == activeId)).EndDate;

        var renewal = await service.CreateRenewalRequestAsync(new RenewalRequestRequest(packageId), Member(10), default);
        var result = await service.ConfirmPaymentAsync(
            renewal.Value!.Id, new ConfirmPaymentRequest(Price, "transfer"), Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(oldEnd.AddDays(Days), result.Value!.Membership.EndDate);
        Assert.Equal(MembershipStatus.Cancelled, (await db.Memberships.SingleAsync(item => item.Id == activeId)).Status);
        Assert.Equal(MembershipStatus.Active, (await db.Memberships.SingleAsync(item => item.Id == renewal.Value.Id)).Status);
        Assert.Equal(1, await db.Memberships.CountAsync(
            item => item.MemberId == memberId && item.Status == MembershipStatus.Active));
    }

    [Fact] // Member duoc tao request gia han som khi goi moi cung loai PT voi goi Active.
    public async Task RenewalRequest_allows_active_membership_when_package_pt_type_matches()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var activeId = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default)).Value!.Membership.Id;
        await service.ConfirmPaymentAsync(activeId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        var result = await service.CreateRenewalRequestAsync(new RenewalRequestRequest(packageId), Member(10), default);

        Assert.True(result.Succeeded);
        Assert.Equal("PendingPayment", result.Value!.Status);
        Assert.Equal(1, await db.Memberships.CountAsync(item => item.Status == MembershipStatus.Active));
        Assert.Equal(1, await db.Memberships.CountAsync(item => item.Status == MembershipStatus.PendingPayment));
    }

    [Fact] // Member khong duoc gia han som sang goi khac loai PT.
    public async Task RenewalRequest_blocks_active_membership_when_package_pt_type_mismatches()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var ptPackageId = await AddPackageAsync(db, 2, supportsPT: true);
        var service = NewService(db);
        var activeId = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default)).Value!.Membership.Id;
        await service.ConfirmPaymentAsync(activeId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        var result = await service.CreateRenewalRequestAsync(new RenewalRequestRequest(ptPackageId), Member(10), default);

        Assert.False(result.Succeeded);
        Assert.Equal("PACKAGE_PT_MISMATCH", result.ErrorCode);
    }

    [Fact] // Staff RenewAsync goi con Active cung bi chan neu doi loai PT.
    public async Task Renew_blocks_package_pt_type_mismatch_while_membership_is_active()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var ptPackageId = await AddPackageAsync(db, 2, supportsPT: true);
        var service = NewService(db);
        var activeId = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default)).Value!.Membership.Id;
        await service.ConfirmPaymentAsync(activeId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        var result = await service.RenewAsync(
            activeId, new RenewMembershipRequest(ptPackageId, "cash"), Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("PACKAGE_PT_MISMATCH", result.ErrorCode);
    }

    [Fact] // Renewal-request lan 2 khi dang co Pending -> tra lai don cu, khong tao them.
    public async Task RenewalRequest_twice_with_pending_returns_existing_request()
    {
        using var db = NewDb();
        var (_, packageId) = await SeedAsync(db);
        var service = NewService(db);

        var first = await service.CreateRenewalRequestAsync(new RenewalRequestRequest(packageId), Member(10), default);
        var second = await service.CreateRenewalRequestAsync(new RenewalRequestRequest(packageId), Member(10), default);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal(1, await db.Memberships.CountAsync(m => m.Status == MembershipStatus.PendingPayment));
    }

    [Fact] // Sell khi member da co Active -> ALREADY_HAS_ACTIVE.
    public async Task Sell_when_member_already_has_active_is_rejected()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var today = AppClock.Today();

        // Goi A: ban + thanh toan -> Active
        var a = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, today), Staff(), default)).Value!.Membership.Id;
        await service.ConfirmPaymentAsync(a, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        var result = await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, today), Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("ALREADY_HAS_ACTIVE", result.ErrorCode);
        Assert.Equal(1, await db.Memberships.CountAsync(
            m => m.MemberId == memberId && m.Status == MembershipStatus.Active));
    }

    [Fact] // ConfirmPayment 1 don -> cac don Pending anh em cua member chuyen Cancelled.
    public async Task ConfirmPayment_cancels_sibling_pending_memberships()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var today = AppClock.Today();
        var primaryId = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, today), Staff(), default)).Value!.Membership.Id;

        db.Memberships.AddRange(
            new Membership
            {
                MemberId = memberId,
                PackageId = packageId,
                StartDate = today,
                EndDate = today.AddDays(Days),
                Status = MembershipStatus.PendingPayment,
                CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow,
            },
            new Membership
            {
                MemberId = memberId,
                PackageId = packageId,
                StartDate = today,
                EndDate = today.AddDays(Days),
                Status = MembershipStatus.PendingPayment,
                CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var result = await service.ConfirmPaymentAsync(primaryId, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await db.Memberships.CountAsync(
            m => m.MemberId == memberId && m.Status == MembershipStatus.Active));
        Assert.Equal(2, await db.Memberships.CountAsync(
            m => m.MemberId == memberId && m.Status == MembershipStatus.Cancelled));
    }
}
