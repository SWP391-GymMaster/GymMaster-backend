using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Billing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Test tang Service cho PaymentService (spec 003 muc 6: /payments, /payments/summary,
// /members/{id}/payments). Day la duong DOC tien - khong ghi gi vao DB, nen moi khang dinh
// deu ve: LOC dung, PHAN TRANG dung, SAP XEP dung, PHAN QUYEN dung, GOM NGAY theo gio VN.
//
// Cong cu: xUnit + EF Core InMemory, moi test mot DB rieng theo Guid.
//
// Vi sao phai seed du 5 bang cho MOI test: BuildPaymentQuery join
// payments -> memberships -> member_profiles -> users -> membership_packages
// (4 join INNER + 1 join LEFT sang users de lay ten nguoi thu tien). Thieu bat ky
// bang nao o nhanh INNER thi dong payment do BIEN MAT khoi ket qua.
public class PaymentServiceTests
{
    private const decimal Price = 500_000m;

    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-pay-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Principal(long userId, string? role)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    // Tao 1 hoi vien (user + profile) + 1 goi + 1 membership. Tra ve memberId de gan payment.
    private static async Task<long> SeedMemberAsync(
        GymMasterDbContext db,
        long userId,
        long memberId,
        long membershipId,
        string fullName = "Nguyen Van A",
        bool userDeleted = false,
        bool profileDeleted = false)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"member{memberId}@gymmaster.local",
            FullName = fullName,
            PasswordHash = "hash",
            Status = UserStatuses.Active,
            IsDeleted = userDeleted,
        });
        db.MemberProfiles.Add(new MemberProfile { Id = memberId, UserId = userId, IsDeleted = profileDeleted });

        if (!await db.MembershipPackages.AnyAsync(item => item.Id == 1))
        {
            db.MembershipPackages.Add(new MembershipPackage
            {
                Id = 1,
                Name = "Goi 1 thang",
                DurationDays = 30,
                Price = Price,
                IsActive = true,
            });
        }

        db.Memberships.Add(new Membership
        {
            Id = membershipId,
            MemberId = memberId,
            PackageId = 1,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 7, 1),
            Status = MembershipStatus.Active,
            CreatedByUserId = 99,
        });

        await db.SaveChangesAsync();
        return memberId;
    }

    private static async Task<Payment> AddPaymentAsync(
        GymMasterDbContext db,
        long id,
        long membershipId,
        PaymentStatus status = PaymentStatus.Paid,
        PaymentMethod method = PaymentMethod.Cash,
        decimal amount = Price,
        DateTime? paidAt = null,
        DateTime? createdAt = null,
        long createdByUserId = 99)
    {
        var payment = new Payment
        {
            Id = id,
            MembershipId = membershipId,
            Amount = amount,
            PaymentMethod = method,
            Status = status,
            PaidAt = paidAt,
            CreatedAt = createdAt ?? new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = createdByUserId,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    // Nhan vien thu tien (nguoi xuat hien o cot createdByName).
    private static async Task AddStaffUserAsync(GymMasterDbContext db, long userId, string fullName)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"staff{userId}@gymmaster.local",
            FullName = fullName,
            PasswordHash = "hash",
            Status = UserStatuses.Active,
        });
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------------
    // GetAllAsync — noi dung dong tra ve
    // ---------------------------------------------------------------------

    [Fact] // §6: Given 1 payment day du quan he, When Staff xem lich su, Then dong tra ve co ten hoi vien, ten goi, ten nguoi thu
    public async Task GetAll_returns_row_joined_from_five_tables()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100, fullName: "Nguyen Van A");
        await AddStaffUserAsync(db, userId: 99, fullName: "Le Thi Thu Ngan");
        await AddPaymentAsync(db, id: 1, membershipId: 100, paidAt: new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));

        var result = await new PaymentService(db).GetAllAsync(null, null, null, null, 1, 50, default);

        Assert.True(result.Succeeded);
        var row = Assert.Single(result.Value!.Items);
        Assert.Equal("Nguyen Van A", row.MemberName);
        Assert.Equal("member1@gymmaster.local", row.MemberEmail);
        Assert.Equal("Goi 1 thang", row.PackageName);
        Assert.Equal("Le Thi Thu Ngan", row.CreatedByName);
        Assert.Equal("Cash", row.PaymentMethod);   // enum -> chuoi PascalCase theo response contract §6.1
        Assert.Equal("Paid", row.Status);
        Assert.Equal("Active", row.MembershipStatus);
        Assert.Equal(Price, row.Amount);
    }

    [Fact] // §6: Given nguoi thu tien khong con trong bang users, When xem lich su, Then dong VAN hien, createdByName = null
    public async Task GetAll_keeps_row_when_creator_user_is_missing()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        // Khong tao user 777 -> nhanh join sang nguoi thu tien khong khop.
        await AddPaymentAsync(db, id: 1, membershipId: 100, createdByUserId: 777);

        var result = await new PaymentService(db).GetAllAsync(null, null, null, null, 1, 50, default);

        // Day la ly do BuildPaymentQuery dung LEFT JOIN (join ... into creators + DefaultIfEmpty).
        // Neu dung INNER JOIN thi mat luon giao dich -> bao cao doanh thu bi thieu tien.
        var row = Assert.Single(result.Value!.Items);
        Assert.Equal(777, row.CreatedByUserId);
        Assert.Null(row.CreatedByName);
    }

    [Fact] // §6.1: Given payment chua thanh toan (PaidAt = null), When xem, Then paymentDate lay CreatedAt
    public async Task GetAll_uses_created_at_as_payment_date_when_not_paid_yet()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        var created = new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc);
        await AddPaymentAsync(db, id: 1, membershipId: 100, status: PaymentStatus.Pending, paidAt: null, createdAt: created);

        var result = await new PaymentService(db).GetAllAsync(null, null, null, null, 1, 50, default);

        var row = Assert.Single(result.Value!.Items);
        Assert.Equal(created, row.PaymentDate); // PaidAt ?? CreatedAt
        Assert.Null(row.PaidAt);
    }

    [Fact] // §6: Given hoi vien da bi xoa mem, When xem lich su chung, Then payment cua ho khong xuat hien
    public async Task GetAll_excludes_payments_of_soft_deleted_members()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100, fullName: "Con hoat dong");
        await SeedMemberAsync(db, userId: 20, memberId: 2, membershipId: 200, fullName: "Da xoa", profileDeleted: true);
        await AddPaymentAsync(db, id: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 2, membershipId: 200);

        var result = await new PaymentService(db).GetAllAsync(null, null, null, null, 1, 50, default);

        var row = Assert.Single(result.Value!.Items);
        Assert.Equal("Con hoat dong", row.MemberName);
    }

    // ---------------------------------------------------------------------
    // GetAllAsync — loc, sap xep, phan trang
    // ---------------------------------------------------------------------

    [Fact] // §6: Given tron Paid va Pending, When loc status=Paid, Then chi tra dong Paid (khong phan biet hoa thuong)
    public async Task GetAll_filters_by_status_case_insensitively()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 1, membershipId: 100, status: PaymentStatus.Paid, paidAt: new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));
        await AddPaymentAsync(db, id: 2, membershipId: 100, status: PaymentStatus.Pending);

        var result = await new PaymentService(db).GetAllAsync(null, null, "paid", null, 1, 50, default);

        var row = Assert.Single(result.Value!.Items);
        Assert.Equal("Paid", row.Status);
    }

    [Fact] // §7: Given status khong thuoc enum, When goi API, Then 422 VALIDATION_ERROR (khong am tham bo qua bo loc)
    public async Task GetAll_with_unknown_status_returns_422()
    {
        using var db = NewDb();

        var result = await new PaymentService(db).GetAllAsync(null, null, "khong-ton-tai", null, 1, 50, default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    [Fact] // §7: Given from > to, When goi API, Then 422 VALIDATION_ERROR (khoang ngay vo nghia)
    public async Task GetAll_with_reversed_date_range_returns_422()
    {
        using var db = NewDb();

        var result = await new PaymentService(db).GetAllAsync(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 6, 1), null, null, 1, 50, default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact] // §6: Given hai hoi vien, When loc memberId, Then chi tra payment cua hoi vien do
    public async Task GetAll_filters_by_member_id()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100, fullName: "Hoi vien 1");
        await SeedMemberAsync(db, userId: 20, memberId: 2, membershipId: 200, fullName: "Hoi vien 2");
        await AddPaymentAsync(db, id: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 2, membershipId: 200);

        var result = await new PaymentService(db).GetAllAsync(null, null, null, 2, 1, 50, default);

        var row = Assert.Single(result.Value!.Items);
        Assert.Equal("Hoi vien 2", row.MemberName);
    }

    [Fact] // §6: Given nhieu payment, When xem, Then sap moi -> cu; cung thoi diem thi Id lon dung truoc
    public async Task GetAll_orders_newest_first_then_by_id()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        var sameMoment = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc);
        await AddPaymentAsync(db, id: 1, membershipId: 100, paidAt: new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc));
        await AddPaymentAsync(db, id: 2, membershipId: 100, paidAt: sameMoment);
        await AddPaymentAsync(db, id: 3, membershipId: 100, paidAt: sameMoment);

        var result = await new PaymentService(db).GetAllAsync(null, null, null, null, 1, 50, default);

        Assert.Equal(new long[] { 3, 2, 1 }, result.Value!.Items.Select(item => item.Id).ToArray());
    }

    [Fact] // §6: Given 5 payment, When lay trang 2 voi pageSize 2, Then tra dung 2 dong giua + tong trang = 3
    public async Task GetAll_paginates()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        for (var i = 1; i <= 5; i++)
        {
            await AddPaymentAsync(
                db, id: i, membershipId: 100,
                paidAt: new DateTime(2026, 6, i, 9, 0, 0, DateTimeKind.Utc));
        }

        var result = await new PaymentService(db).GetAllAsync(null, null, null, null, 2, 2, default);

        var page = result.Value!;
        Assert.Equal(5, page.Total);
        Assert.Equal(3, page.TotalPages);   // ceil(5 / 2)
        Assert.Equal(2, page.Page);
        // Moi -> cu la 5,4,3,2,1; trang 2 (pageSize 2) = phan tu thu 3 va 4.
        Assert.Equal(new long[] { 3, 2 }, page.Items.Select(item => item.Id).ToArray());
    }

    [Theory] // §6: Given tham so phan trang vo ly, When goi API, Then tu chuan hoa ve page>=1 va pageSize mac dinh 50
    [InlineData(0, 0)]
    [InlineData(-5, 101)]
    public async Task GetAll_normalises_invalid_paging_arguments(int page, int pageSize)
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 1, membershipId: 100);

        var result = await new PaymentService(db).GetAllAsync(null, null, null, null, page, pageSize, default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Page);
        Assert.Equal(50, result.Value.PageSize); // DefaultPageSize, chan khong cho keo pageSize > 100
        Assert.Single(result.Value.Items);
    }

    [Fact] // §6: Given khoang ngay from..to, When loc, Then 'to' tinh HET NGAY (bien phai la to + 1 ngay, khong bao gom)
    public async Task GetAll_date_filter_includes_the_whole_to_day()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 1, membershipId: 100, paidAt: new DateTime(2026, 5, 31, 23, 59, 0, DateTimeKind.Utc)); // truoc from
        await AddPaymentAsync(db, id: 2, membershipId: 100, paidAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));   // dung bien trai
        await AddPaymentAsync(db, id: 3, membershipId: 100, paidAt: new DateTime(2026, 6, 30, 15, 0, 0, DateTimeKind.Utc)); // trong ngay cuoi
        await AddPaymentAsync(db, id: 4, membershipId: 100, paidAt: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));   // qua bien phai

        var result = await new PaymentService(db).GetAllAsync(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), null, null, 1, 50, default);

        Assert.Equal(new long[] { 3, 2 }, result.Value!.Items.Select(item => item.Id).ToArray());
    }

    // ---------------------------------------------------------------------
    // GetByMemberAsync — quyen xem lich su cua mot nguoi
    // ---------------------------------------------------------------------

    [Fact] // §7: Given memberId khong ton tai, When xem lich su, Then 404 NOT_FOUND
    public async Task GetByMember_with_unknown_member_returns_404()
    {
        using var db = NewDb();

        var result = await new PaymentService(db).GetByMemberAsync(
            999, Principal(99, RoleNames.Staff), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact] // §7: Given Member A dang nhap, When xem lich su cua Member B, Then 403 FORBIDDEN
    public async Task GetByMember_blocks_member_reading_someone_else()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        await SeedMemberAsync(db, userId: 20, memberId: 2, membershipId: 200);

        var result = await new PaymentService(db).GetByMemberAsync(
            2, Principal(userId: 10, RoleNames.Member), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact] // §6: Given Member dang nhap, When xem lich su CUA CHINH MINH, Then duoc phep
    public async Task GetByMember_allows_member_reading_own_history()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 1, membershipId: 100);

        // Doi chieu userId (10) cua JWT voi MemberProfile.UserId, KHONG phai memberId (1).
        var result = await new PaymentService(db).GetByMemberAsync(
            1, Principal(userId: 10, RoleNames.Member), default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Theory] // §6: Given Admin/Staff, When xem lich su cua bat ky hoi vien nao, Then duoc phep
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Staff)]
    public async Task GetByMember_allows_admin_and_staff(string role)
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 1, membershipId: 100);

        var result = await new PaymentService(db).GetByMemberAsync(1, Principal(999, role), default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Fact] // §7: Given token khong mang role nao (vd PT), When xem lich su tien, Then 403 - mac dinh la TU CHOI
    public async Task GetByMember_denies_principal_without_a_known_role()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);

        var result = await new PaymentService(db).GetByMemberAsync(1, Principal(10, role: null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact] // §6: Given hoi vien chua tra lan nao, When xem lich su, Then mang rong + thanh cong (khong phai 404)
    public async Task GetByMember_with_no_payments_returns_empty_list()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);

        var result = await new PaymentService(db).GetByMemberAsync(
            1, Principal(99, RoleNames.Staff), default);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
    }

    // ---------------------------------------------------------------------
    // GetSummaryAsync — bao cao doanh thu
    // ---------------------------------------------------------------------

    [Fact] // §6.1: Given co ca Paid lan Pending, When tinh doanh thu, Then CHI cong tien Paid
    public async Task Summary_counts_revenue_from_paid_payments_only()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        var paidAt = new DateTime(2026, 6, 15, 3, 0, 0, DateTimeKind.Utc);
        await AddPaymentAsync(db, id: 1, membershipId: 100, status: PaymentStatus.Paid, amount: 500_000m, paidAt: paidAt);
        await AddPaymentAsync(db, id: 2, membershipId: 100, status: PaymentStatus.Paid, amount: 300_000m, paidAt: paidAt);
        await AddPaymentAsync(db, id: 3, membershipId: 100, status: PaymentStatus.Pending, amount: 900_000m);

        var result = await new PaymentService(db).GetSummaryAsync(null, null, default);

        var summary = result.Value!;
        Assert.Equal(3, summary.TotalPayments);
        Assert.Equal(2, summary.PaidPayments);
        Assert.Equal(1, summary.PendingPayments);
        // 900k cua don Pending KHONG duoc tinh - tien chua ve tay thi chua phai doanh thu.
        Assert.Equal(800_000m, summary.Revenue);
    }

    [Fact] // §6.1: Given nhieu hinh thuc tra, When tinh bao cao, Then gom theo phuong thuc, moi nhom co so luot + so tien
    public async Task Summary_groups_by_payment_method()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        var paidAt = new DateTime(2026, 6, 15, 3, 0, 0, DateTimeKind.Utc);
        await AddPaymentAsync(db, id: 1, membershipId: 100, method: PaymentMethod.Cash, amount: 500_000m, paidAt: paidAt);
        await AddPaymentAsync(db, id: 2, membershipId: 100, method: PaymentMethod.Cash, amount: 200_000m, paidAt: paidAt);
        await AddPaymentAsync(db, id: 3, membershipId: 100, method: PaymentMethod.Transfer, amount: 300_000m, paidAt: paidAt);

        var result = await new PaymentService(db).GetSummaryAsync(null, null, default);

        var byMethod = result.Value!.ByMethod;
        Assert.Equal(2, byMethod.Count);
        var cash = Assert.Single(byMethod, item => item.PaymentMethod == "Cash");
        Assert.Equal(2, cash.Count);
        Assert.Equal(700_000m, cash.Amount);
        var transfer = Assert.Single(byMethod, item => item.PaymentMethod == "Transfer");
        Assert.Equal(1, transfer.Count);
        Assert.Equal(300_000m, transfer.Amount);
    }

    [Fact] // NFR-04: Given giao dich luc 18:00 UTC ngay 30/06, When gom theo ngay, Then rot vao 01/07 vi gio VN = UTC+7
    public async Task Summary_groups_daily_revenue_by_vietnam_date_not_utc()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        // 30/06 18:00 UTC = 01/07 01:00 gio VN -> phai vao doanh thu ngay 01/07.
        await AddPaymentAsync(db, id: 1, membershipId: 100, amount: 500_000m,
            paidAt: new DateTime(2026, 6, 30, 18, 0, 0, DateTimeKind.Utc));
        // 30/06 10:00 UTC = 30/06 17:00 gio VN -> van la ngay 30/06.
        await AddPaymentAsync(db, id: 2, membershipId: 100, amount: 200_000m,
            paidAt: new DateTime(2026, 6, 30, 10, 0, 0, DateTimeKind.Utc));

        var result = await new PaymentService(db).GetSummaryAsync(null, null, default);

        var byDay = result.Value!.ByDay;
        Assert.Equal(2, byDay.Count);
        Assert.Equal(new DateOnly(2026, 6, 30), byDay[0].Date);
        Assert.Equal(200_000m, byDay[0].Amount);
        Assert.Equal(new DateOnly(2026, 7, 1), byDay[1].Date);
        Assert.Equal(500_000m, byDay[1].Amount);
    }

    [Fact] // §6.1: Given nhieu ngay, When gom theo ngay, Then sap tang dan de ve bieu do duong
    public async Task Summary_orders_daily_revenue_ascending()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 1, membershipId: 100, paidAt: new DateTime(2026, 6, 20, 3, 0, 0, DateTimeKind.Utc));
        await AddPaymentAsync(db, id: 2, membershipId: 100, paidAt: new DateTime(2026, 6, 5, 3, 0, 0, DateTimeKind.Utc));
        await AddPaymentAsync(db, id: 3, membershipId: 100, paidAt: new DateTime(2026, 6, 12, 3, 0, 0, DateTimeKind.Utc));

        var result = await new PaymentService(db).GetSummaryAsync(null, null, default);

        Assert.Equal(
            new[] { new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 20) },
            result.Value!.ByDay.Select(item => item.Date).ToArray());
    }

    [Fact] // §6: Given khoang ngay duoc chi dinh, When tinh bao cao, Then chi cong giao dich trong khoang + tra lai from/to da nhan
    public async Task Summary_respects_the_requested_date_range()
    {
        using var db = NewDb();
        await SeedMemberAsync(db, userId: 10, memberId: 1, membershipId: 100);
        await AddPaymentAsync(db, id: 1, membershipId: 100, amount: 500_000m, paidAt: new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));
        await AddPaymentAsync(db, id: 2, membershipId: 100, amount: 700_000m, paidAt: new DateTime(2026, 7, 10, 3, 0, 0, DateTimeKind.Utc));

        var from = new DateOnly(2026, 6, 1);
        var to = new DateOnly(2026, 6, 30);
        var result = await new PaymentService(db).GetSummaryAsync(from, to, default);

        var summary = result.Value!;
        Assert.Equal(from, summary.From);
        Assert.Equal(to, summary.To);
        Assert.Equal(1, summary.TotalPayments);
        Assert.Equal(500_000m, summary.Revenue);
    }

    [Fact] // §7: Given from > to, When tinh bao cao, Then 422 VALIDATION_ERROR (cung guard nhu GetAll)
    public async Task Summary_with_reversed_date_range_returns_422()
    {
        using var db = NewDb();

        var result = await new PaymentService(db).GetSummaryAsync(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 6, 1), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    [Fact] // §6.1: Given chua co giao dich nao, When tinh bao cao, Then doanh thu 0 + cac nhom rong (khong chia cho 0, khong null)
    public async Task Summary_with_no_payments_returns_zeroes()
    {
        using var db = NewDb();

        var result = await new PaymentService(db).GetSummaryAsync(null, null, default);

        Assert.True(result.Succeeded);
        var summary = result.Value!;
        Assert.Equal(0, summary.TotalPayments);
        Assert.Equal(0m, summary.Revenue);
        Assert.Empty(summary.ByMethod);
        Assert.Empty(summary.ByDay);
    }
}
