-- =============================================================================
-- 010_demo_active_member_seed.sql
-- Seed dữ liệu DEMO để test tính năng Quét ảnh AI / Nhật ký ăn.
-- Chạy SAU khi: (1) app khởi động 1 lần (DatabaseSeeder tạo sẵn 4 tài khoản demo
--               admin/staff/pt/member@gymmaster.local), và (2) đã chạy 009_food_membership_search.sql.
--
-- Tác dụng: cấp cho member@gymmaster.local MỘT GÓI TẬP CÒN HẠN (active) để
--           nút "Quét ảnh AI" mở khoá và test được full luồng.
--
-- ⚠️ Unicode tiếng Việt: chạy bằng SSMS, hoặc sqlcmd với codepage UTF-8:
--      sqlcmd -S "(localdb)\MSSQLLocalDB" -d GymMaster -E -C -f 65001 -i 010_demo_active_member_seed.sql
-- =============================================================================
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1) Đảm bảo có ít nhất 1 gói tập để bán/gán.
IF NOT EXISTS (SELECT 1 FROM dbo.membership_packages)
BEGIN
    INSERT INTO dbo.membership_packages (Name, Description, DurationDays, Price, IsActive, CreatedAt, SupportsPT)
    VALUES
        (N'Gói 1 tháng',  N'Tập gym tự do 30 ngày',   30,  300000, 1, SYSUTCDATETIME(), 0),
        (N'Gói 3 tháng',  N'Tập gym tự do 90 ngày',   90,  800000, 1, SYSUTCDATETIME(), 0),
        (N'Gói 1 năm PT', N'365 ngày + huấn luyện PT', 365, 5000000, 1, SYSUTCDATETIME(), 1);
END
GO

-- 2) Gán gói ACTIVE cho member@gymmaster.local (nếu chưa có gói còn hạn).
DECLARE @today      DATE   = CAST(GETUTCDATE() AS date);
DECLARE @packageId  BIGINT = (SELECT TOP 1 Id FROM dbo.membership_packages WHERE IsActive = 1 ORDER BY Id);
DECLARE @memberId   BIGINT = (
    SELECT mp.Id
    FROM dbo.member_profiles mp
    JOIN dbo.users u ON u.Id = mp.UserId
    WHERE u.Email = N'member@gymmaster.local'
);
DECLARE @adminId    BIGINT = (SELECT Id FROM dbo.users WHERE Email = N'admin@gymmaster.local');

IF @memberId IS NULL
BEGIN
    PRINT 'BO QUA: chua co member@gymmaster.local. Hay chay app 1 lan (DatabaseSeeder) truoc.';
END
ELSE IF @packageId IS NULL
BEGIN
    PRINT 'BO QUA: chua co membership_packages.';
END
ELSE IF EXISTS (
    SELECT 1 FROM dbo.memberships
    WHERE MemberId = @memberId AND Status = 1 AND EndDate >= @today
)
BEGIN
    PRINT 'OK: member@gymmaster.local da co goi active san.';
END
ELSE
BEGIN
    INSERT INTO dbo.memberships (MemberId, PackageId, StartDate, EndDate, Status, CreatedAt, UpdatedAt, CreatedByUserId)
    VALUES (@memberId, @packageId, @today, DATEADD(day, 30, @today), 1 /*Active*/, SYSUTCDATETIME(), SYSUTCDATETIME(), ISNULL(@adminId, @memberId));
    PRINT 'DA CAP goi active 30 ngay cho member@gymmaster.local.';
END
GO

-- 3) Kiem tra ket qua.
SELECT u.Email,
       (SELECT COUNT(*) FROM dbo.memberships m
        WHERE m.MemberId = mp.Id AND m.Status = 1 AND m.EndDate >= CAST(GETUTCDATE() AS date)) AS GoiActive
FROM dbo.users u
JOIN dbo.member_profiles mp ON mp.UserId = u.Id
WHERE u.Email = N'member@gymmaster.local';
GO
