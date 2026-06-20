/*
    GymMaster Database - FINAL (aligned-with-code build)
    Target: SQL Server 2019+ / Azure SQL + ASP.NET Core 10 + EF Core 10 (Code First)

    ┌─────────────────────────────────────────────────────────────────────────┐
    │  MỤC ĐÍCH FILE NÀY                                                         │
    │  Script này được CHỈNH cho khớp 100% với CODE BACKEND đang có             │
    │  (spec 001 Auth + spec 002 User/Member/PT). Nó là "hợp đồng schema" để    │
    │  FE và 4 bạn BE làm song song mà KHÔNG xung đột.                          │
    └─────────────────────────────────────────────────────────────────────────┘

    ⚠️ QUY ƯỚC BẮT BUỘC (theo code thực tế + 15_DATABASE_SCHEMA.md):
      - Tên BẢNG: snake_case, chữ thường  -> users, roles, user_roles, member_profiles...
      - Tên CỘT : PascalCase               -> Email, FailedLoginCount, UserId... (EF giữ nguyên tên property)
      - Khóa    : Id BIGINT IDENTITY(1,1)
      - User<->Role qua bảng phụ user_roles (nhiều-nhiều). KHÔNG có RoleId trên users.
      - users.Status = CHUỖI 'active' / 'locked' (NVARCHAR), KHÔNG phải TINYINT.
      - Thời gian DATETIME2 (UTC). Boolean BIT. Chuỗi NVARCHAR (Unicode).
      - Soft-delete: cột IsDeleted BIT DEFAULT 0.
      - Code KHÔNG dùng RowVersion -> các bảng ĐÃ CODE không có cột này.

    FILE GỒM 2 PHẦN:
      PHẦN A — ✅ ĐÃ CODE (spec 001 + 002): users, roles, user_roles, refresh_tokens,
               password_reset_tokens, member_profiles, trainer_profiles, audit_logs.
               => Đây là CONTRACT. Tên/kiểu/cột phải khớp ĐÚNG code. Đừng đổi tùy tiện.
      PHẦN B — ⬜ THIẾT KẾ (spec 003–008): membership, payment, check-in, PT training,
               progress, nutrition, dashboard. Cùng quy ước nhưng là BẢN ĐỀ XUẤT;
               tên/cột cuối cùng chốt khi từng spec được code (dev phụ trách spec đó).

    ──────────────────────────────────────────────────────────────────────────
    NGUỒN SỰ THẬT & CÁCH DÙNG (đọc CONSTITUTION ARCH-04):
      "Mọi thay đổi schema phải qua EF Core Migration (Code First)."
      => Nguồn chuẩn của PHẦN A là CODE backend, KHÔNG phải file này.
         File này phản chiếu lại code để mọi người đọc & dựng DB nhanh.

      Có 2 cách dựng DB dev, chọn 1:
      (1) [Khuyến nghị] Để backend tự tạo: app gọi EnsureCreated + patch tương thích
          rồi seed roles + admin (admin@gymmaster.local / Admin123!). Không cần chạy file này.
      (2) Chạy file này để tạo sẵn DB (kể cả PHẦN B chưa code). Backend khi khởi động
          thấy DB đã có bảng -> EnsureCreated bỏ qua, các bước patch là idempotent (no-op),
          rồi seed roles + admin như thường. KHÔNG xung đột vì PHẦN A khớp đúng code.

    ⚠️ CẢNH BÁO: mục 0 sẽ DROP & tạo lại GymMasterDb (mất sạch dữ liệu).
       Chỉ dùng cho máy DEV cá nhân. KHÔNG chạy trên DB dùng chung.
*/

-- Cần cho filtered index, index trên computed column, và view SCHEMABINDING.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =========================================================
-- 0. DATABASE  (DEV reset — destructive)
-- =========================================================
IF DB_ID(N'GymMasterDb') IS NOT NULL
BEGIN
    PRINT N'>> CẢNH BÁO: đang DROP database GymMasterDb (xóa toàn bộ dữ liệu dev).';
    ALTER DATABASE GymMasterDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE GymMasterDb;
END;
GO

CREATE DATABASE GymMasterDb;
GO

USE GymMasterDb;
GO


/* =============================================================================
   ██  PHẦN A — ✅ ĐÃ CODE (spec 001 Auth + spec 002 User/Member/PT)
   ██  Khớp ĐÚNG code backend. KHÔNG đổi tên/kiểu cột nếu chưa đổi entity + DbContext.
   ============================================================================= */

-- =========================================================
-- A1. roles
-- =========================================================
CREATE TABLE dbo.roles (
    Id          BIGINT IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(30)  NOT NULL,   -- 'admin' / 'staff' / 'pt' / 'member' (chữ thường)
    Description NVARCHAR(255) NULL,

    CONSTRAINT PK_roles PRIMARY KEY (Id)
);
GO
CREATE UNIQUE INDEX IX_roles_Name ON dbo.roles(Name);
GO

-- =========================================================
-- A2. users  (1 account; role lấy qua user_roles — KHÔNG có RoleId)
-- =========================================================
CREATE TABLE dbo.users (
    Id                   BIGINT IDENTITY(1,1) NOT NULL,
    Email                NVARCHAR(255) NOT NULL,
    Phone                NVARCHAR(30)  NULL,
    PasswordHash         NVARCHAR(255) NOT NULL,                          -- BCrypt cost >= 12
    FullName             NVARCHAR(150) NOT NULL,
    Status               NVARCHAR(20)  NOT NULL CONSTRAINT DF_users_Status DEFAULT N'active', -- 'active' | 'locked'
    FailedLoginCount     INT           NOT NULL CONSTRAINT DF_users_FailedLoginCount DEFAULT 0,
    LoginWindowStartedAt DATETIME2     NULL,                              -- mốc cửa sổ 15' đếm brute-force
    LockedUntil          DATETIME2     NULL,                              -- khóa tạm tới thời điểm này
    LastLoginAt          DATETIME2     NULL,
    IsDeleted            BIT           NOT NULL CONSTRAINT DF_users_IsDeleted DEFAULT 0,
    CreatedAt            DATETIME2     NOT NULL CONSTRAINT DF_users_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt            DATETIME2     NOT NULL CONSTRAINT DF_users_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_users PRIMARY KEY (Id),
    -- An toàn dữ liệu, khớp giá trị code sinh ra (UserStatuses.Active/Locked):
    CONSTRAINT CK_users_Status CHECK (Status IN (N'active', N'locked')),
    CONSTRAINT CK_users_FailedLoginCount CHECK (FailedLoginCount >= 0)
);
GO
-- EF: HasIndex(Email).IsUnique() ; HasIndex(Phone).IsUnique().HasFilter("[Phone] IS NOT NULL")
CREATE UNIQUE INDEX IX_users_Email ON dbo.users(Email);
CREATE UNIQUE INDEX IX_users_Phone ON dbo.users(Phone) WHERE Phone IS NOT NULL;
GO

-- =========================================================
-- A3. user_roles  (n-n; PK kép)
-- =========================================================
CREATE TABLE dbo.user_roles (
    UserId BIGINT NOT NULL,
    RoleId BIGINT NOT NULL,

    CONSTRAINT PK_user_roles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_user_roles_users FOREIGN KEY (UserId) REFERENCES dbo.users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_user_roles_roles FOREIGN KEY (RoleId) REFERENCES dbo.roles(Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_user_roles_RoleId ON dbo.user_roles(RoleId);
GO

-- =========================================================
-- A4. refresh_tokens  (JWT refresh; lưu bản băm)
-- =========================================================
CREATE TABLE dbo.refresh_tokens (
    Id        BIGINT IDENTITY(1,1) NOT NULL,
    UserId    BIGINT        NOT NULL,
    TokenHash NVARCHAR(255) NOT NULL,
    ExpiresAt DATETIME2     NOT NULL,                  -- 7 ngày
    RevokedAt DATETIME2     NULL,                       -- rotate/logout
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_refresh_tokens_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_refresh_tokens PRIMARY KEY (Id),
    CONSTRAINT FK_refresh_tokens_users FOREIGN KEY (UserId) REFERENCES dbo.users(Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_refresh_tokens_UserId    ON dbo.refresh_tokens(UserId);
CREATE INDEX IX_refresh_tokens_TokenHash ON dbo.refresh_tokens(TokenHash); -- tra cứu khi refresh
GO

-- =========================================================
-- A5. password_reset_tokens  (quên/đặt lại mật khẩu; 30 phút)
-- =========================================================
CREATE TABLE dbo.password_reset_tokens (
    Id        BIGINT IDENTITY(1,1) NOT NULL,
    UserId    BIGINT        NOT NULL,
    TokenHash NVARCHAR(255) NOT NULL,
    ExpiresAt DATETIME2     NOT NULL,                  -- 30 phút
    UsedAt    DATETIME2     NULL,                       -- đánh dấu đã dùng
    AttemptCount INT        NOT NULL CONSTRAINT DF_password_reset_tokens_AttemptCount DEFAULT 0,  -- nhập sai OTP quá 3 lần -> vô hiệu
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_password_reset_tokens_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_password_reset_tokens PRIMARY KEY (Id),
    CONSTRAINT FK_password_reset_tokens_users FOREIGN KEY (UserId) REFERENCES dbo.users(Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_password_reset_tokens_UserId ON dbo.password_reset_tokens(UserId);
GO

-- =========================================================
-- A6. member_profiles  (1-1 với users role=member)
-- LƯU Ý: code KHÔNG có MemberCode/HeightCm; EmergencyContact là 1 cột;
--        DateOfBirth & JoinedAt là DATETIME2; Gender là chuỗi.
-- =========================================================
CREATE TABLE dbo.member_profiles (
    Id               BIGINT IDENTITY(1,1) NOT NULL,
    UserId           BIGINT        NOT NULL,
    DateOfBirth      DATETIME2     NULL,
    Gender           NVARCHAR(20)  NULL,                 -- chuỗi tự do (vd 'male'/'female'/'other')
    Address          NVARCHAR(255) NULL,
    EmergencyContact NVARCHAR(100) NULL,
    JoinedAt         DATETIME2     NOT NULL CONSTRAINT DF_member_profiles_JoinedAt DEFAULT SYSUTCDATETIME(),
    IsDeleted        BIT           NOT NULL CONSTRAINT DF_member_profiles_IsDeleted DEFAULT 0,
    CreatedAt        DATETIME2     NOT NULL CONSTRAINT DF_member_profiles_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2     NOT NULL CONSTRAINT DF_member_profiles_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_member_profiles PRIMARY KEY (Id),
    CONSTRAINT FK_member_profiles_users FOREIGN KEY (UserId) REFERENCES dbo.users(Id) ON DELETE CASCADE
);
GO
CREATE UNIQUE INDEX IX_member_profiles_UserId ON dbo.member_profiles(UserId);
GO

-- =========================================================
-- A7. trainer_profiles  (1-1 với users role=pt)
-- =========================================================
CREATE TABLE dbo.trainer_profiles (
    Id                BIGINT IDENTITY(1,1) NOT NULL,
    UserId            BIGINT         NOT NULL,
    Specialty         NVARCHAR(150)  NULL,
    Bio               NVARCHAR(1000) NULL,
    Gender            NVARCHAR(20)   NULL,
    DateOfBirth       DATETIME2      NULL,
    YearsOfExperience INT            NULL,
    IsDeleted         BIT            NOT NULL CONSTRAINT DF_trainer_profiles_IsDeleted DEFAULT 0,
    CreatedAt         DATETIME2      NOT NULL CONSTRAINT DF_trainer_profiles_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2      NOT NULL CONSTRAINT DF_trainer_profiles_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_trainer_profiles PRIMARY KEY (Id),
    CONSTRAINT FK_trainer_profiles_users FOREIGN KEY (UserId) REFERENCES dbo.users(Id) ON DELETE CASCADE,
    CONSTRAINT CK_trainer_profiles_YearsOfExperience CHECK (YearsOfExperience IS NULL OR YearsOfExperience >= 0)
);
GO
CREATE UNIQUE INDEX IX_trainer_profiles_UserId ON dbo.trainer_profiles(UserId);
GO

-- =========================================================
-- A8. audit_logs  (append-only)
-- LƯU Ý code: cột Entity (không phải EntityName), Metadata (không phải MetadataJson),
--             EntityId BIGINT NOT NULL, và KHÔNG có FK trên UserId.
-- =========================================================
CREATE TABLE dbo.audit_logs (
    Id        BIGINT IDENTITY(1,1) NOT NULL,
    UserId    BIGINT         NULL,                       -- actor (NULL = hệ thống)
    Action    NVARCHAR(100)  NOT NULL,                   -- vd CREATE_MEMBER, UPDATE_USER
    Entity    NVARCHAR(60)   NOT NULL,                   -- vd MemberProfile
    EntityId  BIGINT         NOT NULL,
    Metadata  NVARCHAR(MAX)  NULL,                        -- JSON (không chứa PII nhạy cảm)
    CreatedAt DATETIME2      NOT NULL CONSTRAINT DF_audit_logs_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_audit_logs PRIMARY KEY (Id),
    CONSTRAINT CK_audit_logs_Metadata CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1)
);
GO
CREATE INDEX IX_audit_logs_Entity_EntityId ON dbo.audit_logs(Entity, EntityId);
GO

-- =========================================================
-- A9. SEED tối thiểu cho PHẦN A
-- Roles khớp code (chữ thường). Backend seeder cũng "insert if not exists" -> không trùng.
-- Admin (admin@gymmaster.local / Admin123!) do BACKEND tự seed bằng BCrypt khi khởi động
-- => KHÔNG seed user ở đây để tránh password hash giả không đăng nhập được.
-- =========================================================
INSERT INTO dbo.roles (Name, Description) VALUES
(N'admin',  N'admin role'),
(N'staff',  N'staff role'),
(N'pt',     N'pt role'),
(N'member', N'member role');
GO


/* =============================================================================
   ██  PHẦN B — ⬜ THIẾT KẾ (spec 003–008) — BẢN ĐỀ XUẤT, CHƯA CODE
   ██  Cùng quy ước (snake_case bảng / PascalCase cột / BIGINT / DATETIME2).
   ██  Enum nghiệp vụ dùng TINYINT + CHECK (theo 15_DATABASE_SCHEMA.md).
   ██  Tên/cột cuối cùng do dev phụ trách spec chốt khi code (qua EF Migration).
   ============================================================================= */

-- =========================================================
-- B1. membership_packages  (spec 003)
-- =========================================================
CREATE TABLE dbo.membership_packages (
    Id           BIGINT IDENTITY(1,1) NOT NULL,
    Name         NVARCHAR(100) NOT NULL,
    Description  NVARCHAR(500) NULL,
    DurationDays SMALLINT      NOT NULL,
    Price        DECIMAL(12,2) NOT NULL,
    IsActive     BIT           NOT NULL CONSTRAINT DF_membership_packages_IsActive DEFAULT 1,
    CreatedAt    DATETIME2     NOT NULL CONSTRAINT DF_membership_packages_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2     NULL,

    CONSTRAINT PK_membership_packages PRIMARY KEY (Id),
    CONSTRAINT UQ_membership_packages_Name UNIQUE (Name),
    CONSTRAINT CK_membership_packages_DurationDays CHECK (DurationDays > 0),
    CONSTRAINT CK_membership_packages_Price CHECK (Price >= 0)
);
GO

-- =========================================================
-- B2. memberships  (spec 003) — member mua 1 gói
-- =========================================================
CREATE TABLE dbo.memberships (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    MemberId        BIGINT  NOT NULL,
    PackageId       BIGINT  NOT NULL,
    StartDate       DATE    NOT NULL,
    EndDate         DATE    NOT NULL,
    Status          TINYINT NOT NULL CONSTRAINT DF_memberships_Status DEFAULT 0,  -- 0=PendingPayment (ADR-05)
    CreatedByUserId BIGINT  NOT NULL,
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_memberships_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2 NULL,

    CONSTRAINT PK_memberships PRIMARY KEY (Id),
    CONSTRAINT FK_memberships_member_profiles FOREIGN KEY (MemberId)        REFERENCES dbo.member_profiles(Id),
    CONSTRAINT FK_memberships_packages        FOREIGN KEY (PackageId)       REFERENCES dbo.membership_packages(Id),
    CONSTRAINT FK_memberships_users           FOREIGN KEY (CreatedByUserId) REFERENCES dbo.users(Id),
    CONSTRAINT CK_memberships_Status CHECK (Status IN (0, 1, 2, 3)),       -- 0 Pending,1 Active,2 Expired,3 Cancelled
    CONSTRAINT CK_memberships_Date   CHECK (EndDate >= StartDate)
);
GO
-- Tối đa 1 membership Active / member.
CREATE UNIQUE INDEX UX_memberships_OneActivePerMember ON dbo.memberships(MemberId) WHERE Status = 1;
CREATE INDEX IX_memberships_MemberId_Status_EndDate    ON dbo.memberships(MemberId, Status, EndDate);
CREATE INDEX IX_memberships_PackageId                  ON dbo.memberships(PackageId);
-- Chỉ index gói còn sống (cho job hết hạn + dashboard).
CREATE INDEX IX_memberships_EndDate ON dbo.memberships(EndDate) WHERE Status IN (0, 1);
GO

-- =========================================================
-- B3. payments  (spec 003) — thủ công; 1 membership -> 1..n payment
-- =========================================================
CREATE TABLE dbo.payments (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    MembershipId    BIGINT        NOT NULL,
    Amount          DECIMAL(12,2) NOT NULL,
    PaymentMethod   TINYINT       NOT NULL,                  -- 1 Cash, 2 BankTransfer, 3 Card
    Status          TINYINT       NOT NULL CONSTRAINT DF_payments_Status DEFAULT 0,   -- 0 Pending,1 Paid,2 Refunded
    PaidAt          DATETIME2     NULL,
    CreatedByUserId BIGINT        NOT NULL,
    CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_payments_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2     NULL,                       -- set khi refund/đổi trạng thái

    CONSTRAINT PK_payments PRIMARY KEY (Id),
    CONSTRAINT FK_payments_memberships FOREIGN KEY (MembershipId)    REFERENCES dbo.memberships(Id),
    CONSTRAINT FK_payments_users       FOREIGN KEY (CreatedByUserId) REFERENCES dbo.users(Id),
    CONSTRAINT CK_payments_Amount        CHECK (Amount > 0),
    CONSTRAINT CK_payments_PaymentMethod CHECK (PaymentMethod IN (1, 2, 3)),
    CONSTRAINT CK_payments_Status        CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_payments_PaidAt_WhenPaid CHECK (Status <> 1 OR PaidAt IS NOT NULL)
);
GO
CREATE INDEX IX_payments_MembershipId    ON dbo.payments(MembershipId);
CREATE INDEX IX_payments_CreatedByUserId ON dbo.payments(CreatedByUserId);
-- Filtered covering index cho báo cáo doanh thu (chỉ dòng đã Paid).
CREATE INDEX IX_payments_Paid ON dbo.payments(PaidAt) INCLUDE (Amount) WHERE Status = 1;
GO

-- =========================================================
-- B4. check_ins  (spec 004 — phần phụ trách của BAN)
-- CreatedByUserId NULL = member tự check-in.
-- Tra cứu member theo member_profiles.Id hoặc users.Phone (schema mới KHÔNG có MemberCode).
-- =========================================================
CREATE TABLE dbo.check_ins (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    MemberId        BIGINT    NOT NULL,
    CheckInAt       DATETIME2 NOT NULL CONSTRAINT DF_check_ins_CheckInAt DEFAULT SYSUTCDATETIME(),
    -- Cột ngày persisted cho báo cáo "check-in theo ngày" (index được, khác với index trên biểu thức).
    CheckInDate     AS CONVERT(DATE, CheckInAt) PERSISTED,
    CreatedBy       BIGINT    NULL,   -- khop code (CheckIn.CreatedBy); NULL = member tu check-in
    Note            NVARCHAR(255) NULL,

    CONSTRAINT PK_check_ins PRIMARY KEY (Id),
    CONSTRAINT FK_check_ins_member_profiles FOREIGN KEY (MemberId)  REFERENCES dbo.member_profiles(Id),
    CONSTRAINT FK_check_ins_users           FOREIGN KEY (CreatedBy) REFERENCES dbo.users(Id)
);
GO
CREATE INDEX IX_check_ins_MemberId_CheckInAt ON dbo.check_ins(MemberId, CheckInAt DESC);
CREATE INDEX IX_check_ins_CheckInDate        ON dbo.check_ins(CheckInDate DESC);
GO

-- =========================================================
-- B5. trainer_assignments  (spec 005) — PT <-> Member theo thời gian
-- =========================================================
CREATE TABLE dbo.trainer_assignments (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    TrainerId       BIGINT  NOT NULL,
    MemberId        BIGINT  NOT NULL,
    StartDate       DATE    NOT NULL,
    EndDate         DATE    NULL,
    Status          TINYINT NOT NULL CONSTRAINT DF_trainer_assignments_Status DEFAULT 1,  -- 1 Active, 2 Ended
    CreatedByUserId BIGINT  NOT NULL,
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_trainer_assignments_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2 NULL,

    CONSTRAINT PK_trainer_assignments PRIMARY KEY (Id),
    CONSTRAINT FK_trainer_assignments_trainer_profiles FOREIGN KEY (TrainerId)       REFERENCES dbo.trainer_profiles(Id),
    CONSTRAINT FK_trainer_assignments_member_profiles  FOREIGN KEY (MemberId)        REFERENCES dbo.member_profiles(Id),
    CONSTRAINT FK_trainer_assignments_users            FOREIGN KEY (CreatedByUserId) REFERENCES dbo.users(Id),
    CONSTRAINT CK_trainer_assignments_Status CHECK (Status IN (1, 2)),
    CONSTRAINT CK_trainer_assignments_Date   CHECK (EndDate IS NULL OR EndDate >= StartDate),
    CONSTRAINT CK_trainer_assignments_EndDate_ByStatus
        CHECK ((Status = 1 AND EndDate IS NULL) OR (Status = 2 AND EndDate IS NOT NULL))
);
GO
-- Tối đa 1 PT Active / member (A-06).
CREATE UNIQUE INDEX UX_trainer_assignments_OneActivePerMember ON dbo.trainer_assignments(MemberId) WHERE Status = 1;
CREATE INDEX IX_trainer_assignments_TrainerId_Status ON dbo.trainer_assignments(TrainerId, Status);
CREATE INDEX IX_trainer_assignments_MemberId_Status  ON dbo.trainer_assignments(MemberId, Status);
GO

-- =========================================================
-- B6. exercise_catalog  (spec 005)
-- =========================================================
CREATE TABLE dbo.exercise_catalog (
    Id          BIGINT IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(150) NOT NULL,
    MuscleGroup NVARCHAR(80)  NULL,
    Description NVARCHAR(500) NULL,
    IsActive    BIT NOT NULL CONSTRAINT DF_exercise_catalog_IsActive DEFAULT 1,

    CONSTRAINT PK_exercise_catalog PRIMARY KEY (Id),
    CONSTRAINT UQ_exercise_catalog_Name UNIQUE (Name)
);
GO
CREATE INDEX IX_exercise_catalog_MuscleGroup ON dbo.exercise_catalog(MuscleGroup);
GO

-- =========================================================
-- B7. workout_plans  (spec 005)
-- Service PHẢI kiểm tra TrainerAssignment Active (ownership) trước khi PT tạo/sửa plan.
-- Trigger bên dưới chỉ là lưới an toàn (defense-in-depth).
-- =========================================================
CREATE TABLE dbo.workout_plans (
    Id        BIGINT IDENTITY(1,1) NOT NULL,
    MemberId  BIGINT        NOT NULL,
    TrainerId BIGINT        NOT NULL,
    Title     NVARCHAR(150) NOT NULL,
    Goal      NVARCHAR(255) NULL,
    StartDate DATE          NOT NULL,
    EndDate   DATE          NULL,
    Status    TINYINT       NOT NULL CONSTRAINT DF_workout_plans_Status DEFAULT 1,  -- 1 Active,2 Completed,3 Cancelled
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_workout_plans_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2     NULL,

    CONSTRAINT PK_workout_plans PRIMARY KEY (Id),
    CONSTRAINT FK_workout_plans_member_profiles  FOREIGN KEY (MemberId)  REFERENCES dbo.member_profiles(Id),
    CONSTRAINT FK_workout_plans_trainer_profiles FOREIGN KEY (TrainerId) REFERENCES dbo.trainer_profiles(Id),
    CONSTRAINT CK_workout_plans_Status CHECK (Status IN (1, 2, 3)),
    CONSTRAINT CK_workout_plans_Date   CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO
CREATE INDEX IX_workout_plans_MemberId_Status  ON dbo.workout_plans(MemberId, Status);
CREATE INDEX IX_workout_plans_TrainerId_Status ON dbo.workout_plans(TrainerId, Status);
GO

CREATE OR ALTER TRIGGER dbo.trg_workout_plans_RequireActiveAssignment
ON dbo.workout_plans
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.trainer_assignments ta
            WHERE ta.TrainerId = i.TrainerId
              AND ta.MemberId  = i.MemberId
              AND ta.Status = 1
        )
    )
    BEGIN
        RAISERROR(N'WorkoutPlan bị từ chối: PT chưa được phân công active cho member này.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

-- =========================================================
-- B8. workout_exercises  (spec 005) — N-N + đơn kê
-- (15_DATABASE_SCHEMA.md gọi bảng này là workout_exercises)
-- =========================================================
CREATE TABLE dbo.workout_exercises (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    WorkoutPlanId   BIGINT   NOT NULL,
    ExerciseId      BIGINT   NOT NULL,
    SortOrder       SMALLINT NOT NULL CONSTRAINT DF_workout_exercises_SortOrder DEFAULT 1,
    Sets            TINYINT  NULL,
    Reps            SMALLINT NULL,
    WeightKg        DECIMAL(6,2)  NULL,
    DurationMinutes SMALLINT NULL,
    RestSeconds     SMALLINT NULL,
    Note            NVARCHAR(255) NULL,

    CONSTRAINT PK_workout_exercises PRIMARY KEY (Id),
    CONSTRAINT FK_workout_exercises_workout_plans    FOREIGN KEY (WorkoutPlanId) REFERENCES dbo.workout_plans(Id) ON DELETE CASCADE,
    CONSTRAINT FK_workout_exercises_exercise_catalog FOREIGN KEY (ExerciseId)    REFERENCES dbo.exercise_catalog(Id),
    CONSTRAINT CK_workout_exercises_SortOrder       CHECK (SortOrder > 0),
    CONSTRAINT CK_workout_exercises_Sets            CHECK (Sets IS NULL OR Sets > 0),
    CONSTRAINT CK_workout_exercises_Reps            CHECK (Reps IS NULL OR Reps > 0),
    CONSTRAINT CK_workout_exercises_WeightKg        CHECK (WeightKg IS NULL OR WeightKg >= 0),
    CONSTRAINT CK_workout_exercises_DurationMinutes CHECK (DurationMinutes IS NULL OR DurationMinutes > 0),
    CONSTRAINT CK_workout_exercises_RestSeconds     CHECK (RestSeconds IS NULL OR RestSeconds >= 0),
    CONSTRAINT UQ_workout_exercises_Plan_Order      UNIQUE (WorkoutPlanId, SortOrder)
);
GO
CREATE INDEX IX_workout_exercises_WorkoutPlanId_SortOrder ON dbo.workout_exercises(WorkoutPlanId, SortOrder);
CREATE INDEX IX_workout_exercises_ExerciseId             ON dbo.workout_exercises(ExerciseId);
GO

-- =========================================================
-- B9. trainer_notes  (spec 005)
-- =========================================================
CREATE TABLE dbo.trainer_notes (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    TrainerId       BIGINT         NOT NULL,
    MemberId        BIGINT         NOT NULL,
    NoteDate        DATE           NOT NULL,
    Content         NVARCHAR(1000) NOT NULL,
    CreatedByUserId BIGINT         NULL,                 -- ai ghi note
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_trainer_notes_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2      NULL,

    CONSTRAINT PK_trainer_notes PRIMARY KEY (Id),
    CONSTRAINT FK_trainer_notes_trainer_profiles FOREIGN KEY (TrainerId)       REFERENCES dbo.trainer_profiles(Id),
    CONSTRAINT FK_trainer_notes_member_profiles  FOREIGN KEY (MemberId)        REFERENCES dbo.member_profiles(Id),
    CONSTRAINT FK_trainer_notes_users            FOREIGN KEY (CreatedByUserId) REFERENCES dbo.users(Id)
);
GO
CREATE INDEX IX_trainer_notes_MemberId_NoteDate  ON dbo.trainer_notes(MemberId, NoteDate DESC);
CREATE INDEX IX_trainer_notes_TrainerId_NoteDate ON dbo.trainer_notes(TrainerId, NoteDate DESC);
GO

-- =========================================================
-- B10. progress_logs  (spec 006)
-- =========================================================
CREATE TABLE dbo.progress_logs (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    MemberId        BIGINT    NOT NULL,
    MeasuredAt      DATETIME2 NOT NULL CONSTRAINT DF_progress_logs_MeasuredAt DEFAULT SYSUTCDATETIME(),
    WeightKg        DECIMAL(5,2) NULL,
    BodyFatPercent  DECIMAL(5,2) NULL,
    ChestCm         DECIMAL(5,2) NULL,
    WaistCm         DECIMAL(5,2) NULL,
    HipCm           DECIMAL(5,2) NULL,
    Note            NVARCHAR(500) NULL,
    CreatedByUserId BIGINT    NULL,                       -- ai đo: PT / Staff / Member tự nhập
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_progress_logs_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_progress_logs PRIMARY KEY (Id),
    CONSTRAINT FK_progress_logs_member_profiles FOREIGN KEY (MemberId)        REFERENCES dbo.member_profiles(Id),
    CONSTRAINT FK_progress_logs_users           FOREIGN KEY (CreatedByUserId) REFERENCES dbo.users(Id),
    CONSTRAINT CK_progress_logs_WeightKg       CHECK (WeightKg IS NULL OR WeightKg BETWEEN 20 AND 300),
    CONSTRAINT CK_progress_logs_BodyFatPercent CHECK (BodyFatPercent IS NULL OR BodyFatPercent BETWEEN 0 AND 70),
    CONSTRAINT CK_progress_logs_BodyMeasurements CHECK (
        (ChestCm IS NULL OR ChestCm BETWEEN 30 AND 200)
        AND (WaistCm IS NULL OR WaistCm BETWEEN 30 AND 200)
        AND (HipCm   IS NULL OR HipCm   BETWEEN 30 AND 200)
    )
);
GO
CREATE INDEX IX_progress_logs_MemberId_MeasuredAt ON dbo.progress_logs(MemberId, MeasuredAt DESC);
GO

-- =========================================================
-- B11. food_items  (spec 007)
-- =========================================================
CREATE TABLE dbo.food_items (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    Name            NVARCHAR(150) NOT NULL,
    Unit            NVARCHAR(30)  NOT NULL,                -- "100g", "1 quả", ...
    CaloriesPerUnit DECIMAL(8,2)  NOT NULL,
    ProteinG        DECIMAL(8,2)  NULL,
    CarbG           DECIMAL(8,2)  NULL,
    FatG            DECIMAL(8,2)  NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_food_items_IsActive DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_food_items_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_food_items PRIMARY KEY (Id),
    CONSTRAINT UQ_food_items_Name UNIQUE (Name),
    CONSTRAINT CK_food_items_Calories CHECK (CaloriesPerUnit >= 0),
    CONSTRAINT CK_food_items_Macros CHECK (
        (ProteinG IS NULL OR ProteinG >= 0)
        AND (CarbG IS NULL OR CarbG >= 0)
        AND (FatG  IS NULL OR FatG  >= 0)
    )
);
GO

-- =========================================================
-- B12. meal_logs / meal_log_items  (spec 007)
-- =========================================================
CREATE TABLE dbo.meal_logs (
    Id        BIGINT IDENTITY(1,1) NOT NULL,
    MemberId  BIGINT  NOT NULL,
    LogDate   DATE    NOT NULL,
    MealType  TINYINT NOT NULL,                           -- 1 Breakfast,2 Lunch,3 Dinner,4 Snack
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_meal_logs_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_meal_logs PRIMARY KEY (Id),
    CONSTRAINT FK_meal_logs_member_profiles FOREIGN KEY (MemberId) REFERENCES dbo.member_profiles(Id),
    CONSTRAINT CK_meal_logs_MealType CHECK (MealType IN (1, 2, 3, 4))
);
GO
CREATE INDEX IX_meal_logs_MemberId_LogDate ON dbo.meal_logs(MemberId, LogDate DESC);
GO

CREATE TABLE dbo.meal_log_items (
    Id         BIGINT IDENTITY(1,1) NOT NULL,
    MealLogId  BIGINT       NOT NULL,
    FoodItemId BIGINT       NOT NULL,
    Quantity   DECIMAL(8,2) NOT NULL,
    Calories   DECIMAL(8,2) NOT NULL,                     -- snapshot tại thời điểm lưu

    CONSTRAINT PK_meal_log_items PRIMARY KEY (Id),
    CONSTRAINT FK_meal_log_items_meal_logs  FOREIGN KEY (MealLogId)  REFERENCES dbo.meal_logs(Id) ON DELETE CASCADE,
    CONSTRAINT FK_meal_log_items_food_items FOREIGN KEY (FoodItemId) REFERENCES dbo.food_items(Id),
    CONSTRAINT CK_meal_log_items_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_meal_log_items_Calories CHECK (Calories >= 0)
);
GO
CREATE INDEX IX_meal_log_items_MealLogId  ON dbo.meal_log_items(MealLogId);
CREATE INDEX IX_meal_log_items_FoodItemId ON dbo.meal_log_items(FoodItemId);
GO

-- =========================================================
-- B13. calorie_targets  (spec 007 — secondary)
-- =========================================================
CREATE TABLE dbo.calorie_targets (
    Id            BIGINT IDENTITY(1,1) NOT NULL,
    MemberId      BIGINT       NOT NULL,
    EffectiveDate DATE         NOT NULL,
    DailyCalories DECIMAL(8,2) NOT NULL,
    ProteinG      DECIMAL(8,2) NULL,
    CarbG         DECIMAL(8,2) NULL,
    FatG          DECIMAL(8,2) NULL,
    CreatedAt     DATETIME2 NOT NULL CONSTRAINT DF_calorie_targets_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_calorie_targets PRIMARY KEY (Id),
    CONSTRAINT FK_calorie_targets_member_profiles FOREIGN KEY (MemberId) REFERENCES dbo.member_profiles(Id),
    CONSTRAINT UQ_calorie_targets_Member_Date UNIQUE (MemberId, EffectiveDate),
    CONSTRAINT CK_calorie_targets_DailyCalories CHECK (DailyCalories > 0)
);
GO
CREATE INDEX IX_calorie_targets_MemberId_EffectiveDate ON dbo.calorie_targets(MemberId, EffectiveDate DESC);
GO

-- =========================================================
-- B14. VIEWS (WITH SCHEMABINDING) — phục vụ dashboard (spec 008)
-- Lưu ý: schema mới KHÔNG có MemberCode -> view dùng member_profiles.Id + users.*.
-- =========================================================
CREATE VIEW dbo.vw_active_member_subscriptions WITH SCHEMABINDING AS
SELECT
    ms.Id AS MembershipId, mp.Id AS MemberId,
    u.FullName AS MemberName, u.Email, u.Phone,
    pkg.Name AS PackageName, ms.StartDate, ms.EndDate,
    DATEDIFF(DAY, CONVERT(DATE, SYSUTCDATETIME()), ms.EndDate) AS DaysRemaining
FROM dbo.memberships ms
INNER JOIN dbo.member_profiles mp     ON ms.MemberId  = mp.Id
INNER JOIN dbo.users u                ON mp.UserId    = u.Id
INNER JOIN dbo.membership_packages pkg ON ms.PackageId = pkg.Id
WHERE ms.Status = 1;
GO

CREATE VIEW dbo.vw_trainer_active_members WITH SCHEMABINDING AS
SELECT
    ta.Id AS AssignmentId, tp.Id AS TrainerId, trainer.FullName AS TrainerName,
    mp.Id AS MemberId, member.FullName AS MemberName, ta.StartDate
FROM dbo.trainer_assignments ta
INNER JOIN dbo.trainer_profiles tp ON ta.TrainerId = tp.Id
INNER JOIN dbo.users trainer       ON tp.UserId    = trainer.Id
INNER JOIN dbo.member_profiles mp  ON ta.MemberId  = mp.Id
INNER JOIN dbo.users member        ON mp.UserId    = member.Id
WHERE ta.Status = 1;
GO

CREATE VIEW dbo.vw_member_daily_calories WITH SCHEMABINDING AS
SELECT ml.MemberId, ml.LogDate, SUM(mli.Calories) AS TotalCalories, COUNT_BIG(*) AS ItemCount
FROM dbo.meal_logs ml
INNER JOIN dbo.meal_log_items mli ON ml.Id = mli.MealLogId
GROUP BY ml.MemberId, ml.LogDate;
GO

-- =========================================================
-- B15. PROC — job hết hạn membership (spec 003/008)
-- Backend gọi 1 lần/ngày qua BackgroundService:
--   await db.Database.ExecuteSqlRawAsync("EXEC dbo.usp_ExpireMemberships");
-- LƯU Ý: audit_logs.EntityId NOT NULL -> dùng 0 cho hành động hệ thống (batch).
-- =========================================================
CREATE OR ALTER PROCEDURE dbo.usp_ExpireMemberships
AS
BEGIN
    SET NOCOUNT ON;
    -- "hôm nay" theo giờ VN (EndDate là mốc ngày địa phương của phòng gym).
    DECLARE @todayVN DATE = CONVERT(DATE, SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time');

    UPDATE dbo.memberships
       SET Status = 2, UpdatedAt = SYSUTCDATETIME()       -- 2 = Expired
     WHERE Status = 1 AND EndDate < @todayVN;

    DECLARE @n INT = @@ROWCOUNT;
    IF @n > 0
        INSERT INTO dbo.audit_logs (UserId, Action, Entity, EntityId, Metadata)
        VALUES (NULL, N'EXPIRE_MEMBERSHIP', N'memberships', 0,
                CONCAT(N'{"expiredCount":', @n, ',"runAtUtc":"',
                       CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126), N'"}'));
END;
GO


/* =============================================================================
   ██  TEST QUERIES  (kiểm tra nhanh; phần B chạy được sau khi có dữ liệu)
   ============================================================================= */

-- A. Users + role (qua user_roles)
SELECT u.Id, u.FullName, u.Email, u.Phone, r.Name AS RoleName, u.Status
FROM dbo.users u
LEFT JOIN dbo.user_roles ur ON ur.UserId = u.Id
LEFT JOIN dbo.roles r       ON r.Id = ur.RoleId
WHERE u.IsDeleted = 0
ORDER BY u.Id;
GO

-- B. Memberships đang active
SELECT * FROM dbo.vw_active_member_subscriptions ORDER BY EndDate;
GO

-- C. Doanh thu (chỉ Paid)
SELECT COUNT(*) AS PaidPaymentCount, SUM(Amount) AS TotalRevenue
FROM dbo.payments WHERE Status = 1;
GO

-- D. Check-in theo ngày (dùng cột persisted CheckInDate)
SELECT CheckInDate, COUNT(*) AS TotalCheckIns
FROM dbo.check_ins GROUP BY CheckInDate ORDER BY CheckInDate DESC;
GO

