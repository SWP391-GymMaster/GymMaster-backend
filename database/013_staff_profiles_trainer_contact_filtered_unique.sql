IF OBJECT_ID('dbo.staff_profiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.staff_profiles (
        Id               BIGINT IDENTITY(1,1) NOT NULL,
        UserId           BIGINT        NOT NULL,
        DateOfBirth      DATETIME2     NULL,
        Gender           NVARCHAR(20)  NULL,
        Address          NVARCHAR(255) NULL,
        EmergencyContact NVARCHAR(100) NULL,
        IsDeleted        BIT           NOT NULL CONSTRAINT DF_staff_profiles_IsDeleted DEFAULT 0,
        CreatedAt        DATETIME2     NOT NULL CONSTRAINT DF_staff_profiles_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2     NOT NULL CONSTRAINT DF_staff_profiles_UpdatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_staff_profiles PRIMARY KEY (Id),
        CONSTRAINT FK_staff_profiles_users FOREIGN KEY (UserId) REFERENCES dbo.users(Id) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_staff_profiles_UserId ON dbo.staff_profiles(UserId);
END;
GO

IF COL_LENGTH('dbo.trainer_profiles', 'Address') IS NULL
BEGIN
    ALTER TABLE dbo.trainer_profiles ADD Address NVARCHAR(255) NULL;
END;
GO

IF COL_LENGTH('dbo.trainer_profiles', 'EmergencyContact') IS NULL
BEGIN
    ALTER TABLE dbo.trainer_profiles ADD EmergencyContact NVARCHAR(100) NULL;
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_users_Email'
      AND object_id = OBJECT_ID('dbo.users')
)
BEGIN
    DROP INDEX IX_users_Email ON dbo.users;
END;
GO

CREATE UNIQUE INDEX IX_users_Email ON dbo.users(Email) WHERE IsDeleted = 0;
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_users_Phone'
      AND object_id = OBJECT_ID('dbo.users')
)
BEGIN
    DROP INDEX IX_users_Phone ON dbo.users;
END;
GO

CREATE UNIQUE INDEX IX_users_Phone ON dbo.users(Phone) WHERE Phone IS NOT NULL AND IsDeleted = 0;
GO
