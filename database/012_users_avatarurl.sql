-- =============================================================================
-- 012_users_avatarurl.sql  (Account self-service + Avatar)
-- Them cot AvatarUrl vao users de luu anh dai dien tai khoan.
-- Idempotent. Chay tren DB GymMaster.
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d GymMaster -E -C -i 012_users_avatarurl.sql
-- =============================================================================
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.users', 'AvatarUrl') IS NULL
    ALTER TABLE dbo.users ADD AvatarUrl NVARCHAR(500) NULL;
GO
