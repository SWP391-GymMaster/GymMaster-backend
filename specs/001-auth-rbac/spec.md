# Feature Specification: Authentication & Role-Based Access Control

**Feature Branch**: `001-auth-rbac`  
**Created**: 2026-05-30  
**Status**: Approved  
**Spec style**: SDD + Spec Kit - 9 components, EARS notation  
**Source**: 03_SRS (UC-01, UC-02), 04_REQUIREMENTS (FR-AUTH, FR-RBAC), CONSTITUTION SEC-01..05

> **EARS legend:** Ubiquitous `THE system SHALL ...` · Event `WHEN <trigger>, THE system SHALL ...` · State `WHILE <state>, THE system SHALL ...` · Optional `WHERE <feature>, THE system SHALL ...` · Unwanted `IF <condition>, THEN THE system SHALL ...`

---

## 1. Context & Goal
Moi truy cap GymMaster phai duoc xac thuc va phan quyen theo 4 role (Admin, Staff, PT, Member). Day la feature nen tang: moi feature khac phu thuoc identity + role lay tu token. Muc tieu: dang nhap an toan (BCrypt + JWT), chong privilege escalation, chong brute-force, va ho tro cac luong auth can thiet cho do an.

**In Scope:**
- Login bang email/password (JWT access token 15 phut, refresh token 7 ngay, rotate refresh token, logout revoke).
- Register tai khoan Member (self-service).
- Forgot password / Reset password qua reset token.
- Change password khi da dang nhap.
- Google login bang Google ID token.
- RBAC theo 4 role; chong brute-force; chong user enumeration.

**Base path:** `/api/v1/auth`  
**Response format:** `{ success, data, error, meta }`

## 2. Actors
| Actor | Vai tro trong feature |
|---|---|
| Admin | Dang nhap; co toan quyen. |
| Staff | Dang nhap; quyen van hanh quay. |
| PT | Dang nhap; quyen tren member duoc phan cong. |
| Member | Dang nhap; quyen tren du lieu cua minh. |
| System | Phat hanh/thu hoi token, kiem tra quyen, ghi security event khi co audit module. |

## 3. Functional Requirements (EARS)
- **FR-AUTH-01 (Ubiquitous):** THE system SHALL luu mat khau duoi dang bam BCrypt voi cost factor >= 12.
- **FR-AUTH-02 (Event):** WHEN nguoi dung gui email + mat khau dung, THE system SHALL phat hanh access token (TTL 15 phut) va refresh token (TTL 7 ngay).
- **FR-AUTH-03 (Unwanted):** IF mot tai khoan dang nhap sai > 5 lan trong 15 phut, THEN THE system SHALL khoa tam tai khoan do 15 phut. WHERE audit/security-event module da co, THE system SHALL ghi security event.
- **FR-AUTH-04 (Unwanted):** IF email khong ton tai hoac mat khau sai, THEN THE system SHALL tra thong bao loi GIONG NHAU cho ca hai truong hop (chong user enumeration).
- **FR-AUTH-05 (Event):** WHEN access token het han va client gui refresh token hop le chua thu hoi, THE system SHALL phat hanh access token moi va xoay (rotate) refresh token.
- **FR-AUTH-06 (Event):** WHEN nguoi dung logout, THE system SHALL thu hoi refresh token hien tai (set RevokedAt).
- **FR-AUTH-07 (State):** WHILE tai khoan o trang thai Locked hoac dang bi khoa tam, THE system SHALL tu choi dang nhap kem thong bao trang thai khoa.
- **FR-AUTH-08 (Event):** WHEN nguoi dung dang ky voi email chua ton tai va du lieu hop le, THE system SHALL tao tai khoan role Member, bam mat khau BCrypt cost factor >= 12, va phat hanh access token + refresh token.
- **FR-AUTH-09 (Unwanted):** IF email hoac phone da ton tai khi dang ky, THEN THE system SHALL tra 409 Conflict va KHONG tao tai khoan.
- **FR-AUTH-10 (Event):** WHEN nguoi dung yeu cau quen mat khau bang email, THE system SHALL tao password reset token co han dung 30 phut va luu token duoi dang bam. WHERE moi truong development, THE system MAY tra `resetToken` de test; WHERE moi truong production, THE system SHALL gui token qua email khi email service duoc cau hinh.
- **FR-AUTH-11 (Event):** WHEN nguoi dung gui reset token hop le kem mat khau moi, THE system SHALL cap nhat password hash moi, thu hoi refresh token cu, va danh dau reset token da dung (set UsedAt).
- **FR-AUTH-12 (Event):** WHEN nguoi dung da dang nhap doi mat khau va cung cap dung mat khau hien tai, THE system SHALL cap nhat password hash moi va thu hoi refresh token cu.
- **FR-AUTH-13 (Event):** WHEN nguoi dung dang nhap bang Google ID token hop le, THE system SHALL xac thuc token voi Google ClientId, tao user role Member neu chua ton tai, va phat hanh access token + refresh token.
- **FR-RBAC-01 (Ubiquitous):** THE system SHALL lay `userId` va `role` tu JWT claim, KHONG BAO GIO tu request body hoac query.
- **FR-RBAC-02 (Unwanted):** IF nguoi dung goi endpoint khong thuoc quyen role cua minh, THEN THE system SHALL tra 403 va KHONG thuc hien hanh dong.
- **FR-RBAC-03 (Ubiquitous):** THE system SHALL yeu cau access token hop le cho moi endpoint mutating (POST/PUT/PATCH/DELETE) tru cac public auth endpoint duoc danh dau ro.

## 4. Non-functional Requirements
- **NFR-01 (Bao mat):** Token ky HS256/RS256; secret lay tu env/User-Secrets hoac cau hinh moi truong. Khong log token/mat khau.
- **NFR-02 (Hieu nang):** Xac thuc login < 500ms (P95) trong dieu kien demo.
- **NFR-03 (Audit):** Login fail/lock/logout SHOULD ghi security event khi Audit/SecurityEvents module duoc trien khai.
- **NFR-04 (Tuong thich):** Stateless - khong session server-side ngoai `refresh_tokens`.
- **NFR-05 (API Contract):** Moi response co body SHALL dung wrapper `{ success, data, error, meta }`; endpoint logout co the tra HTTP 204 No Content.

## 5. Data Model
- **Users**(Id, Email[UNIQUE], PasswordHash, FullName, Phone, Status{Active,Locked}, FailedLoginCount, LoginWindowStartedAt, LockedUntil, LastLoginAt, IsDeleted, CreatedAt, UpdatedAt)
- **Roles**(Id, Name[UNIQUE: admin/staff/pt/member], Description)
- **UserRoles**(UserId -> Users, RoleId -> Roles) - quan he nhieu-nhieu de ho tro user co the co nhieu role neu can.
- **RefreshTokens**(Id, UserId -> Users, TokenHash, ExpiresAt, RevokedAt, CreatedAt)
- **PasswordResetTokens**(Id, UserId -> Users, TokenHash, ExpiresAt, UsedAt, CreatedAt)
- **SecurityEvents**(optional/future audit module: Id, UserId?, EventType, Metadata, CreatedAt)

## 6. API Spec
| Method | Path | Auth | Role | Request | Success | Loi |
|---|---|---|---|---|---|---|
| POST | /api/v1/auth/login | none | all | `{email, password}` | 200 `{accessToken, refreshToken, user, role, redirectPath}` | 400, 401, 423, 429 |
| POST | /api/v1/auth/register | none | all | `{fullName, email, phone?, password}` | 201 `{accessToken, refreshToken, user, role, redirectPath}` | 400, 409 |
| POST | /api/v1/auth/refresh | none | all | `{refreshToken}` | 200 `{accessToken, refreshToken, user, role, redirectPath}` | 400, 401 |
| GET | /api/v1/auth/me | Bearer | all | - | 200 `{userId, email, fullName, role, status}` | 401, 423 |
| POST | /api/v1/auth/logout | Bearer | all | - | 204 | 401 |
| POST | /api/v1/auth/forgot-password | none | all | `{email}` | 200 `{message, resetToken?}` | 400 |
| POST | /api/v1/auth/reset-password | none | all | `{resetToken, newPassword}` | 200 `{message}` | 400, 401 |
| POST | /api/v1/auth/change-password | Bearer | all | `{currentPassword, newPassword}` | 200 `{message}` | 400, 401 |
| POST | /api/v1/auth/google | none | all | `{idToken}` | 200 `{accessToken, refreshToken, user, role, redirectPath}` | 400, 500 |

Response chuan:
```json
{
  "success": true,
  "data": {},
  "error": null,
  "meta": null
}
```

Error response chuan:
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable message",
    "requestId": "trace-id"
  },
  "meta": null
}
```

## 7. Error Handling (EARS Unwanted)
- IF thieu input bat buoc, THEN tra 400 `VALIDATION_ERROR`.
- IF credentials sai, THEN tra 401 `INVALID_CREDENTIALS` voi thong bao chung.
- IF tai khoan Locked, THEN tra 423 `ACCOUNT_LOCKED`.
- IF vuot nguong brute-force, THEN tra 429 `TOO_MANY_ATTEMPTS`.
- IF refresh token het han/da thu hoi/khong hop le, THEN tra 401 `INVALID_REFRESH_TOKEN`.
- IF token thieu/khong hop le tren endpoint bao ve, THEN tra 401 `UNAUTHORIZED`.
- IF role khong du quyen, THEN tra 403 `FORBIDDEN`.
- IF email da ton tai khi dang ky, THEN tra 409 `EMAIL_EXISTS`.
- IF phone da ton tai khi dang ky, THEN tra 409 `PHONE_EXISTS`.
- IF reset token khong hop le/het han/da dung, THEN tra 401 `INVALID_RESET_TOKEN`.
- IF mat khau hien tai sai khi doi mat khau, THEN tra 401 `INVALID_CURRENT_PASSWORD`.
- IF Google ClientId chua duoc cau hinh, THEN tra 500 `GOOGLE_NOT_CONFIGURED`.
- IF Google ID token khong hop le, THEN tra 400 `INVALID_GOOGLE_TOKEN`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given tai khoan hop le, When login dung, Then nhan access+refresh token va role dung.
- [ ] **AC-02:** Given mat khau sai, When login, Then 401 voi thong bao chung, khong tiet lo email co ton tai hay khong.
- [ ] **AC-03:** Given sai 6 lan trong 15 phut, When login lan 6, Then 429 va tai khoan bi khoa tam 15 phut.
- [ ] **AC-04:** Given refresh token hop le, When refresh, Then nhan token moi va refresh token cu bi revoke.
- [ ] **AC-05:** Given logout, When dung lai refresh token cu, Then 401.
- [ ] **AC-06:** Given email chua ton tai + du lieu hop le, When register, Then tao user role Member va nhan access+refresh token (201).
- [ ] **AC-07:** Given email hoac phone da ton tai, When register, Then 409 va khong tao tai khoan.
- [ ] **AC-08:** Given email ton tai, When forgot-password, Then tao reset token han 30 phut; development co the tra resetToken de test.
- [ ] **AC-09:** Given reset token hop le + mat khau moi, When reset-password, Then doi duoc mat khau, refresh token cu bi revoke, reset token duoc danh dau da dung.
- [ ] **AC-10:** Given Bearer token + dung mat khau hien tai, When change-password, Then doi duoc mat khau va refresh token cu bi revoke.
- [ ] **AC-11:** Given sai mat khau hien tai, When change-password, Then 401 `INVALID_CURRENT_PASSWORD`.
- [ ] **AC-12:** Given Google ID token hop le va Google ClientId da cau hinh, When google login, Then tao user Member neu chua co va tra token he thong.
- [ ] **AC-13:** Given chua cau hinh Google ClientId, When google login, Then tra 500 `GOOGLE_NOT_CONFIGURED`.
- [ ] **AC-14:** Given request gui userId trong body khac token, When xu ly endpoint protected, Then system dung userId tu JWT claim, bo qua body.
- [ ] **AC-15:** Given Member token, When goi endpoint Admin-only, Then 403.

## 9. Out of Scope
- 2FA/OTP.
- Gui email that bang SMTP/SendGrid trong MVP neu chua cau hinh email provider; development duoc phep tra resetToken truc tiep de test.
- Quan ly OAuth provider nang cao (multi-provider, account linking, token refresh phia provider).
- Admin/Staff user management chi tiet (tao/sua/khoa Staff/PT/Member) thuoc feature Member/User Management rieng.
