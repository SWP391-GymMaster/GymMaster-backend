# Implementation Plan: Authentication & Role-Based Access Control

**Feature Branch**: `001-auth-rbac` | **Spec**: [spec.md](spec.md)
**Status**: `Implemented`
**Input**: `docs/03-Interface-Specs/feature-specs/001-auth-rbac/spec.md`

---

## 1. Summary

Feature nền tảng: xác thực bằng email + mật khẩu (BCrypt cost 12), phát hành **JWT HS256** (access 15 phút / refresh 7 ngày có rotate), phân quyền 4 role qua `[Authorize(Roles=...)]`, cộng các luồng phụ trợ (register, forgot/reset bằng OTP 6 số, change password, Google login).

Cách tiếp cận kỹ thuật: **Vertical slice** — toàn bộ feature nằm trong một thư mục `Features/Auth/`, gồm controller mỏng + một service chứa nghiệp vụ + một file DTO. Không có tầng repository riêng: service truy cập `DbContext` trực tiếp (quyết định D-002 bên dưới). Identity luôn lấy từ JWT claim, không bao giờ từ body.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 (`net10.0`) |
| **Primary Dependencies** | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.8, `BCrypt.Net-Next` 4.2.0, `Google.Apis.Auth` 1.74.0, `MailKit` 4.17.0 |
| **Storage** | SQL Server + EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer` 10.0.8) |
| **Testing** | xUnit (`tests/GymMaster.Api.Tests/`) + black-box PowerShell (`tests/blackbox/`) |
| **Target Platform** | Linux container — Google Cloud Run + Cloud SQL |
| **Project Type** | Web service (REST API), FE Next.js ở repo riêng `GymMaster-frontend` |
| **Performance Goals** | Login < 500ms P95 (NFR-02) |
| **Constraints** | Stateless (không session server-side); `ClockSkew = 0`; secret lấy từ env/User-Secrets, không hardcode |
| **Scale/Scope** | 9 endpoint, 5 bảng, ~1000 user quy mô đồ án |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng trong code |
|---|---|---|
| SEC-01 — mật khẩu phải hash BCrypt cost ≥ 12 | ✅ PASS | `Features/Auth/AuthService.cs` |
| GBL-05 — identity lấy từ JWT claim, không từ body | ✅ PASS | `Common/ApiControllerBase.cs` (`CurrentUserId`, `CurrentRole`) |
| SAFE-02 — không log token / mật khẩu / OTP | ✅ PASS | không có `Log*` nào nhận các field này |
| SEC-05 — secret không commit vào repo | ✅ PASS | Secret nằm ở **User Secrets** (`UserSecretsId` trong `.csproj`) và env vars Cloud Run; `Options/JwtOptions.cs` bind từ configuration. `.gitignore` chặn `secrets.json` · `env.yaml` · `.env*` · `appsettings.*.local.json`. `appsettings.Development.json` có trong Git nhưng chỉ chứa cấu hình `Logging`, không chứa secret. |
| AUDIT-01 — hành động quan trọng ghi AuditLog | ✅ PASS | AUDIT-01 liệt kê hành động **đổi dữ liệu nghiệp vụ** (Membership · Payment · phân công PT · đổi role) — thao tác tài khoản ghi qua `IAuditService` ở spec 002. Login/logout không thuộc phạm vi, xem §8 |
| ARCH-02 — response bọc `ApiResponse<T>` | ✅ PASS | `Common/ApiResponse.cs`, dùng ở mọi action |
| DATA-01 — soft delete, không xoá cứng | ✅ PASS | `users.IsDeleted`, unique index có filter `IsDeleted = 0` |

## 4. Project Structure

### Documentation (feature này)

```text
docs/03-Interface-Specs/feature-specs/001-auth-rbac/
├── spec.md      # Đặc tả 9 thành phần (EARS)
├── plan.md      # File này — kiến trúc as-built
└── tasks.md     # Bảng công việc + trạng thái + truy vết
```

### Source Code (thực tế trong repo)

```text
backend/GymMaster.API/
├── Features/Auth/
│   ├── AuthController.cs          # 9 action, route "api/v1/auth", controller mỏng
│   ├── IAuthService.cs            # Hợp đồng nghiệp vụ (dùng cho DI + unit test)
│   ├── AuthService.cs             # Toàn bộ nghiệp vụ: hash, JWT, khoá tài khoản, OTP, Google
│   └── AuthDtos.cs                # Request/Response record của feature
├── Entities/
│   ├── User.cs                    # + FailedLoginCount, LoginWindowStartedAt, LockedUntil
│   ├── Role.cs · UserRole.cs      # RBAC nhiều-nhiều (thực tế 1 role/user)
│   ├── RefreshToken.cs            # TokenHash (BCrypt), ExpiresAt, RevokedAt
│   └── PasswordResetToken.cs      # TokenHash của OTP, AttemptCount, UsedAt
├── Options/
│   ├── JwtOptions.cs              # SecretKey, Issuer, Audience, TTL
│   └── GoogleAuthOptions.cs       # ClientId
├── Infrastructure/
│   ├── IEmailSender.cs
│   └── EmailSender.cs             # MailKit SMTP — gửi OTP reset
├── Common/
│   ├── ApiControllerBase.cs       # CurrentUserId / CurrentRole đọc từ claim
│   ├── ApiResponse.cs             # Wrapper { success, data, error, meta }
│   └── ServiceResult.cs           # Kết quả nghiệp vụ (mã lỗi + HTTP status)
├── Data/
│   ├── GymMasterDbContext.cs      # DbSet + cấu hình index/filter
│   └── DatabaseSeeder.cs          # Seed 4 role + tài khoản admin
└── Program.cs                     # AddJwtBearer, ClockSkew=0, DI, CORS

database/
└── 007_add_reset_attemptcount.sql # Migration thêm AttemptCount cho OTP

tests/
├── blackbox/Api.BlackBox.Tests.ps1   # Test luồng auth chạy thật qua HTTP
└── GymMaster.Api.Tests/               # (chưa có AuthServiceTests — xem tasks.md T-020)
```

**Structure Decision**: Vertical slice theo feature (`Features/<Tên>/`), mỗi slice tự chứa controller + service + DTO. Entity dùng chung đặt ở `Entities/` vì nhiều slice cùng tham chiếu (`User` được 8/10 feature dùng). Đây là cấu trúc thống nhất cho cả 10 spec.

## 5. Design Decisions

> **Chi tiết hoá ADR dự án**: [D-01](../../../06-Management/decision-log.md) (4 role) · [D-05](../../../06-Management/decision-log.md) (JWT Bearer + BCrypt) · [D-11](../../../06-Management/decision-log.md) (API contract) · [D-23](../../../06-Management/decision-log.md) (OTP 6 số). Bảng dưới là quyết định **cấp feature**, không thay thế hệ đánh số D-xx của dự án.

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-001 | JWT HS256 thay vì RS256 | 1 service duy nhất, không cần bên thứ ba verify; đơn giản hoá vận hành | Đổi key phải deploy lại; không tách được khoá ký/khoá verify |
| D-002 | Service gọi thẳng `DbContext`, không có Repository | EF Core `DbSet` đã là repository; thêm tầng nữa chỉ tăng code cho đồ án 10 feature | Unit test phải dùng EF InMemory/SQLite thay vì mock interface |
| D-003 | Refresh token **rotate** mỗi lần refresh | Giới hạn thiệt hại nếu token bị lộ (token cũ bị `RevokedAt` ngay) | Client phải lưu lại token mới sau mỗi lần refresh |
| D-004 | Lưu **hash** refresh token & OTP (BCrypt), không lưu plaintext | Rò rỉ DB không dẫn tới chiếm quyền tài khoản | Không tra ngược được token; verify phải quét theo user |
| D-005 | OTP 6 chữ số qua email thay vì link token dài | Nhập được trên mobile, hợp bối cảnh phòng gym | Không gian mã nhỏ → bắt buộc có `AttemptCount` giới hạn 3 lần |
| D-006 | Lỗi login luôn dùng **một thông điệp chung** | Chống user enumeration (FR-AUTH-04) | UX kém hơn cho người dùng thật gõ nhầm email |
| D-007 | Khoá tạm bằng cột trên `users`, không dùng Redis/cache | Không thêm hạ tầng cho đồ án; trạng thái khoá bền vững khi restart | Mỗi lần login sai có một lần ghi DB |
| D-008 | `ClockSkew = TimeSpan.Zero` | TTL 15 phút mà mặc định lệch 5 phút là quá lớn | Server lệch giờ sẽ gây lỗi 401 — cần NTP chuẩn |

## 6. Data Flow

```text
Login:
  POST /api/v1/auth/login
    → AuthController.Login          (Features/Auth/AuthController.cs)
    → AuthService.LoginAsync        (Features/Auth/AuthService.cs)
        ├─ users.Where(Email, !IsDeleted)          → kiểm tra tồn tại
        ├─ kiểm tra Status / LockedUntil           → 423 nếu đang khoá
        ├─ BCrypt.Verify(password, PasswordHash)   → sai: ++FailedLoginCount, 401/429
        ├─ tạo JWT (JwtOptions) + RefreshToken(hash) → INSERT refresh_tokens
        └─ reset bộ đếm, cập nhật LastLoginAt
    → ApiResponse<AuthLoginResponse>  { accessToken, refreshToken, user, role, redirectPath }

Request có bảo vệ:
  Bearer token → UseAuthentication → UseAuthorization ([Authorize(Roles=…)])
    → ApiControllerBase.CurrentUserId đọc claim NameIdentifier
    → service dùng userId đó (KHÔNG đọc từ body — FR-RBAC-01)

Reset mật khẩu:
  forgot-password → sinh OTP 6 số → BCrypt hash → INSERT password_reset_tokens (TTL 30')
                  → EmailSender (MailKit SMTP); chặn gửi lại trong 60s
  reset-password  → verify OTP → sai: ++AttemptCount (3 lần → vô hiệu)
                  → đúng: đổi PasswordHash, UsedAt, revoke toàn bộ refresh token
```

## 7. Traceability (FR → code)

| FR | Triển khai tại | Ghi chú |
|---|---|---|
| FR-AUTH-01 | `AuthService.cs` — BCrypt cost 12 | dùng lại ở spec 002 khi tạo user |
| FR-AUTH-02, 05, 06 | `AuthService.cs` (Login/Refresh/Logout) + `Entities/RefreshToken.cs` | rotate ở Refresh |
| FR-AUTH-03, 07 | `Entities/User.cs` (FailedLoginCount, LoginWindowStartedAt, LockedUntil) | 5 lần / 15 phút |
| FR-AUTH-04 | `AuthService.cs` — cùng một message cho mọi nhánh sai | |
| FR-AUTH-08, 09 | `AuthService.RegisterAsync` | 409 EMAIL_EXISTS / PHONE_EXISTS |
| FR-AUTH-10…11a | `AuthService` + `PasswordResetToken.cs` + `EmailSender.cs` | `database/007_add_reset_attemptcount.sql` |
| FR-AUTH-12 | `AuthService.ChangePasswordAsync` | revoke refresh token cũ |
| FR-AUTH-13 | `AuthService` + `Google.Apis.Auth` + `Options/GoogleAuthOptions.cs` | |
| FR-RBAC-01 | `Common/ApiControllerBase.cs` | dùng chung cho cả 10 feature |
| FR-RBAC-02 | `[Authorize(Roles=…)]` trên từng controller | |
| FR-RBAC-03 | `Program.cs` — `UseAuthentication/UseAuthorization`; `[AllowAnonymous]` ở endpoint public | trừ VNPay callback (spec 010) |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| AUDIT-01 chưa áp cho login/logout/refresh | Ghi audit mọi lần login sẽ làm phình bảng `audit_logs` trong demo; audit đang tập trung vào hành động thay đổi dữ liệu | Ghi hết → bảng audit bị nhiễu, khó dùng cho dashboard (spec 008) |
| `AuthService.cs` gánh 9 luồng trong một class | Các luồng dùng chung state khoá tài khoản + phát hành token; tách nhỏ sẽ phải truyền qua lại | Tách 3 service (Login/Password/OAuth) → tăng file, lợi ích thấp ở quy mô này |
| Chưa có unit test cho `AuthService` | Luồng auth đang được phủ bởi black-box test chạy thật qua HTTP | Bỏ hẳn test → không chấp nhận; xem T-020 trong tasks.md |
