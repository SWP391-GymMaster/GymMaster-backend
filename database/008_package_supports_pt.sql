/* =============================================================================
   008_package_supports_pt.sql
   Them thuoc tinh "goi tap co ho tro PT hay khong" vao bang membership_packages.
   -----------------------------------------------------------------------------
   TAC GIA: Team DB (BAN). Phan nay CHI sua DATABASE. Code C# do nguoi phu trach
            module Goi tap / PT tu lam (xem muc "DANH CHO NGUOI VIET CODE" ben duoi).

   ---------------------------------------------------------------------------
   1) BOI CANH NGHIEP VU
   ---------------------------------------------------------------------------
   - Khi nguoi dung dang ky tai khoan -> mac dinh la Member (hoi vien thuong),
     KHONG co PT. Tai khoan KHONG luu co "co PT / khong PT".
   - Viec hoi vien co duoc dung dich vu PT (Personal Trainer) hay khong KHONG cố
     dinh o tai khoan, ma PHU THUOC VAO GOI TAP dang dang ky va con hieu luc:
       + Goi khong ho tro PT  -> chi duoc tap thuong.
       + Goi co ho tro PT     -> duoc dat lich / tap voi PT.
       + Chua mua goi hoac goi het han -> KHONG duoc dung PT.
   - Nho vay khi hoi vien nang cap / doi goi / het han goi, quyen PT tu thay doi
     theo goi dang hieu luc, KHONG bi sai/lech du lieu.

   ---------------------------------------------------------------------------
   2) CAI CU THIEU GI (truoc khi co script nay)
   ---------------------------------------------------------------------------
   Bang membership_packages cu chi co:
       Id, Name, Description, DurationDays, Price, IsActive, CreatedAt, UpdatedAt
   => KHONG co cot nao cho biet goi do co PT hay khong. Vi vay he thong khong the
      phan biet "goi thuong" voi "goi co PT" -> hoi vien thuong van co the duoc
      dung dich vu PT (sai nghiep vu). Day la van de can fix.

   ---------------------------------------------------------------------------
   3) SCRIPT NAY THEM GI
   ---------------------------------------------------------------------------
   Them DUNG 1 cot vao membership_packages:
       SupportsPT  BIT  NOT NULL  DEFAULT 0
         - 0 = goi KHONG ho tro PT (mac dinh, an toan cho cac goi cu).
         - 1 = goi CO ho tro PT.
   KHONG them cot nao o users / member_profiles / memberships
   (vi quyen PT la suy ra dong tu goi, khong luu o tai khoan).

   An toan:
     - Idempotent: chay lai nhieu lan khong loi (chi them khi cot chua ton tai).
     - Additive: khong DROP, khong sua du lieu cu. Cac goi cu tu dong = 0 (khong PT).
   ============================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.membership_packages', 'SupportsPT') IS NULL
BEGIN
    ALTER TABLE dbo.membership_packages
        ADD SupportsPT BIT NOT NULL CONSTRAINT DF_membership_packages_SupportsPT DEFAULT 0;
    PRINT 'Da them cot membership_packages.SupportsPT (0 = khong PT, 1 = co PT).';
END
ELSE PRINT 'Bo qua: cot membership_packages.SupportsPT da ton tai.';
GO

/* =============================================================================
   DANH CHO NGUOI VIET CODE (module Goi tap / PT) + AI ho tro
   -----------------------------------------------------------------------------
   a) ENTITY: them property vao MembershipPackage (EF tu map bool <-> bit):
          public bool SupportsPT { get; set; }

   b) ADMIN THEM/SUA GOI: cho phep set SupportsPT (true/false) trong
      CreatePackageRequest / UpdatePackageRequest va luu xuong cot nay.
      => Day la luc admin quyet dinh goi do "co PT hay khong".

   c) QUY TAC XAC DINH HOI VIEN CO DUOC DUNG PT (suy ra, KHONG luu san):
        Hoi vien duoc dung PT  <=>  ton tai 1 membership cua ho thoa:
            Status = 1 (Active)
            AND EndDate >= ngay hien tai
            AND Package.SupportsPT = 1
      Vi du truy van kiem tra 1 member (memberProfileId):
        SELECT CASE WHEN EXISTS (
            SELECT 1
            FROM memberships m
            JOIN membership_packages p ON p.Id = m.PackageId
            WHERE m.MemberId = @memberProfileId
              AND m.Status = 1
              AND m.EndDate >= CAST(GETUTCDATE() AS date)
              AND p.SupportsPT = 1
        ) THEN 1 ELSE 0 END AS DuocDungPT;

   d) AP DUNG QUY TAC NAY TAI CAC CHO CAN GAC PT, vi du:
        - Khi gan PT cho hoi vien / hoi vien dat lich voi PT:
          neu khong thoa quy tac (c) -> tu choi (vi du tra 422 NO_PT_PACKAGE).
        - Goi het han thi (c) tu dong false -> mat quyen PT, khong can lam gi them.

   GHI CHU: cot SupportsPT chi la DU LIEU. Viec CHAN hoi vien thuong dung PT phai
   nam o CODE (kiem tra theo quy tac (c)); chi them cot khong tu chan duoc.
   ============================================================================= */
