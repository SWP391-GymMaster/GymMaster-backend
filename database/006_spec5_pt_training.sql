/* =============================================================================
   006_spec5_pt_training.sql  —  Spec 005 PT Training (assignment / workout / note)
   -----------------------------------------------------------------------------
   Thêm 5 bảng spec 005 còn THIẾU để backend (merge-final) + Dashboard chạy được:
     trainer_assignments, exercise_catalog, workout_plans, workout_exercises, trainer_notes
   Idempotent: chỉ tạo khi bảng chưa tồn tại (IF OBJECT_ID ... IS NULL).
   KHÔNG DROP, KHÔNG đụng bảng đang có data.
   DDL khớp đúng entity + GymMasterDbContext trên nhánh merge-final.
   ============================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- 1) trainer_assignments (PT <-> Member theo thời gian)
IF OBJECT_ID('dbo.trainer_assignments', 'U') IS NULL
BEGIN
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
    CREATE UNIQUE INDEX UX_trainer_assignments_OneActivePerMember ON dbo.trainer_assignments(MemberId) WHERE Status = 1;
    CREATE INDEX IX_trainer_assignments_TrainerId_Status ON dbo.trainer_assignments(TrainerId, Status);
    CREATE INDEX IX_trainer_assignments_MemberId_Status  ON dbo.trainer_assignments(MemberId, Status);
    PRINT 'Đã tạo bảng dbo.trainer_assignments.';
END
ELSE PRINT 'Bỏ qua trainer_assignments (đã có).';
GO

-- 2) exercise_catalog
IF OBJECT_ID('dbo.exercise_catalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.exercise_catalog (
        Id          BIGINT IDENTITY(1,1) NOT NULL,
        Name        NVARCHAR(150) NOT NULL,
        MuscleGroup NVARCHAR(80)  NULL,
        Description NVARCHAR(500) NULL,
        IsActive    BIT NOT NULL CONSTRAINT DF_exercise_catalog_IsActive DEFAULT 1,
        CONSTRAINT PK_exercise_catalog PRIMARY KEY (Id),
        CONSTRAINT UQ_exercise_catalog_Name UNIQUE (Name)
    );
    CREATE INDEX IX_exercise_catalog_MuscleGroup ON dbo.exercise_catalog(MuscleGroup);
    PRINT 'Đã tạo bảng dbo.exercise_catalog.';
END
ELSE PRINT 'Bỏ qua exercise_catalog (đã có).';
GO

-- 3) workout_plans (+ trigger lưới an toàn: PT phải có assignment Active)
IF OBJECT_ID('dbo.workout_plans', 'U') IS NULL
BEGIN
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
    CREATE INDEX IX_workout_plans_MemberId_Status  ON dbo.workout_plans(MemberId, Status);
    CREATE INDEX IX_workout_plans_TrainerId_Status ON dbo.workout_plans(TrainerId, Status);
    PRINT 'Đã tạo bảng dbo.workout_plans.';
END
ELSE PRINT 'Bỏ qua workout_plans (đã có).';
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

-- 4) workout_exercises
IF OBJECT_ID('dbo.workout_exercises', 'U') IS NULL
BEGIN
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
    CREATE INDEX IX_workout_exercises_WorkoutPlanId_SortOrder ON dbo.workout_exercises(WorkoutPlanId, SortOrder);
    CREATE INDEX IX_workout_exercises_ExerciseId             ON dbo.workout_exercises(ExerciseId);
    PRINT 'Đã tạo bảng dbo.workout_exercises.';
END
ELSE PRINT 'Bỏ qua workout_exercises (đã có).';
GO

-- 5) trainer_notes
IF OBJECT_ID('dbo.trainer_notes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.trainer_notes (
        Id              BIGINT IDENTITY(1,1) NOT NULL,
        TrainerId       BIGINT         NOT NULL,
        MemberId        BIGINT         NOT NULL,
        NoteDate        DATE           NOT NULL,
        Content         NVARCHAR(1000) NOT NULL,
        CreatedByUserId BIGINT         NULL,
        CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_trainer_notes_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2      NULL,
        CONSTRAINT PK_trainer_notes PRIMARY KEY (Id),
        CONSTRAINT FK_trainer_notes_trainer_profiles FOREIGN KEY (TrainerId)       REFERENCES dbo.trainer_profiles(Id),
        CONSTRAINT FK_trainer_notes_member_profiles  FOREIGN KEY (MemberId)        REFERENCES dbo.member_profiles(Id),
        CONSTRAINT FK_trainer_notes_users            FOREIGN KEY (CreatedByUserId) REFERENCES dbo.users(Id)
    );
    CREATE INDEX IX_trainer_notes_MemberId_NoteDate  ON dbo.trainer_notes(MemberId, NoteDate DESC);
    CREATE INDEX IX_trainer_notes_TrainerId_NoteDate ON dbo.trainer_notes(TrainerId, NoteDate DESC);
    PRINT 'Đã tạo bảng dbo.trainer_notes.';
END
ELSE PRINT 'Bỏ qua trainer_notes (đã có).';
GO
