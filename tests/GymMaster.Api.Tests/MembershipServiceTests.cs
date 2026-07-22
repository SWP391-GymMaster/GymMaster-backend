using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;
using GymMaster.API.Features.Billing;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Common;

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

    // =====================================================================
    // Duong DOC: GET /members/{id}/memberships (FR-MS-09) va GET /memberships (roster).
    // Diem mau chot: doc cung LA luc he thong dong bo trang thai (lazy expire) —
    // he thong KHONG co cron/timer nao chay nen, moi thu tinh tai thoi diem truy van.
    // =====================================================================

    // Them mot hoi vien thu hai de kiem tra quyen "chi xem cua minh".
    private static async Task<long> AddSecondMemberAsync(GymMasterDbContext db, long userId, long memberId)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"member{memberId}@gymmaster.local",
            FullName = "Hoi vien khac",
            PasswordHash = "hash",
            Status = UserStatuses.Active,
        });
        db.MemberProfiles.Add(new MemberProfile { Id = memberId, UserId = userId, IsDeleted = false });
        await db.SaveChangesAsync();
        return memberId;
    }

    [Fact] // FR-MS-09: Given memberId khong ton tai, When xem lich su goi, Then 404 NOT_FOUND
    public async Task GetMembershipsForMember_with_unknown_member_returns_404()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).GetMembershipsForMemberAsync(999, StaffRole(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact] // FR-MS-09: Given Member A dang nhap, When xem lich su goi cua Member B, Then 403 FORBIDDEN
    public async Task GetMembershipsForMember_blocks_member_reading_someone_else()
    {
        using var db = NewDb();
        await SeedAsync(db);                                  // member 1 <- user 10
        await AddSecondMemberAsync(db, userId: 20, memberId: 2);

        var result = await NewService(db).GetMembershipsForMemberAsync(2, Member(userId: 10), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact] // FR-MS-09: Given hoi vien co nhieu goi, When xem lich su, Then sap moi -> cu
    public async Task GetMembershipsForMember_returns_newest_first()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var today = AppClock.Today();
        db.Memberships.AddRange(
            new Membership
            {
                Id = 1, MemberId = memberId, PackageId = packageId,
                StartDate = today, EndDate = today.AddDays(Days),
                Status = MembershipStatus.Cancelled, CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            },
            new Membership
            {
                Id = 2, MemberId = memberId, PackageId = packageId,
                StartDate = today, EndDate = today.AddDays(Days),
                Status = MembershipStatus.Active, CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
            });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetMembershipsForMemberAsync(memberId, StaffRole(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(new long[] { 2, 1 }, result.Value!.Select(item => item.Id).ToArray());
    }

    [Fact] // FR-MS-07(b): Given goi Active da qua EndDate, When truy van, Then tu chuyen Expired VA ghi lai vao DB
    public async Task GetMembershipsForMember_lazily_expires_past_due_membership()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var today = AppClock.Today();
        db.Memberships.Add(new Membership
        {
            Id = 1, MemberId = memberId, PackageId = packageId,
            StartDate = today.AddDays(-40), EndDate = today.AddDays(-1),  // het han tu hom qua
            Status = MembershipStatus.Active, CreatedByUserId = 99,
            CreatedAt = DateTime.UtcNow.AddDays(-40),
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetMembershipsForMemberAsync(memberId, StaffRole(), default);

        Assert.Equal("Expired", Assert.Single(result.Value!).Status);
        // Khong chi doi o ban tra ve: trang thai duoc GHI xuong DB nen lan doc sau khong phai tinh lai.
        var stored = await db.Memberships.AsNoTracking().SingleAsync(item => item.Id == 1);
        Assert.Equal(MembershipStatus.Expired, stored.Status);
    }

    [Fact] // FR-MS-07(a) / AC-09: Given don PendingPayment tao qua 30 phut, When truy van, Then tu chuyen Cancelled
    public async Task GetMembershipsForMember_lazily_cancels_stale_pending_order()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var today = AppClock.Today();
        db.Memberships.Add(new Membership
        {
            Id = 1, MemberId = memberId, PackageId = packageId,
            StartDate = today, EndDate = today.AddDays(Days),
            Status = MembershipStatus.PendingPayment, CreatedByUserId = 99,
            CreatedAt = DateTime.UtcNow.AddMinutes(-31),   // qua TTL 30 phut
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetMembershipsForMemberAsync(memberId, StaffRole(), default);

        // Don treo qua han -> Cancelled (KHONG phai Expired; Expired chi danh cho goi da tung Active).
        Assert.Equal("Cancelled", Assert.Single(result.Value!).Status);
    }

    [Fact] // FR-MS-07(a): Given don PendingPayment moi tao 5 phut, When truy van, Then VAN cho thanh toan
    public async Task GetMembershipsForMember_keeps_pending_order_inside_the_30_minute_window()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var today = AppClock.Today();
        db.Memberships.Add(new Membership
        {
            Id = 1, MemberId = memberId, PackageId = packageId,
            StartDate = today, EndDate = today.AddDays(Days),
            Status = MembershipStatus.PendingPayment, CreatedByUserId = 99,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetMembershipsForMemberAsync(memberId, StaffRole(), default);

        // Moc 30 phut nay chinh la moc ma man hinh FE phai hien thi giong het (PAYMENT_WINDOW_MS).
        Assert.Equal("PendingPayment", Assert.Single(result.Value!).Status);
    }

    [Fact] // §6.1: Given goi Active con 5 ngay, When xem, Then daysRemaining=5 va isExpiringSoon=true (canh bao 0..7 ngay)
    public async Task GetMembershipsForMember_flags_membership_expiring_within_seven_days()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var today = AppClock.Today();
        db.Memberships.Add(new Membership
        {
            Id = 1, MemberId = memberId, PackageId = packageId,
            StartDate = today.AddDays(-25), EndDate = today.AddDays(5),
            Status = MembershipStatus.Active, CreatedByUserId = 99,
            CreatedAt = DateTime.UtcNow.AddDays(-25),
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetMembershipsForMemberAsync(memberId, StaffRole(), default);

        var item = Assert.Single(result.Value!);
        Assert.Equal(5, item.DaysRemaining);
        Assert.True(item.IsExpiringSoon);
    }

    [Fact] // §6: Given status khong thuoc enum, When xem roster, Then 422 VALIDATION_ERROR
    public async Task GetAll_with_unknown_status_returns_422()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).GetAllAsync("khong-ton-tai", 1, StaffRole(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    [Fact] // §6: Given roster co nhieu trang thai, When loc status=active, Then chi tra goi Active
    public async Task GetAll_filters_roster_by_status()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var today = AppClock.Today();
        db.Memberships.AddRange(
            new Membership
            {
                Id = 1, MemberId = memberId, PackageId = packageId,
                StartDate = today, EndDate = today.AddDays(Days),
                Status = MembershipStatus.Active, CreatedByUserId = 99, CreatedAt = DateTime.UtcNow,
            },
            new Membership
            {
                Id = 2, MemberId = memberId, PackageId = packageId,
                StartDate = today, EndDate = today.AddDays(Days),
                Status = MembershipStatus.Cancelled, CreatedByUserId = 99, CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetAllAsync("active", null, StaffRole(), default);

        Assert.True(result.Succeeded);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("Active", item.Status);
    }

    [Fact] // §6: Given 25 hop dong, When xem roster, Then moi trang 20 dong va co 2 trang
    public async Task GetAll_pages_roster_by_twenty()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var today = AppClock.Today();
        for (var i = 1; i <= 25; i++)
        {
            db.Memberships.Add(new Membership
            {
                Id = i, MemberId = memberId, PackageId = packageId,
                StartDate = today, EndDate = today.AddDays(Days),
                Status = MembershipStatus.Cancelled, CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
            });
        }
        await db.SaveChangesAsync();
        var service = NewService(db);

        var first = await service.GetAllAsync(null, 1, StaffRole(), default);
        var second = await service.GetAllAsync(null, 2, StaffRole(), default);

        Assert.Equal(25, first.Value!.Total);
        Assert.Equal(20, first.Value.PageSize);
        Assert.Equal(2, first.Value.TotalPages);
        Assert.Equal(20, first.Value.Items.Count);
        Assert.Equal(5, second.Value!.Items.Count);
    }

    [Fact] // §6: Given page null hoac <= 0, When xem roster, Then coi nhu trang 1 (khong vo, khong tra rong)
    public async Task GetAll_normalises_invalid_page_number()
    {
        using var db = NewDb();
        await SeedAsync(db);
        var service = NewService(db);

        var nullPage = await service.GetAllAsync(null, null, StaffRole(), default);
        var zeroPage = await service.GetAllAsync(null, 0, StaffRole(), default);

        Assert.Equal(1, nullPage.Value!.Page);
        Assert.Equal(1, zeroPage.Value!.Page);
    }

    // =====================================================================
    // Cac chot chan (guard) con lai cua 5 endpoint ghi.
    // Nhom nay kiem BAC THANG MA LOI: 403 khong xac dinh duoc nguoi thao tac ->
    // 404 khong ton tai -> 422/409 vi pham nghiep vu. Moi endpoint deu di dung
    // thu tu do, nen doc test theo hang la thay ngay tinh nhat quan cua API.
    // =====================================================================

    // Token khong mang claim dinh danh -> service khong biet AI dang thao tac.
    private static ClaimsPrincipal NoActor() => new(new ClaimsIdentity());

    [Fact] // §7: Given token khong co claim dinh danh, When ban goi, Then 403 FORBIDDEN
    public async Task Sell_without_actor_identity_returns_403()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);

        var result = await NewService(db).SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), NoActor(), default);

        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact] // §7: Given memberId khong ton tai, When ban goi, Then 404 NOT_FOUND
    public async Task Sell_to_unknown_member_returns_404()
    {
        using var db = NewDb();
        var (_, packageId) = await SeedAsync(db);

        var result = await NewService(db).SellAsync(
            new SellMembershipRequest(999, packageId, AppClock.Today()), Staff(), default);

        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact] // §7: Given packageId khong ton tai, When ban goi, Then 404 NOT_FOUND
    public async Task Sell_with_unknown_package_returns_404()
    {
        using var db = NewDb();
        var (memberId, _) = await SeedAsync(db);

        var result = await NewService(db).SellAsync(
            new SellMembershipRequest(memberId, 999, AppClock.Today()), Staff(), default);

        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact] // FR-PKG-02 / §7: Given goi da ngung ban, When ban goi, Then 422 PACKAGE_INACTIVE
    public async Task Sell_with_inactive_package_returns_422()
    {
        using var db = NewDb();
        var (memberId, _) = await SeedAsync(db);
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 5, Name = "Goi ngung ban", DurationDays = Days, Price = Price, IsActive = false,
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).SellAsync(
            new SellMembershipRequest(memberId, 5, AppClock.Today()), Staff(), default);

        // Goi bi Admin tat qua MembershipPackageService.UpdateAsync(IsActive: false) thi den day bi chan.
        Assert.Equal("PACKAGE_INACTIVE", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    [Fact] // §7: Given token khong co claim dinh danh, When ghi thanh toan, Then 403 FORBIDDEN
    public async Task ConfirmPayment_without_actor_identity_returns_403()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).ConfirmPaymentAsync(
            1, new ConfirmPaymentRequest(Price, "cash"), NoActor(), default);

        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact] // §7: Given membership khong ton tai, When ghi thanh toan, Then 404 NOT_FOUND
    public async Task ConfirmPayment_for_unknown_membership_returns_404()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).ConfirmPaymentAsync(
            999, new ConfirmPaymentRequest(Price, "cash"), Staff(), default);

        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Theory] // §7: Given phuong thuc la va / so tien <= 0, When ghi thanh toan, Then 422 VALIDATION_ERROR
    [InlineData("bitcoin", 500_000)]  // phuong thuc khong thuoc {cash, transfer, card}
    [InlineData("cash", 0)]           // so tien phai duong
    [InlineData("cash", -1)]
    public async Task ConfirmPayment_with_invalid_method_or_amount_returns_422(string method, int amount)
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var pendingId = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default)).Value!.Membership.Id;

        var result = await service.ConfirmPaymentAsync(
            pendingId, new ConfirmPaymentRequest(amount, method), Staff(), default);

        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        // Don van con o trang thai cho thanh toan, khong bi day sang Active.
        Assert.Equal(MembershipStatus.PendingPayment, (await db.Memberships.FindAsync(pendingId))!.Status);
    }

    [Fact] // §7: Given token khong co claim dinh danh, When gia han, Then 403 FORBIDDEN
    public async Task Renew_without_actor_identity_returns_403()
    {
        using var db = NewDb();
        var (_, packageId) = await SeedAsync(db);

        var result = await NewService(db).RenewAsync(
            1, new RenewMembershipRequest(packageId, "cash"), NoActor(), default);

        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // §7: Given membership khong ton tai, When gia han, Then 404 NOT_FOUND
    public async Task Renew_unknown_membership_returns_404()
    {
        using var db = NewDb();
        var (_, packageId) = await SeedAsync(db);

        var result = await NewService(db).RenewAsync(
            999, new RenewMembershipRequest(packageId, "cash"), Staff(), default);

        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact] // §7: Given goi gia han khong ton tai, When gia han, Then 404 NOT_FOUND
    public async Task Renew_with_unknown_package_returns_404()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var id = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default)).Value!.Membership.Id;

        var result = await service.RenewAsync(id, new RenewMembershipRequest(999, "cash"), Staff(), default);

        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact] // FR-PKG-02 / §7: Given goi gia han da ngung ban, When gia han, Then 422 PACKAGE_INACTIVE
    public async Task Renew_with_inactive_package_returns_422()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 5, Name = "Goi ngung ban", DurationDays = Days, Price = Price, IsActive = false,
        });
        await db.SaveChangesAsync();
        var service = NewService(db);
        var id = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default)).Value!.Membership.Id;

        var result = await service.RenewAsync(id, new RenewMembershipRequest(5, "cash"), Staff(), default);

        Assert.Equal("PACKAGE_INACTIVE", result.ErrorCode);
    }

    [Fact] // §7: Given phuong thuc thanh toan la, When gia han, Then 422 VALIDATION_ERROR
    public async Task Renew_with_invalid_payment_method_returns_422()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var service = NewService(db);
        var id = (await service.SellAsync(
            new SellMembershipRequest(memberId, packageId, AppClock.Today()), Staff(), default)).Value!.Membership.Id;

        var result = await service.RenewAsync(id, new RenewMembershipRequest(packageId, "momo"), Staff(), default);

        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    [Fact] // §7: Given token khong co claim dinh danh, When gui yeu cau gia han, Then 403 FORBIDDEN
    public async Task RenewalRequest_without_actor_identity_returns_403()
    {
        using var db = NewDb();
        var (_, packageId) = await SeedAsync(db);

        var result = await NewService(db).CreateRenewalRequestAsync(
            new RenewalRequestRequest(packageId), NoActor(), default);

        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // §7: Given tai khoan chua co ho so hoi vien, When gui yeu cau gia han, Then loi tu tang Member duoc chuyen tiep nguyen ven
    public async Task RenewalRequest_propagates_failure_from_member_profile_lookup()
    {
        using var db = NewDb();
        var (_, packageId) = await SeedAsync(db);

        // User 999 khong ton tai trong bang users -> MemberService khong tao noi ho so.
        var result = await NewService(db).CreateRenewalRequestAsync(
            new RenewalRequestRequest(packageId), Member(userId: 999), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact] // §7: Given goi khong ton tai, When gui yeu cau gia han, Then 404 NOT_FOUND
    public async Task RenewalRequest_with_unknown_package_returns_404()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).CreateRenewalRequestAsync(
            new RenewalRequestRequest(999), Member(10), default);

        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact] // FR-PKG-02 / §7: Given goi da ngung ban, When gui yeu cau gia han, Then 422 PACKAGE_INACTIVE
    public async Task RenewalRequest_with_inactive_package_returns_422()
    {
        using var db = NewDb();
        await SeedAsync(db);
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 5, Name = "Goi ngung ban", DurationDays = Days, Price = Price, IsActive = false,
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateRenewalRequestAsync(
            new RenewalRequestRequest(5), Member(10), default);

        Assert.Equal("PACKAGE_INACTIVE", result.ErrorCode);
    }

    [Fact] // §7: Given token khong co claim dinh danh, When huy, Then 403 FORBIDDEN
    public async Task Cancel_without_actor_identity_returns_403()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).CancelAsync(1, NoActor(), default);

        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // §7: Given membership khong ton tai, When huy, Then 404 NOT_FOUND
    public async Task Cancel_unknown_membership_returns_404()
    {
        using var db = NewDb();
        await SeedAsync(db);

        var result = await NewService(db).CancelAsync(999, StaffRole(), default);

        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact] // FR-MS-07: Given roster co goi qua han va don treo, When Staff mo danh sach, Then trang thai duoc dong bo TRUOC khi tra ve
    public async Task GetAll_syncs_stale_statuses_before_listing()
    {
        using var db = NewDb();
        var (memberId, packageId) = await SeedAsync(db);
        var today = AppClock.Today();
        db.Memberships.AddRange(
            new Membership
            {
                Id = 1, MemberId = memberId, PackageId = packageId,
                StartDate = today.AddDays(-40), EndDate = today.AddDays(-1),
                Status = MembershipStatus.Active, CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow.AddDays(-40),
            },
            new Membership
            {
                Id = 2, MemberId = memberId, PackageId = packageId,
                StartDate = today, EndDate = today.AddDays(Days),
                Status = MembershipStatus.PendingPayment, CreatedByUserId = 99,
                CreatedAt = DateTime.UtcNow.AddMinutes(-31),
            });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetAllAsync(null, 1, StaffRole(), default);

        Assert.True(result.Succeeded);
        // Man hinh roster khong bao gio hien trang thai cu: goi qua han -> Expired, don treo -> Cancelled.
        var byId = result.Value!.Items.ToDictionary(item => item.Id, item => item.Status);
        Assert.Equal("Expired", byId[1]);
        Assert.Equal("Cancelled", byId[2]);
    }
}
