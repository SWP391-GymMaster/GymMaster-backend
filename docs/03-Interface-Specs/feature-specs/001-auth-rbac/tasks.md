---
description: "Task list — Authentication & Role-Based Access Control"
---

# Tasks: Authentication & Role-Based Access Control

**Feature**: `001-auth-rbac`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 20/22 hoàn thành — feature đang chạy production

> **Cách đọc.** Đây là bảng công việc **as-built**: các task `[X]` là việc đã làm xong và đã có trong code (đường dẫn file là thật, kiểm chứng được). Các task `[ ]` là việc còn nợ, phát hiện khi đối chiếu spec ↔ code. Không tự ý sinh task mới cho tính năng chưa có trong spec.

**Ký hiệu**: `[P]` = có thể làm song song (khác file, không phụ thuộc) · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 Khởi tạo project ASP.NET Core .NET 10 tại `backend/GymMaster.API/GymMaster.API.csproj`
- [X] T002 [P] Thêm package `BCrypt.Net-Next`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Google.Apis.Auth`, `MailKit`
- [X] T003 [P] Khai báo option binding trong `backend/GymMaster.API/Options/JwtOptions.cs` và `GoogleAuthOptions.cs`
- [X] T004 Cấu hình secret qua User-Secrets / biến môi trường (`UserSecretsId` trong csproj), không commit key

## Phase 2: Foundational (chặn mọi user story)

**⚠️ Toàn bộ 9 feature còn lại phụ thuộc phase này.**

- [X] T005 Định nghĩa entity `Entities/User.cs`, `Role.cs`, `UserRole.cs` + cấu hình trong `Data/GymMasterDbContext.cs`
- [X] T006 Unique index có filter `IsDeleted = 0` cho `users.Email` và `users.Phone` (DATA-01)
- [X] T007 [P] Wrapper response `Common/ApiResponse.cs` + `Common/ServiceResult.cs` (ARCH-01)
- [X] T008 `Common/ApiControllerBase.cs` — `CurrentUserId` / `CurrentRole` đọc từ JWT claim → **FR-RBAC-01**
- [X] T009 Đăng ký JWT Bearer + `ClockSkew = TimeSpan.Zero` trong `Program.cs`
- [X] T010 `Data/DatabaseSeeder.cs` — seed 4 role (admin/staff/pt/member) + tài khoản admin khởi tạo

**Checkpoint**: có identity + wrapper response → các user story bắt đầu được.

---

## Phase 3: US1 — Đăng nhập & duy trì phiên (P1) 🎯 MVP

**Goal**: người dùng đăng nhập lấy được token và giữ phiên mà không phải nhập lại mật khẩu.
**Independent Test**: `POST /auth/login` → dùng access token gọi `GET /auth/me` → `POST /auth/refresh` → `POST /auth/logout` rồi thử lại refresh token cũ (phải 401).

- [X] T011 [US1] `Features/Auth/AuthDtos.cs` — `LoginRequest` (nhận alias `identifier`/`email`/`username`), `AuthLoginResponse`, `AuthUserResponse`
- [X] T012 [US1] `Features/Auth/AuthService.cs` — `LoginAsync`: BCrypt cost 12, phát hành JWT + refresh token → **FR-AUTH-01, 02**
- [X] T013 [US1] Đếm sai mật khẩu + khoá tạm 15 phút bằng `FailedLoginCount` / `LoginWindowStartedAt` / `LockedUntil` trong `Entities/User.cs` → **FR-AUTH-03, 07**
- [X] T014 [US1] Thông điệp lỗi chung cho mọi nhánh sai (chống user enumeration) → **FR-AUTH-04**
- [X] T015 [US1] `Entities/RefreshToken.cs` + rotate khi refresh (revoke token cũ, cấp token mới) → **FR-AUTH-05**
- [X] T016 [US1] `LogoutAsync` — revoke toàn bộ refresh token còn hiệu lực, trả 204 → **FR-AUTH-06**
- [X] T017 [US1] `Features/Auth/AuthController.cs` — action `login` / `refresh` / `me` / `logout`, tính `redirectPath` theo role
- [X] T018 [US1] Bật `[Authorize]` mặc định + `[AllowAnonymous]` cho endpoint auth public trong `Program.cs` → **FR-RBAC-03**
- [X] T019 [US1] Black-box test luồng login/refresh/logout trong `tests/blackbox/Api.BlackBox.Tests.ps1`
- [ ] T020 [US1] **Còn nợ** — unit test `tests/GymMaster.Api.Tests/AuthServiceTests.cs` cho khoá tài khoản + rotate refresh token (14/14 service khác đã có test, riêng `AuthService` chưa)

**Checkpoint**: đăng nhập được, phiên duy trì được → mọi role dùng được hệ thống.

---

## Phase 4: US2 — Tự đăng ký tài khoản Member (P2)

**Goal**: khách tự tạo tài khoản Member mà không cần nhân viên nhập hộ.
**Independent Test**: `POST /auth/register` với email mới → 201 kèm token; đăng ký lại cùng email → 409.

- [X] T021 [US2] `RegisterAsync` — tạo user role Member, hash BCrypt cost 12, trả token 201 → **FR-AUTH-08**
- [X] T022 [US2] Chặn trùng: 409 `EMAIL_EXISTS` / `PHONE_EXISTS`, không tạo tài khoản → **FR-AUTH-09**

**Checkpoint**: US1 + US2 chạy độc lập.

---

## Phase 5: US3 — Khôi phục & đổi mật khẩu (P2)

**Goal**: người dùng quên mật khẩu tự lấy lại được; người đang đăng nhập đổi được mật khẩu.
**Independent Test**: `forgot-password` → nhận OTP → `reset-password` OTP đúng đổi được mật khẩu; nhập sai 3 lần thì OTP bị vô hiệu.

- [X] T023 [US3] `Entities/PasswordResetToken.cs` — lưu **hash BCrypt của OTP**, `ExpiresAt` 30 phút, `UsedAt`
- [X] T024 [US3] Migration `database/007_add_reset_attemptcount.sql` — thêm cột `AttemptCount`
- [X] T025 [US3] `Infrastructure/EmailSender.cs` (MailKit SMTP) + `Infrastructure/IEmailSender.cs`, đăng ký DI trong `Program.cs`
- [X] T026 [US3] `ForgotPasswordAsync` — sinh OTP 6 số, vô hiệu OTP cũ, trả thông báo chung; Development chưa cấu hình SMTP thì trả `resetToken` để test → **FR-AUTH-10**
- [X] T027 [US3] Chặn gửi lại OTP trong vòng 60 giây → **FR-AUTH-10a**
- [X] T028 [US3] `ResetPasswordAsync` — đổi mật khẩu, đánh dấu OTP đã dùng, reset bộ đếm khoá, revoke refresh token → **FR-AUTH-11**
- [X] T029 [US3] Đếm OTP sai, 3 lần thì vô hiệu (`TOO_MANY_ATTEMPTS`) → **FR-AUTH-11a**
- [X] T030 [US3] `ChangePasswordAsync` — xác minh mật khẩu hiện tại, revoke refresh token cũ → **FR-AUTH-12**

---

## Phase 6: US4 — Đăng nhập bằng Google (P3)

**Goal**: giảm ma sát đăng ký cho hội viên mới.
**Independent Test**: gửi Google ID token hợp lệ → tạo user Member (nếu chưa có) + trả token.

- [X] T031 [US4] Xác thực ID token bằng `Google.Apis.Auth` theo `GoogleAuthOptions.ClientId`
- [X] T032 [US4] Tạo user role Member nếu chưa tồn tại, lấy `name`/`picture` làm `FullName`/`AvatarUrl` → **FR-AUTH-13**
- [X] T033 [US4] Lỗi cấu hình → 500 `GOOGLE_NOT_CONFIGURED`; token sai → 400 `INVALID_GOOGLE_TOKEN`

---

## Phase 7: Polish & Cross-cutting

- [X] T034 Chuẩn hoá catalog mã lỗi (`VALIDATION_ERROR`, `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `TOO_MANY_ATTEMPTS`, …)
- [X] T035 [P] CORS policy `Frontend` + chỉ ép HTTPS ở Development (TLS đã terminate ở Cloud Run) — `Program.cs`
- [X] T037 ADR cho OTP 6 số đã có trong `docs/06-Management/decision-log.md` → **D-23**; JWT Bearer + BCrypt → **D-05**
- [X] T038 Lý do chọn **HS256 thay vì RS256** đã ghi vào **D-05** trong `decision-log.md` (một service vừa phát hành vừa verify, không có bên thứ ba cần verify độc lập)

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2**: bắt buộc tuần tự.
- **Phase 2** chặn tất cả — và chặn luôn cả 9 feature còn lại (mọi feature đều đọc identity từ `ApiControllerBase`).
- **US1 (P1)** phải xong trước, vì US2/US3/US4 đều tái sử dụng hàm phát hành token của US1.
- **US2, US3, US4** độc lập với nhau → làm song song được.
- **Phase 7** sau cùng.

```text
Setup → Foundational → US1 ─┬→ US2
                            ├→ US3
                            └→ US4  → Polish
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01, AC-02 | T012, T014 | `tests/blackbox/Api.BlackBox.Tests.ps1` |
| AC-03 | T013 | black-box (login sai 6 lần) |
| AC-04, AC-05 | T015, T016 | black-box |
| AC-06, AC-07 | T021, T022 | black-box |
| AC-08, AC-09, AC-09a | T026, T028, T029 | black-box (Development trả `resetToken`) |
| AC-10, AC-11 | T030 | black-box |
| AC-12, AC-13 | T031–T033 | thủ công (cần Google ID token thật) |
| AC-14, AC-15 | T008, T018 | black-box (gọi endpoint Admin bằng token Member → 403) |

> **Khoảng trống đã biết**: AC-01…AC-11 hiện chỉ được phủ ở mức black-box, chưa có unit test cho `AuthService` (T020). AC-12/AC-13 chưa tự động hoá được vì phụ thuộc Google.
