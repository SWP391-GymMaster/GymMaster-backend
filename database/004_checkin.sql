/* =============================================================================
   004_checkin.sql  —  Spec 004 Member Check-in  (phần phụ trách: BAN)
   -----------------------------------------------------------------------------
   Chạy trên DB GymMaster thật để backend check-in đọc/ghi được.
   Idempotent: chạy lại nhiều lần không lỗi.
   ⚠️ KHÔNG xoá / sửa bảng khác. Chỉ TẠO bảng còn thiếu (+ seed demo tuỳ chọn).
   Schema khớp ĐÚNG cột mà code map (đã đối chiếu DB thật):
     check_ins(Id, MemberId, CheckInAt, CreatedBy)         -- CreatedBy NULL = member tự check-in
     memberships(Id, MemberId, PackageId, StartDate, EndDate, Status INT, CreatedAt, UpdatedAt)
   ============================================================================= */

SET NOCOUNT ON;
GO

/* -----------------------------------------------------------------------------
   1) BẮT BUỘC — bảng check_ins (entity GymMaster.API/Entities/CheckIn.cs)
   ----------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.check_ins', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.check_ins (
        Id        BIGINT IDENTITY(1,1) NOT NULL,
        MemberId  BIGINT    NOT NULL,
        CheckInAt DATETIME2 NOT NULL CONSTRAINT DF_check_ins_CheckInAt DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT    NULL,

        CONSTRAINT PK_check_ins PRIMARY KEY (Id),
        CONSTRAINT FK_check_ins_member_profiles FOREIGN KEY (MemberId)  REFERENCES dbo.member_profiles(Id),
        CONSTRAINT FK_check_ins_users           FOREIGN KEY (CreatedBy) REFERENCES dbo.users(Id)
    );

    CREATE INDEX IX_check_ins_MemberId_CheckInAt ON dbo.check_ins(MemberId, CheckInAt DESC);

    PRINT 'Đã tạo bảng dbo.check_ins.';
END
ELSE
    PRINT 'Bảng dbo.check_ins đã tồn tại — bỏ qua.';
GO

/* =============================================================================
   2) TUỲ CHỌN (DEMO) — chỉ cần khi bật CheckIn:EnforceMembership = true.
   Mặc định backend để false nên check-in chạy được NGAY mà không cần phần này.
   Block dưới đảm bảo có bảng memberships + 1 gói Active cho member demo để
   minh hoạ AC-01 (cho vào) / AC-02 (hết hạn) / AC-03 (chờ thanh toán).
   Module membership đầy đủ thuộc spec 003 — đây chỉ là helper tạm.
   ============================================================================= */
IF OBJECT_ID('dbo.memberships', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.memberships (
        Id        BIGINT IDENTITY(1,1) NOT NULL,
        MemberId  BIGINT    NOT NULL,
        PackageId BIGINT    NULL,
        StartDate DATE      NOT NULL,
        EndDate   DATE      NOT NULL,
        Status    INT       NOT NULL CONSTRAINT DF_memberships_Status DEFAULT 0,  -- 0 Pending,1 Active,2 Expired,3 Cancelled
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_memberships_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_memberships PRIMARY KEY (Id),
        CONSTRAINT FK_memberships_member_profiles FOREIGN KEY (MemberId) REFERENCES dbo.member_profiles(Id),
        CONSTRAINT CK_memberships_Status CHECK (Status IN (0, 1, 2, 3)),
        CONSTRAINT CK_memberships_Date   CHECK (EndDate >= StartDate)
    );
    CREATE INDEX IX_memberships_MemberId_Status_EndDate ON dbo.memberships(MemberId, Status, EndDate);
    PRINT 'Đã tạo bảng dbo.memberships (demo).';
END
GO

-- Seed 1 membership Active (PackageId NULL) cho member demo nếu hồ sơ đã có và chưa có gói Active.
DECLARE @memberProfileId BIGINT = (
    SELECT TOP 1 mp.Id
    FROM dbo.member_profiles mp
    JOIN dbo.users u ON u.Id = mp.UserId
    WHERE u.Email IN ('member@gymmaster.local', 'demo.member@gymmaster.local')
    ORDER BY mp.Id
);

IF @memberProfileId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.memberships WHERE MemberId = @memberProfileId AND Status = 1)
BEGIN
    INSERT dbo.memberships (MemberId, PackageId, StartDate, EndDate, Status)
    VALUES (@memberProfileId, NULL,
            CONVERT(DATE, SYSUTCDATETIME()),
            DATEADD(DAY, 30, CONVERT(DATE, SYSUTCDATETIME())),
            1); -- Active
    PRINT 'Đã seed gói Active demo cho member.';
END
ELSE
    PRINT 'Bỏ qua seed membership demo (không thấy hồ sơ hoặc đã có gói Active).';
GO
