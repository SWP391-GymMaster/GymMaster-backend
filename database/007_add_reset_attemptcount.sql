/* =============================================================================
   007_add_reset_attemptcount.sql  —  Spec 001 (OTP reset hardening)
   -----------------------------------------------------------------------------
   Them cot AttemptCount vao password_reset_tokens (dem so lan nhap OTP sai,
   qua 3 lan -> vo hieu ma). Idempotent: chi them neu chua co.
   Chay tren DB dang co data (khong can rebuild). Chon dung GymMasterDb roi F5.
   ============================================================================= */

IF COL_LENGTH('dbo.password_reset_tokens', 'AttemptCount') IS NULL
BEGIN
    ALTER TABLE dbo.password_reset_tokens
        ADD AttemptCount INT NOT NULL
        CONSTRAINT DF_password_reset_tokens_AttemptCount DEFAULT 0;
    PRINT 'Da them cot AttemptCount vao password_reset_tokens.';
END
ELSE
    PRINT 'Bo qua: cot AttemptCount da ton tai.';
GO
