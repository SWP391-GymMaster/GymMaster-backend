/* =============================================================================
   011_fix_check_ins_createdby_column.sql  —  Spec 004 / 005 (Check-in)
   -----------------------------------------------------------------------------
   SUA LECH SCHEMA (schema drift):
   Mot so DB cu tao bang dbo.check_ins voi cot ten `CreatedByUserId`, trong khi
   code (Entities/CheckIn.cs + Data/GymMasterDbContext.cs) va schema chuan
   (004_checkin.sql, GymMaster_SQLServer_Final.sql) deu dung ten `CreatedBy`.

   => EF Core sinh SQL tham chieu cot `CreatedBy` -> loi:
        "Invalid column name 'CreatedBy'."
      khien MOI luong check-in (Staff / Member tu check-in / PT check-in) tra ve
      HTTP 500 (FE bao "GymMaster tra ve phan hoi khong hop le").

   File nay doi ten cot `CreatedByUserId` -> `CreatedBy` cho khop code.
   - sp_rename giu nguyen FK + index dang tham chieu cot (chung tro theo column-id,
     khong theo ten) nen KHONG can drop/recreate gi.
   - Idempotent + an toan: chi doi ten khi that su con lech.
     DB da dung chuan (da co cot CreatedBy) -> tu dong bo qua.

   CACH CHAY: chon dung database GymMasterDb trong SSMS roi F5 (hoac):
     sqlcmd -S "." -d GymMasterDb -E -C -i 011_fix_check_ins_createdby_column.sql
   Chay tren DB dang co data, KHONG can rebuild / khong mat du lieu check-in cu.
   ============================================================================= */

SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.check_ins', 'U') IS NULL
BEGIN
    PRINT 'BO QUA: chua co bang dbo.check_ins. Hay chay 004_checkin.sql truoc.';
END
ELSE IF COL_LENGTH('dbo.check_ins', 'CreatedBy') IS NOT NULL
BEGIN
    PRINT 'OK: cot check_ins.CreatedBy da ton tai — khong can sua.';
END
ELSE IF COL_LENGTH('dbo.check_ins', 'CreatedByUserId') IS NOT NULL
BEGIN
    EXEC sp_rename 'dbo.check_ins.CreatedByUserId', 'CreatedBy', 'COLUMN';
    PRINT 'DA doi ten cot check_ins.CreatedByUserId -> CreatedBy.';
END
ELSE
BEGIN
    -- Truong hop hiem: thieu ca hai cot -> them moi cho khop code (NULL = member tu check-in).
    ALTER TABLE dbo.check_ins ADD CreatedBy BIGINT NULL
        CONSTRAINT FK_check_ins_users FOREIGN KEY (CreatedBy) REFERENCES dbo.users(Id);
    PRINT 'DA them cot check_ins.CreatedBy (BIGINT NULL).';
END
GO
