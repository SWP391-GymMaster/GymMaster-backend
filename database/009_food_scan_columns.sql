-- =============================================================================
-- 009_food_scan_columns.sql  (tinh nang Quet anh AI)
-- Them cot ServingSize + Source vao food_items de luu mon AI (Source='AI', ServingSize=100).
-- Idempotent. Chay tren DB GymMaster.
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d GymMaster -E -C -i 009_food_scan_columns.sql
-- =============================================================================
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.food_items', 'ServingSize') IS NULL
    ALTER TABLE dbo.food_items ADD ServingSize DECIMAL(8,2) NOT NULL
        CONSTRAINT DF_food_items_ServingSize DEFAULT 100;
GO

IF COL_LENGTH('dbo.food_items', 'Source') IS NULL
    ALTER TABLE dbo.food_items ADD Source NVARCHAR(20) NOT NULL
        CONSTRAINT DF_food_items_Source DEFAULT N'Admin';
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_food_items_Source')
    ALTER TABLE dbo.food_items WITH CHECK ADD CONSTRAINT CK_food_items_Source
        CHECK (Source IN (N'Admin', N'Manual', N'AI', N'API'));
GO
