/* =====================================================================
   005_sync_missing_tables.sql
   Tao cac bang spec-003/006/007 con THIEU tren DB (membership / payment /
   progress / nutrition) de backend main chay khong con loi 500.

   - Idempotent: chi tao khi bang chua ton tai (IF OBJECT_ID ... IS NULL).
   - KHONG DROP, KHONG dung toi users/member_profiles/check_ins dang co data.
   - Cot/kieu khop dung entity + GymMasterDbContext mapping tren main.

   Chay: sqlcmd -S "(localdb)\MSSQLLocalDB" -d GymMaster -i 005_sync_missing_tables.sql
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 1) membership_packages (spec 003)
IF OBJECT_ID('dbo.membership_packages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.membership_packages (
        Id           BIGINT        IDENTITY(1,1) NOT NULL,
        Name         NVARCHAR(100) NOT NULL,
        Description  NVARCHAR(500) NULL,
        DurationDays SMALLINT      NOT NULL,
        Price        DECIMAL(12,2) NOT NULL,
        IsActive     BIT           NOT NULL,
        CreatedAt    DATETIME2     NOT NULL,
        UpdatedAt    DATETIME2     NULL,
        CONSTRAINT PK_membership_packages PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_membership_packages_Name ON dbo.membership_packages(Name);
    PRINT 'Created dbo.membership_packages';
END

-- 2) memberships (spec 003) — FK PackageId -> membership_packages
IF OBJECT_ID('dbo.memberships', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.memberships (
        Id              BIGINT    IDENTITY(1,1) NOT NULL,
        MemberId        BIGINT    NOT NULL,
        PackageId       BIGINT    NOT NULL,
        StartDate       DATE      NOT NULL,
        EndDate         DATE      NOT NULL,
        Status          TINYINT   NOT NULL,
        CreatedByUserId BIGINT    NOT NULL,
        CreatedAt       DATETIME2 NOT NULL,
        UpdatedAt       DATETIME2 NULL,
        CONSTRAINT PK_memberships PRIMARY KEY (Id),
        CONSTRAINT FK_memberships_packages FOREIGN KEY (PackageId) REFERENCES dbo.membership_packages(Id)
    );
    CREATE INDEX IX_memberships_MemberId ON dbo.memberships(MemberId);
    PRINT 'Created dbo.memberships';
END
ELSE
BEGIN
    -- DB da co bang memberships cu (vd tu block demo trong 004_checkin.sql):
    -- bo sung cot/kieu con thieu cho khop entity (CreatedByUserId, Status tinyint).
    IF COL_LENGTH('dbo.memberships', 'CreatedByUserId') IS NULL
        ALTER TABLE dbo.memberships ADD CreatedByUserId BIGINT NOT NULL
            CONSTRAINT DF_memberships_CreatedByUserId DEFAULT 0;

    IF EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.memberships')
                 AND name = 'Status' AND system_type_id = TYPE_ID('int'))
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_memberships_MemberId_Status'
                     AND object_id = OBJECT_ID('dbo.memberships'))
            DROP INDEX IX_memberships_MemberId_Status ON dbo.memberships;
        ALTER TABLE dbo.memberships ALTER COLUMN Status TINYINT NOT NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_memberships_MemberId'
                     AND object_id = OBJECT_ID('dbo.memberships'))
        CREATE INDEX IX_memberships_MemberId ON dbo.memberships(MemberId);
    PRINT 'Patched existing dbo.memberships';
END

-- 3) payments (spec 003)
IF OBJECT_ID('dbo.payments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.payments (
        Id              BIGINT        IDENTITY(1,1) NOT NULL,
        MembershipId    BIGINT        NOT NULL,
        Amount          DECIMAL(12,2) NOT NULL,
        PaymentMethod   TINYINT       NOT NULL,
        Status          TINYINT       NOT NULL,
        PaidAt          DATETIME2     NULL,
        CreatedByUserId BIGINT        NOT NULL,
        CreatedAt       DATETIME2     NOT NULL,
        UpdatedAt       DATETIME2     NULL,
        CONSTRAINT PK_payments PRIMARY KEY (Id)
    );
    CREATE INDEX IX_payments_MembershipId ON dbo.payments(MembershipId);
    PRINT 'Created dbo.payments';
END

-- 4) progress_logs (spec 006)
IF OBJECT_ID('dbo.progress_logs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.progress_logs (
        Id              BIGINT      IDENTITY(1,1) NOT NULL,
        MemberId        BIGINT      NOT NULL,
        MeasuredAt      DATETIME2   NOT NULL,
        WeightKg        DECIMAL(5,2) NULL,
        BodyFatPercent  DECIMAL(5,2) NULL,
        ChestCm         DECIMAL(5,2) NULL,
        WaistCm         DECIMAL(5,2) NULL,
        HipCm           DECIMAL(5,2) NULL,
        Note            NVARCHAR(500) NULL,
        CreatedByUserId BIGINT      NULL,
        CreatedAt       DATETIME2   NOT NULL,
        CONSTRAINT PK_progress_logs PRIMARY KEY (Id)
    );
    CREATE INDEX IX_progress_logs_MemberId_MeasuredAt ON dbo.progress_logs(MemberId, MeasuredAt);
    PRINT 'Created dbo.progress_logs';
END

-- 5) food_items (spec 007)
IF OBJECT_ID('dbo.food_items', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.food_items (
        Id              BIGINT        IDENTITY(1,1) NOT NULL,
        Name            NVARCHAR(150) NOT NULL,
        Unit            NVARCHAR(30)  NOT NULL,
        CaloriesPerUnit DECIMAL(8,2)  NOT NULL,
        ProteinG        DECIMAL(8,2)  NULL,
        CarbG           DECIMAL(8,2)  NULL,
        FatG            DECIMAL(8,2)  NULL,
        IsActive        BIT           NOT NULL,
        CreatedAt       DATETIME2     NOT NULL,
        CONSTRAINT PK_food_items PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_food_items_Name ON dbo.food_items(Name);
    PRINT 'Created dbo.food_items';
END

-- 6) meal_logs (spec 007)
IF OBJECT_ID('dbo.meal_logs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.meal_logs (
        Id        BIGINT    IDENTITY(1,1) NOT NULL,
        MemberId  BIGINT    NOT NULL,
        LogDate   DATE      NOT NULL,
        MealType  TINYINT   NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT PK_meal_logs PRIMARY KEY (Id)
    );
    CREATE INDEX IX_meal_logs_MemberId_LogDate ON dbo.meal_logs(MemberId, LogDate);
    PRINT 'Created dbo.meal_logs';
END

-- 7) meal_log_items (spec 007) — FK -> meal_logs, food_items
IF OBJECT_ID('dbo.meal_log_items', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.meal_log_items (
        Id         BIGINT       IDENTITY(1,1) NOT NULL,
        MealLogId  BIGINT       NOT NULL,
        FoodItemId BIGINT       NOT NULL,
        Quantity   DECIMAL(8,2) NOT NULL,
        Calories   DECIMAL(8,2) NOT NULL,
        CONSTRAINT PK_meal_log_items PRIMARY KEY (Id),
        CONSTRAINT FK_meal_log_items_meal_logs  FOREIGN KEY (MealLogId)  REFERENCES dbo.meal_logs(Id),
        CONSTRAINT FK_meal_log_items_food_items FOREIGN KEY (FoodItemId) REFERENCES dbo.food_items(Id)
    );
    PRINT 'Created dbo.meal_log_items';
END

-- 8) calorie_targets (spec 007)
IF OBJECT_ID('dbo.calorie_targets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.calorie_targets (
        Id            BIGINT       IDENTITY(1,1) NOT NULL,
        MemberId      BIGINT       NOT NULL,
        EffectiveDate DATE         NOT NULL,
        DailyCalories DECIMAL(8,2) NOT NULL,
        ProteinG      DECIMAL(8,2) NULL,
        CarbG         DECIMAL(8,2) NULL,
        FatG          DECIMAL(8,2) NULL,
        CreatedAt     DATETIME2    NOT NULL,
        CONSTRAINT PK_calorie_targets PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_calorie_targets_MemberId_EffectiveDate ON dbo.calorie_targets(MemberId, EffectiveDate);
    PRINT 'Created dbo.calorie_targets';
END

PRINT 'Done: schema sync complete.';
