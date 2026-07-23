# Feature Specification: Authentication & Role-Based Access Control

**Feature Branch**: `001-auth-rbac`  
**Created**: 2026-05-30  
**Status**: Implemented (spec đồng bộ theo code 2026-07-15)  
**Spec style**: SDD + Spec Kit - 9 components, EARS notation  
**Source**: srs-use-cases (UC-01, UC-02), requirements (FR-AUTH, FR-RBAC), CONSTITUTION SEC-01..05

> **EARS legend:** Ubiquitous `THE system SHALL ...` · Event `WHEN <trigger>, THE system SHALL ...` · State `WHILE <state>, THE system SHALL ...` · Optional `WHERE <feature>, THE system SHALL ...` · Unwanted `IF <condition>, THEN THE system SHALL ...`

---

## 1. Context & Goal
Moi truy cap GymMaster phai duoc xac thuc va phan quyen theo 4 role (Admin, Staff, PT, Member). Day la feature nen tang: moi feature khac phu thuoc identity + role lay tu token. Muc tieu: dang nhap an toan (BCrypt cost 12 + JWT HS256), chong privilege escalation, chong brute-force, va ho tro cac luong auth can thiet cho do an.

**In Scope (đúng code):**
- Login bang email/mat khau (JWT access token 15 phut, refresh token 7 ngay, **rotate** refresh khi refresh, logout revoke). Field dang nhap chap nhan alias `identifier`/`email`/`username`.
- Register tai khoan Member (self-service).
- Forgot password / Reset password qua **OTP 6 chu so** gui email (gioi han so lan nhap sai, chong spam gui lai).
- Change password khi da dang nhap.
- Google login bang Google ID token (tao tai khoan Member neu chua co, lay anh Google lam avatar mac dinh).
- RBAC theo 4 role; chong brute-force; chong user enumeration.

**Base path:** `/api/v1/auth`  
**Response format:** `{ success, data, error, meta }` (wrapper `ApiResponse<T>`).

## 2. Actors
| Actor | Vai tro trong feature |
|---|---|
| Admin | Dang nhap; co toan quyen. |
| Staff | Dang nhap; quyen van hanh quay. |
| PT | Dang nhap; quyen tren member duoc phan cong. |
| Member | Dang nhap; quyen tren du lieu cua minh. |
| System | Phat hanh/thu hoi token, kiem tra quyen, ghi AuditLog cac hanh dong mutating. |

## 3. Functional Requirements (EARS)
- **FR-AUTH-01 (Ubiquitous):** THE system SHALL luu mat khau duoi dang bam BCrypt voi cost factor = 12.
- **FR-AUTH-02 (Event):** WHEN nguoi dung gui email + mat khau dung, THE system SHALL phat hanh access token (TTL 15 phut, HS256) va refresh token (TTL 7 ngay), kem thong tin user + role + `redirectPath` theo role.
- **FR-AUTH-03 (Unwanted):** IF mot tai khoan dang nhap sai > 5 lan trong cua so 15 phut, THEN THE system SHALL khoa tam tai khoan do 15 phut (dat `LockedUntil`). Lan sai thu 6 tro di tra 429 `TOO_MANY_ATTEMPTS`.
- **FR-AUTH-04 (Unwanted):** IF email khong ton tai hoac mat khau sai, THEN THE system SHALL tra thong bao loi GIONG NHAU ("Email hoac mat khau khong dung.") cho ca hai truong hop (chong user enumeration).
- **FR-AUTH-05 (Event):** WHEN client gui refresh token hop le chua thu hoi va con han, THE system SHALL phat hanh access token moi va **xoay (rotate)** refresh token (revoke token cu, cap token moi).
- **FR-AUTH-06 (Event):** WHEN nguoi dung logout, THE system SHALL thu hoi moi refresh token dang hieu luc cua user (set `RevokedAt`), tra 204.
- **FR-AUTH-07 (State):** WHILE tai khoan o trang thai `Locked` hoac dang bi khoa tam (`LockedUntil` con hieu luc), THE system SHALL tu choi dang nhap kem thong bao trang thai khoa.
- **FR-AUTH-08 (Event):** WHEN nguoi dung dang ky voi email chua ton tai va du lieu hop le (mat khau >= 6 ky tu), THE system SHALL tao tai khoan role Member, bam mat khau BCrypt cost 12, va tra access + refresh token (201).
- **FR-AUTH-09 (Unwanted):** IF email da ton tai khi dang ky, THEN tra 409 `EMAIL_EXISTS`; IF phone da ton tai, THEN tra 409 `PHONE_EXISTS`. KHONG tao tai khoan.
- **FR-AUTH-10 (Event):** WHEN nguoi dung yeu cau quen mat khau bang email, THE system SHALL tao mot **OTP 6 chu so**, luu duoi dang bam BCrypt, han **30 phut**, vo hieu cac OTP cu chua dung. WHERE email da cau hinh SMTP, THE system SHALL gui OTP + link qua email va KHONG tra OTP trong response. WHERE chua cau hinh email va o moi truong Development, THE system MAY tra `resetToken` (OTP) de test. Luon tra thong bao chung (khong tiet lo email co ton tai hay khong).
- **FR-AUTH-10a (Unwanted):** IF vua gui OTP trong vong 60 giay, THEN THE system SHALL KHONG gui OTP moi (chong spam) nhung van tra thong bao chung.
- **FR-AUTH-11 (Event):** WHEN nguoi dung gui `{email, resetToken (OTP), newPassword}` voi OTP dung con han, THE system SHALL cap nhat password hash moi, danh dau OTP da dung, reset bo dem khoa tai khoan, va thu hoi refresh token cu.
- **FR-AUTH-11a (Unwanted):** IF OTP sai, THEN THE system SHALL tang `AttemptCount`; sau **3 lan sai** THE system SHALL vo hieu OTP (bat xin ma moi) va tra `TOO_MANY_ATTEMPTS`. Con lai tra `INVALID_RESET_TOKEN` kem so lan thu con lai.
- **FR-AUTH-12 (Event):** WHEN nguoi dung da dang nhap doi mat khau va cung cap dung mat khau hien tai (mat khau moi >= 6 ky tu), THE system SHALL cap nhat password hash moi va thu hoi refresh token cu.
- **FR-AUTH-13 (Event):** WHEN nguoi dung dang nhap bang Google ID token hop le, THE system SHALL xac thuc token voi Google ClientId, tao user role Member neu chua ton tai (lay `name`/`picture` tu Google), va phat hanh access + refresh token.
- **FR-RBAC-01 (Ubiquitous):** THE system SHALL lay `userId` va `role` tu JWT claim (`NameIdentifier`/`sub`, `Role`), KHONG BAO GIO tu request body hoac query.
- **FR-RBAC-02 (Unwanted):** IF nguoi dung goi endpoint khong thuoc quyen role cua minh, THEN tra 403 va KHONG thuc hien hanh dong.
- **FR-RBAC-03 (Ubiquitous):** THE system SHALL yeu cau access token hop le cho moi endpoint tru cac public auth endpoint (login/register/refresh/forgot/reset/google) va cac VNPay callback (`ipn`/`return`, bao ve bang chu ky).

## 4. Non-functional Requirements
- **NFR-01 (Bao mat):** Token ky HS256; `Jwt:SecretKey` lay tu env/User-Secrets. `ClockSkew = 0`. Khong log token/mat khau/OTP.
- **NFR-02 (Hieu nang):** Xac thuc login < 500ms (P95) trong dieu kien demo.
- **NFR-03 (Audit):** Cac hanh dong tai khoan (tao/sua/khoa/reset) ghi AuditLog qua `AuditService`.
- **NFR-04 (Tuong thich):** Stateless - khong session server-side ngoai bang `refresh_tokens` / `password_reset_tokens`.
- **NFR-05 (API Contract):** Moi response co body dung wrapper `{ success, data, error, meta }`; logout tra HTTP 204 No Content.

## 5. Data Model
- **users**(Id, Email[UNIQUE filter IsDeleted=0], Phone[UNIQUE filter], PasswordHash, FullName, AvatarUrl, Status{active,locked}, FailedLoginCount, LoginWindowStartedAt, LockedUntil, LastLoginAt, IsDeleted, CreatedAt, UpdatedAt)
- **roles**(Id, Name[UNIQUE: admin/staff/pt/member], Description)
- **user_roles**(UserId → users, RoleId → roles) — PK ghep, quan he nhieu-nhieu (mot user thuc te co 1 role chinh).
- **refresh_tokens**(Id, UserId → users, TokenHash[BCrypt], ExpiresAt, RevokedAt, CreatedAt)
- **password_reset_tokens**(Id, UserId → users, TokenHash[BCrypt cua OTP], ExpiresAt, UsedAt, **AttemptCount**, CreatedAt)

## 6. API Spec
| Method | Path | Auth | Role | Request | Success | Loi |
|---|---|---|---|---|---|---|
| POST | /api/v1/auth/login | none | all | `{identifier\|email\|username, password}` | 200 `{accessToken, refreshToken, expiresAt, user, role, redirectPath}` | 400, 401, 423, 429 |
| POST | /api/v1/auth/register | none | all | `{fullName, email, phone?, password}` | 201 (nhu login) | 400, 409 |
| POST | /api/v1/auth/refresh | none | all | `{refreshToken}` | 200 (nhu login) | 400, 401 |
| GET | /api/v1/auth/me | Bearer | all | - | 200 `AuthUserResponse` | 401, 423 |
| POST | /api/v1/auth/logout | Bearer | all | - | 204 | 401 |
| POST | /api/v1/auth/forgot-password | none | all | `{email}` | 200 `{message, resetToken?}` | 400 |
| POST | /api/v1/auth/reset-password | none | all | `{email, resetToken, newPassword}` | 200 `{message}` | 400, 401 |
| POST | /api/v1/auth/change-password | Bearer | all | `{currentPassword, newPassword}` | 200 `{message}` | 400, 401 |
| POST | /api/v1/auth/google | none | all | `{idToken}` | 200 (nhu login) | 400, 423, 500 |

**AuthLoginResponse:** `{ accessToken, refreshToken, expiresAt, user: AuthUserResponse, role, redirectPath }`  
**AuthUserResponse:** `{ userId, email, fullName, avatarUrl, role, status, memberProfileId }` — `memberProfileId` la Id ho so `member_profiles` (null neu khong phai member/chua co ho so).  
**redirectPath theo role:** admin→`/admin`, staff→`/staff`, pt→`/pt`, member→`/member`.

Response chuan:
```json
{ "success": true, "data": {}, "error": null, "meta": null }
```
Error chuan:
```json
{ "success": false, "data": null,
  "error": { "code": "ERROR_CODE", "message": "…", "requestId": "trace-id" }, "meta": null }
```

## 7. Error Handling (EARS Unwanted)
- IF thieu input bat buoc, THEN 400 `VALIDATION_ERROR`.
- IF credentials sai, THEN 401 `INVALID_CREDENTIALS` (thong bao chung).
- IF tai khoan Locked, THEN 423 `ACCOUNT_LOCKED`.
- IF vuot nguong brute-force (>5 lan/15 phut), THEN 429 `TOO_MANY_ATTEMPTS`.
- IF refresh token het han/da thu hoi/khong hop le, THEN 401 `INVALID_REFRESH_TOKEN`.
- IF token thieu/khong hop le tren endpoint bao ve, THEN 401 `UNAUTHORIZED`.
- IF role khong du quyen, THEN 403 (`[Authorize(Roles=...)]`).
- IF email da ton tai khi dang ky, THEN 409 `EMAIL_EXISTS`; IF phone da ton tai, THEN 409 `PHONE_EXISTS`.
- IF OTP reset khong hop le/het han/da dung, THEN 401 `INVALID_RESET_TOKEN`; IF sai qua 3 lan, THEN 401 `TOO_MANY_ATTEMPTS`.
- IF mat khau hien tai sai khi doi mat khau, THEN 401 `INVALID_CURRENT_PASSWORD`.
- IF Google ClientId chua cau hinh, THEN 500 `GOOGLE_NOT_CONFIGURED`; IF Google ID token khong hop le, THEN 400 `INVALID_GOOGLE_TOKEN`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given tai khoan hop le, When login dung, Then nhan access+refresh token, `expiresAt`, role, `redirectPath` dung.
- [ ] **AC-02:** Given mat khau sai, When login, Then 401 voi thong bao chung.
- [ ] **AC-03:** Given sai 6 lan trong 15 phut, When login lan 6, Then 429 va tai khoan bi khoa tam 15 phut.
- [ ] **AC-04:** Given refresh token hop le, When refresh, Then nhan token moi va refresh token cu bi revoke (rotate).
- [ ] **AC-05:** Given logout, When dung lai refresh token cu, Then 401.
- [ ] **AC-06:** Given email chua ton tai + du lieu hop le, When register, Then tao user role Member + tra token (201).
- [ ] **AC-07:** Given email/phone da ton tai, When register, Then 409, khong tao tai khoan.
- [ ] **AC-08:** Given email ton tai, When forgot-password, Then tao OTP 6 so han 30 phut; development co the tra `resetToken` de test; da cau hinh SMTP thi gui email.
- [ ] **AC-09:** Given OTP dung + mat khau moi, When reset-password, Then doi duoc mat khau, OTP danh dau da dung, refresh token cu bi revoke.
- [ ] **AC-09a:** Given OTP sai 3 lan, When reset-password, Then OTP bi vo hieu (`TOO_MANY_ATTEMPTS`), phai xin ma moi.
- [ ] **AC-10:** Given Bearer token + dung mat khau hien tai, When change-password, Then doi duoc mat khau va refresh token cu bi revoke.
- [ ] **AC-11:** Given sai mat khau hien tai, When change-password, Then 401 `INVALID_CURRENT_PASSWORD`.
- [ ] **AC-12:** Given Google ID token hop le, When google login, Then tao user Member neu chua co (kem avatar Google) va tra token.
- [ ] **AC-13:** Given chua cau hinh Google ClientId, When google login, Then 500 `GOOGLE_NOT_CONFIGURED`.
- [ ] **AC-14:** Given request gui userId trong body khac token, When xu ly endpoint protected, Then system dung userId tu JWT claim, bo qua body.
- [ ] **AC-15:** Given Member token, When goi endpoint Admin-only, Then 403.

## 9. Out of Scope
- 2FA/OTP dang nhap (OTP chi dung cho reset mat khau).
- Multi-provider OAuth / account linking nang cao.
- Quan ly tai khoan Staff/PT/Member chi tiet — thuoc spec 002.
