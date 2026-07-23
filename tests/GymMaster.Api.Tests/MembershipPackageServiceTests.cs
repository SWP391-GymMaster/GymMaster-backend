using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Billing;
using GymMaster.API.Features.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Test tang Service cho MembershipPackageService (spec 003, muc FR-PKG).
//
// Cong cu: xUnit + EF Core InMemory. Moi test tu tao mot DB rieng theo Guid nen
// khong test nao thay du lieu cua test khac -> chay song song van on dinh.
//
// GIOI HAN da biet cua InMemory (khai bao truoc de khong tuong test bao ve duoc):
//   - InMemory so sanh chuoi CASE-SENSITIVE, con SQL Server dung collation
//     mac dinh CASE-INSENSITIVE. Vi vay o day KHONG khang dinh "Goi 1 thang" va
//     "GOI 1 THANG" bi coi la trung ten; rang buoc do thuoc ve UNIQUE index cua DB.
//   - InMemory khong ap dat UNIQUE index, nen test duoi day kiem tra ĐOAN CODE
//     chan trung ten (guard trong service), khong phai kiem tra index.
public class MembershipPackageServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-pkg-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal WithRole(string? role)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "99") };
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static async Task<MembershipPackage> AddPackageAsync(
        GymMasterDbContext db,
        string name,
        decimal price = 500_000m,
        short durationDays = 30,
        bool isActive = true,
        bool supportsPT = false)
    {
        var package = new MembershipPackage
        {
            Name = name,
            Price = price,
            DurationDays = durationDays,
            IsActive = isActive,
            SupportsPT = supportsPT,
        };
        db.MembershipPackages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    // Audit gia lap co GHI LAI loi goi, de khang dinh NFR-03 "moi buoc co audit".
    // NoopAudit (dung o MembershipServiceTests) chi nuot loi goi nen khong chung minh duoc dieu do.
    private sealed class RecordingAudit : IAuditService
    {
        public List<(string Action, string Entity, long EntityId)> Entries { get; } = [];

        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
        {
            Entries.Add((action, entity, entityId));
            return Task.CompletedTask;
        }
    }

    // ---------------------------------------------------------------------
    // CreateAsync
    // ---------------------------------------------------------------------

    [Fact] // FR-PKG-01: Given du lieu hop le, When tao goi, Then luu + tra 201 + goi mac dinh dang ban duoc
    public async Task Create_with_valid_input_persists_active_package_and_returns_201()
    {
        using var db = NewDb();
        var audit = new RecordingAudit();
        var service = new MembershipPackageService(db, audit);

        var result = await service.CreateAsync(
            new CreatePackageRequest("Goi 1 thang", 30, 500_000m, "Goi co ban"), default);

        Assert.True(result.Succeeded);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("Goi 1 thang", result.Value!.Name);
        Assert.Equal(30, result.Value.DurationDays);
        Assert.Equal(500_000m, result.Value.Price);
        // Goi moi tao mac dinh IsActive=true -> ban duoc ngay, khong can buoc kich hoat rieng.
        Assert.True(result.Value.IsActive);
        Assert.Equal(1, await db.MembershipPackages.CountAsync());
    }

    [Fact] // NFR-03: Given tao goi thanh cong, When luu xong, Then ghi AuditLog CREATE_PACKAGE
    public async Task Create_writes_audit_log()
    {
        using var db = NewDb();
        var audit = new RecordingAudit();
        var service = new MembershipPackageService(db, audit);

        var result = await service.CreateAsync(
            new CreatePackageRequest("Goi 1 thang", 30, 500_000m, null), default);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("CREATE_PACKAGE", entry.Action);
        Assert.Equal("MembershipPackage", entry.Entity);
        // Audit ghi SAU SaveChanges nen Id da co that (khac 0) -> tra ve dung ban ghi.
        Assert.Equal(result.Value!.Id, entry.EntityId);
        Assert.NotEqual(0, entry.EntityId);
    }

    [Theory] // FR-PKG-01: Given ten rong hoac chi toan khoang trang, When tao goi, Then 400 VALIDATION_ERROR
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_with_blank_name_is_rejected(string name)
    {
        using var db = NewDb();
        var audit = new RecordingAudit();
        var service = new MembershipPackageService(db, audit);

        var result = await service.CreateAsync(
            new CreatePackageRequest(name, 30, 500_000m, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Empty(db.MembershipPackages);
        Assert.Empty(audit.Entries); // Guard chan truoc khi ghi -> khong sinh audit rac.
    }

    [Theory] // FR-PKG-01: Given thoi han <= 0, When tao goi, Then 400 (goi 0 ngay se lam EndDate = StartDate)
    [InlineData((short)0)]
    [InlineData((short)-1)]
    public async Task Create_with_non_positive_duration_is_rejected(short durationDays)
    {
        using var db = NewDb();
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.CreateAsync(
            new CreatePackageRequest("Goi loi", durationDays, 500_000m, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Fact] // FR-PKG-01: Given gia am, When tao goi, Then 400. Gia = 0 van hop le (goi dung thu/khuyen mai).
    public async Task Create_with_negative_price_is_rejected_but_zero_price_is_allowed()
    {
        using var db = NewDb();
        var service = new MembershipPackageService(db, new RecordingAudit());

        var negative = await service.CreateAsync(
            new CreatePackageRequest("Goi am", 30, -1m, null), default);
        var free = await service.CreateAsync(
            new CreatePackageRequest("Goi dung thu", 7, 0m, null), default);

        Assert.False(negative.Succeeded);
        Assert.Equal("VALIDATION_ERROR", negative.ErrorCode);
        Assert.True(free.Succeeded);
        Assert.Equal(0m, free.Value!.Price);
    }

    [Fact] // FR-PKG-01: Given ten goi da ton tai, When tao trung, Then 409 DUPLICATE (khong phai 400/422)
    public async Task Create_with_duplicate_name_returns_409()
    {
        using var db = NewDb();
        await AddPackageAsync(db, "Goi 1 thang");
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.CreateAsync(
            new CreatePackageRequest("Goi 1 thang", 90, 1_200_000m, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("DUPLICATE", result.ErrorCode);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(1, await db.MembershipPackages.CountAsync());
    }

    [Fact] // FR-PKG-01: Given ten co khoang trang thua, When tao goi, Then luu ban da Trim va van bat duoc trung ten
    public async Task Create_trims_name_so_padded_duplicate_is_still_detected()
    {
        using var db = NewDb();
        var service = new MembershipPackageService(db, new RecordingAudit());

        var first = await service.CreateAsync(
            new CreatePackageRequest("  Goi 1 thang  ", 30, 500_000m, "  mo ta  "), default);
        var second = await service.CreateAsync(
            new CreatePackageRequest("Goi 1 thang", 30, 500_000m, null), default);

        Assert.True(first.Succeeded);
        Assert.Equal("Goi 1 thang", first.Value!.Name); // Trim truoc khi luu.
        Assert.Equal("mo ta", first.Value.Description);
        Assert.False(second.Succeeded); // Nho Trim nen "  X  " va "X" bi coi la mot.
        Assert.Equal("DUPLICATE", second.ErrorCode);
    }

    [Fact] // FR-PKG-01: Given mo ta rong, When tao goi, Then luu null (khong luu chuoi rong vao DB)
    public async Task Create_stores_blank_description_as_null()
    {
        using var db = NewDb();
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.CreateAsync(
            new CreatePackageRequest("Goi 1 thang", 30, 500_000m, "   "), default);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Description);
    }

    [Fact] // FR-PKG-03 / AC-07: Given tao goi, When khong khai bao supportsPT, Then mac dinh false; khai bao true thi luu true
    public async Task Create_defaults_supportsPT_to_false_and_honours_true()
    {
        using var db = NewDb();
        var service = new MembershipPackageService(db, new RecordingAudit());

        var normal = await service.CreateAsync(
            new CreatePackageRequest("Goi thuong", 30, 500_000m, null), default);
        var withPt = await service.CreateAsync(
            new CreatePackageRequest("Goi PT", 30, 900_000m, null, SupportsPT: true), default);

        Assert.False(normal.Value!.SupportsPT);
        Assert.True(withPt.Value!.SupportsPT);
    }

    // ---------------------------------------------------------------------
    // UpdateAsync
    // ---------------------------------------------------------------------

    [Fact] // FR-PKG-01: Given id khong ton tai, When sua goi, Then 404 NOT_FOUND
    public async Task Update_missing_package_returns_404()
    {
        using var db = NewDb();
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.UpdateAsync(
            999, new UpdatePackageRequest("Ten moi", null, null, null, null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact] // FR-PKG-01: Given chi gui gia moi, When sua goi, Then cac truong khac GIU NGUYEN (partial update)
    public async Task Update_with_only_price_leaves_other_fields_untouched()
    {
        using var db = NewDb();
        var package = await AddPackageAsync(db, "Goi 1 thang", price: 500_000m, durationDays: 30, supportsPT: true);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, null, 650_000m, null, null, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal(650_000m, result.Value!.Price);
        // Truong null trong request = "khong doi", khong phai "xoa ve null".
        Assert.Equal("Goi 1 thang", result.Value.Name);
        Assert.Equal(30, result.Value.DurationDays);
        Assert.True(result.Value.SupportsPT);
        Assert.True(result.Value.IsActive);
    }

    [Fact] // FR-PKG-01: Given goi khac da mang ten do, When doi ten trung, Then 409 DUPLICATE
    public async Task Update_to_a_name_used_by_another_package_returns_409()
    {
        using var db = NewDb();
        var first = await AddPackageAsync(db, "Goi 1 thang");
        var second = await AddPackageAsync(db, "Goi 3 thang");
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.UpdateAsync(
            second.Id, new UpdatePackageRequest("Goi 1 thang", null, null, null, null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal("DUPLICATE", result.ErrorCode);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    [Fact] // FR-PKG-01: Given goi gui lai CHINH ten cua no, When sua, Then van thanh cong (dieu kien Id != id loai tru chinh no)
    public async Task Update_keeping_its_own_name_is_not_treated_as_duplicate()
    {
        using var db = NewDb();
        var package = await AddPackageAsync(db, "Goi 1 thang", price: 500_000m);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest("Goi 1 thang", null, 600_000m, null, null, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Goi 1 thang", result.Value!.Name);
        Assert.Equal(600_000m, result.Value.Price);
    }

    [Fact] // FR-PKG-01: Given sua ten thanh rong / thoi han <= 0 / gia am, When sua, Then 400 va DB khong doi
    public async Task Update_rejects_invalid_values_without_persisting_them()
    {
        using var db = NewDb();
        var package = await AddPackageAsync(db, "Goi 1 thang", price: 500_000m, durationDays: 30);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var blankName = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest("   ", null, null, null, null, null), default);
        var badDuration = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, 0, null, null, null, null), default);
        var badPrice = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, null, -5m, null, null, null), default);

        Assert.Equal("VALIDATION_ERROR", blankName.ErrorCode);
        Assert.Equal("VALIDATION_ERROR", badDuration.ErrorCode);
        Assert.Equal("VALIDATION_ERROR", badPrice.ErrorCode);
        Assert.All(
            new[] { blankName, badDuration, badPrice },
            item => Assert.Equal(StatusCodes.Status400BadRequest, item.StatusCode));

        // Doc lai tu DB de chac chan guard chan TRUOC SaveChanges, khong phai chan sau khi da ghi.
        var stored = await db.MembershipPackages.AsNoTracking().SingleAsync(item => item.Id == package.Id);
        Assert.Equal("Goi 1 thang", stored.Name);
        Assert.Equal(30, stored.DurationDays);
        Assert.Equal(500_000m, stored.Price);
    }

    [Fact] // FR-PKG-02: Given goi dang ban, When Admin tat IsActive, Then goi luu IsActive=false (dau vao cua guard PACKAGE_INACTIVE)
    public async Task Update_can_deactivate_package()
    {
        using var db = NewDb();
        var package = await AddPackageAsync(db, "Goi 1 thang");
        var audit = new RecordingAudit();
        var service = new MembershipPackageService(db, audit);

        var result = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, null, null, null, IsActive: false, null), default);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.IsActive);
        // Goi tat roi thi MembershipService.SellAsync se tra 422 PACKAGE_INACTIVE (test o MembershipServiceTests).
        Assert.Equal("UPDATE_PACKAGE", Assert.Single(audit.Entries).Action);
    }

    [Fact] // FR-PKG-01: Given thoi han moi hop le, When sua goi, Then luu thoi han moi (doi truc tiep EndDate cua don ban sau do)
    public async Task Update_changes_duration_when_value_is_valid()
    {
        using var db = NewDb();
        var package = await AddPackageAsync(db, "Goi 1 thang", durationDays: 30);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, 90, null, null, null, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal(90, result.Value!.DurationDays);
        var stored = await db.MembershipPackages.AsNoTracking().SingleAsync(item => item.Id == package.Id);
        Assert.Equal(90, stored.DurationDays);
    }

    [Fact] // FR-PKG-03: Given goi thuong, When Admin bat SupportsPT, Then goi thanh goi co PT (anh huong guard PACKAGE_PT_MISMATCH khi gia han)
    public async Task Update_can_toggle_supportsPT()
    {
        using var db = NewDb();
        var package = await AddPackageAsync(db, "Goi 1 thang", supportsPT: false);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var on = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, null, null, null, null, SupportsPT: true), default);
        var off = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, null, null, null, null, SupportsPT: false), default);

        Assert.True(on.Value!.SupportsPT);
        Assert.False(off.Value!.SupportsPT);
    }

    [Fact] // FR-PKG-01: Given sua mo ta, When gui chuoi rong, Then luu null; gui chuoi that thi Trim roi luu
    public async Task Update_normalises_description()
    {
        using var db = NewDb();
        var package = await AddPackageAsync(db, "Goi 1 thang");
        var service = new MembershipPackageService(db, new RecordingAudit());

        var filled = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, null, null, "  Bao gom tu do  ", null, null), default);
        var cleared = await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, null, null, "   ", null, null), default);

        Assert.Equal("Bao gom tu do", filled.Value!.Description);
        Assert.Null(cleared.Value!.Description); // "   " nghia la xoa mo ta, khong luu khoang trang vao DB.
    }

    [Fact] // NFR-03: Given sua goi thanh cong, When luu xong, Then UpdatedAt duoc dong dau thoi diem sua
    public async Task Update_stamps_updated_at()
    {
        using var db = NewDb();
        var package = await AddPackageAsync(db, "Goi 1 thang");
        Assert.Null(package.UpdatedAt);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var before = DateTime.UtcNow;
        await service.UpdateAsync(
            package.Id, new UpdatePackageRequest(null, null, 700_000m, null, null, null), default);

        var stored = await db.MembershipPackages.AsNoTracking().SingleAsync(item => item.Id == package.Id);
        Assert.NotNull(stored.UpdatedAt);
        Assert.InRange(stored.UpdatedAt!.Value, before, DateTime.UtcNow);
    }

    // ---------------------------------------------------------------------
    // ListAsync
    // ---------------------------------------------------------------------

    [Theory] // FR-PKG-02 / §6: Given co goi da tat, When Admin hoac Staff xem, Then thay CA goi tat (de bat lai)
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Staff)]
    public async Task List_shows_inactive_packages_to_admin_and_staff(string role)
    {
        using var db = NewDb();
        await AddPackageAsync(db, "Goi dang ban", isActive: true);
        await AddPackageAsync(db, "Goi ngung ban", isActive: false);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.ListAsync(WithRole(role), default);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Count);
    }

    [Theory] // FR-PKG-02 / §6: Given co goi da tat, When Member (hoac PT) xem, Then chi thay goi con ban
    [InlineData(RoleNames.Member)]
    [InlineData(RoleNames.Pt)]
    [InlineData(null)]
    public async Task List_hides_inactive_packages_from_non_staff(string? role)
    {
        using var db = NewDb();
        await AddPackageAsync(db, "Goi dang ban", isActive: true);
        await AddPackageAsync(db, "Goi ngung ban", isActive: false);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.ListAsync(WithRole(role), default);

        Assert.True(result.Succeeded);
        var item = Assert.Single(result.Value!);
        Assert.Equal("Goi dang ban", item.Name);
    }

    [Fact] // §6: Given nhieu goi, When liet ke, Then sap theo gia tang dan, cung gia thi thoi han ngan truoc
    public async Task List_orders_by_price_then_duration()
    {
        using var db = NewDb();
        await AddPackageAsync(db, "Goi dat", price: 1_200_000m, durationDays: 90);
        await AddPackageAsync(db, "Goi re dai ngay", price: 500_000m, durationDays: 60);
        await AddPackageAsync(db, "Goi re ngan ngay", price: 500_000m, durationDays: 30);
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.ListAsync(WithRole(RoleNames.Admin), default);

        Assert.Equal(
            new[] { "Goi re ngan ngay", "Goi re dai ngay", "Goi dat" },
            result.Value!.Select(item => item.Name).ToArray());
    }

    [Fact] // §6: Given chua co goi nao, When liet ke, Then tra mang rong + Succeeded=true (khong phai 404)
    public async Task List_with_no_packages_returns_empty_success()
    {
        using var db = NewDb();
        var service = new MembershipPackageService(db, new RecordingAudit());

        var result = await service.ListAsync(WithRole(RoleNames.Admin), default);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
    }
}
