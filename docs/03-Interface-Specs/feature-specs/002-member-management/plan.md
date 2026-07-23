# Implementation Plan: User, Staff, PT & Member Management

**Feature Branch**: `002-member-management` | **Spec**: [spec.md](spec.md)
**Status**: `Implemented`
**Input**: `docs/03-Interface-Specs/feature-specs/002-member-management/spec.md`

---

## 1. Summary

Quản lý vòng đời hồ sơ người dùng cho 4 role. Điểm kiến trúc quan trọng: **tách `users` (tài khoản, do spec 001 sở hữu) khỏi 3 bảng profile** (`member_profiles`, `staff_profiles`, `trainer_profiles`) theo quan hệ 1-1. Nhờ vậy mỗi role có tập thuộc tính riêng mà không cần cột null tràn lan trên `users`.

Feature được chia thành **4 vertical slice** thay vì một slice lớn, vì 4 nhóm endpoint có chủ thể và quyền khác hẳn nhau:

| Slice | Route gốc | Ai dùng | Vai trò |
|---|---|---|---|
| `Features/Users/` | `/api/v1/users` | Admin | CRUD tài khoản mọi role |
| `Features/Members/` | `/api/v1/members` | Admin, Staff, Member(self) | Hồ sơ hội viên |
| `Features/Trainers/` | `/api/v1/trainers` | Admin, PT(self) | Hồ sơ PT |
| `Features/Account/` | `/api/v1/users/me` | mọi role đã đăng nhập | Self-service + avatar |

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | EF Core 10 (SqlServer), `BCrypt.Net-Next` 4.2.0, `CloudinaryDotNet` 1.29.2 |
| **Storage** | SQL Server — `users`, `member_profiles`, `staff_profiles`, `trainer_profiles` |
| **Ảnh** | Cloudinary (chỉ lưu URL vào `users.AvatarUrl`, DB không chứa nhị phân) |
| **Testing** | xUnit — `UserServiceTests.cs`, `TrainerServiceTests.cs`, `AccountServiceTests.cs` |
| **Target Platform** | Cloud Run + Cloud SQL |
| **Performance Goals** | Tìm kiếm < 1s với ~1000 hội viên (NFR-01) |
| **Constraints** | Phân trang mặc định 20, tối đa 100/trang; **role bất biến sau khi tạo** |
| **Scale/Scope** | 21 endpoint, 4 slice, 3 bảng profile |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| DATA-01 — soft delete, không xoá cứng | ✅ PASS | `IsDeleted` trên cả 4 bảng; `DELETE` chỉ set cờ |
| GBL-05 — identity từ JWT claim | ✅ PASS | `/me` lấy `CurrentUserId` từ `ApiControllerBase` |
| AUDIT-01 — hành động quan trọng ghi AuditLog | ✅ PASS | `IAuditService` được inject vào `UserService`, `MemberService` |
| SAFE-02 — không log PII | ✅ PASS | không log SĐT/email (NFR-03) |
| ARCH-02 — wrapper `ApiResponse<T>` + `PagedResult<T>` | ✅ PASS | `Common/PagedResult.cs` |
| GBL-03 — validate dữ liệu người ở một chỗ | ✅ PASS | `Common/PersonValidation.cs` dùng chung 4 slice |
| BIZ-08 — role gán khi tạo là bất biến | ✅ PASS | 422 `ROLE_TRANSITION_NOT_ALLOWED` (FR-USR-04) |

## 4. Project Structure

### Documentation (feature này)

```text
docs/03-Interface-Specs/feature-specs/002-member-management/
├── spec.md · plan.md · tasks.md
```

### Source Code (thực tế trong repo)

```text
backend/GymMaster.API/
├── Features/Users/
│   ├── UsersController.cs         # route "api/v1/users", [Authorize(Roles="admin")]
│   ├── IUserService.cs · UserService.cs
│   └── UserDtos.cs                # AdminUserResponse, CreateUserRequest…
├── Features/Members/
│   ├── MembersController.cs       # route "api/v1/members" — Admin/Staff/Member(self)
│   ├── IMemberService.cs · MemberService.cs
│   └── MemberDtos.cs              # MemberResponse, CreateMemberResponse
├── Features/Trainers/
│   ├── TrainersController.cs      # route "api/v1/trainers"
│   ├── ITrainerService.cs · TrainerService.cs
│   └── TrainerDtos.cs             # CreateTrainerResponse
├── Features/Account/
│   ├── AccountController.cs       # route "api/v1/users/me" — self-service
│   ├── IAccountService.cs · AccountService.cs
│   └── AccountDtos.cs             # PersonalProfileResponse
├── Entities/
│   ├── MemberProfile.cs · StaffProfile.cs · TrainerProfile.cs
│   └── User.cs                    # dùng chung với spec 001
├── Common/
│   ├── PersonValidation.cs        # validate dob/gender/phone/emergencyContact
│   └── PagedResult.cs             # { items, page, pageSize, total }
├── Infrastructure/
│   ├── IAvatarStorage.cs
│   ├── CloudinaryAvatarStorage.cs # upload jpeg/png/webp ≤5MB
│   └── AvatarStorageException.cs  # → HTTP 502
└── Options/CloudinaryOptions.cs

database/
└── 012_users_avatarurl.sql        # thêm cột AvatarUrl
└── 013_staff_profiles_trainer_contact_filtered_unique.sql

tests/GymMaster.Api.Tests/
├── UserServiceTests.cs · TrainerServiceTests.cs · AccountServiceTests.cs
└── (chưa có MemberServiceTests.cs — xem tasks.md T-034)
```

**Structure Decision**: 4 slice tách theo **chủ thể quản trị**, không theo entity. `/users/me` được đặt trong slice `Account` riêng (không nhét vào `Users`) vì luồng self-service có quyền và nghiệp vụ khác hẳn luồng Admin quản trị — trộn chung sẽ khiến controller phải rẽ nhánh theo role ở mọi action.

## 5. Design Decisions

> **Chi tiết hoá ADR dự án**: [D-09](../../../06-Management/decision-log.md) (soft delete) · [D-11](../../../06-Management/decision-log.md) (API contract) · [D-20](../../../06-Management/decision-log.md) (avatar → Cloudinary). Bảng dưới là quyết định **cấp feature**, không thay thế hệ đánh số D-xx của dự án.

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-101 | 3 bảng profile 1-1 thay vì cột null trên `users` | Mỗi role có thuộc tính khác nhau (PT có specialty/experience, Member có joinedAt) | Đọc hồ sơ đầy đủ phải JOIN; tạo user role admin/staff sinh thêm 1 INSERT |
| D-102 | **Role bất biến** — đổi role trả 422 | Đổi role đang có dữ liệu gắn kèm (PT có member được phân công, Member có membership) sẽ để lại dữ liệu mồ côi | Muốn đổi vai trò phải tạo tài khoản mới + khoá tài khoản cũ (thủ công hơn) |
| D-103 | `GET /members/me` **tự tạo** MemberProfile nếu chưa có | User đăng ký qua `/auth/register` (spec 001) chưa có profile; chặn luồng sẽ làm hỏng trải nghiệm đăng ký | Một GET có thể ghi DB — lệch nguyên tắc "GET không side-effect" |
| D-104 | `MemberCode` **suy ra** `MEM-{Id:D6}`, không lưu DB | Không cần bảng sinh mã, không lo trùng, không cần migration | Không đổi được định dạng mã về sau mà không phá dữ liệu hiển thị cũ |
| D-105 | Tạo PT trong **một bước** (`POST /trainers` tạo cả user + profile) | Admin luôn tạo PT kèm chuyên môn; bắt gọi 2 API dễ tạo user PT mồ côi | Endpoint làm 2 việc, cần transaction |
| D-106 | Tự sinh mật khẩu tạm khi Admin không nhập | Admin tạo hàng loạt tài khoản không phải nghĩ mật khẩu | `initialPassword` xuất hiện trong response 201 — chỉ hiện đúng một lần |
| D-107 | Ảnh đại diện đẩy Cloudinary, DB chỉ giữ URL | Không phình DB, có CDN sẵn | Phụ thuộc dịch vụ ngoài; chưa cấu hình → 500 `CLOUDINARY_NOT_CONFIGURED` |
| D-108 | Unique index **có filter** `IsDeleted = 0` | Soft-delete xong vẫn tái dùng lại được email/phone đó | Index có điều kiện, khó port sang DBMS khác |

## 6. Data Flow

```text
Admin tạo tài khoản:
  POST /api/v1/users  →  UsersController  →  UserService.CreateAsync
     ├─ kiểm tra trùng email/phone (filter IsDeleted=0)        → 409 DUPLICATE
     ├─ password: nhập → dùng | không nhập → sinh tạm         → passwordAutoGenerated
     ├─ INSERT users + user_roles
     ├─ role admin/staff + có thông tin cá nhân → INSERT staff_profiles
     └─ AuditService.LogAsync("CREATE_USER")
  → 201 { …, initialPassword? }

Staff tạo hội viên:
  POST /api/v1/members → MemberService.CreateAsync
     ├─ email chưa có           → tạo User(role member) + MemberProfile
     ├─ email là Member chưa có hồ sơ → chỉ tạo MemberProfile (linkedToExistingAccount=true, KHÔNG đổi mật khẩu)
     └─ email đã có hồ sơ / thuộc role khác → 409

Self-service:
  GET/PUT /api/v1/users/me/profile → AccountService → rẽ theo role
     ├─ admin/staff → staff_profiles
     ├─ pt          → trainer_profiles
     └─ member      → 422 NOT_SUPPORTED (điều hướng sang /members/me)

  POST /api/v1/users/me/avatar (multipart)
     → validate mime + size ≤5MB → CloudinaryAvatarStorage.UploadAsync
     → cập nhật users.AvatarUrl → AuditLog "UPDATE_AVATAR"
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-USR-01, 02 | `Features/Users/UserService.cs` (CreateAsync) |
| FR-USR-03 | `[Authorize(Roles="admin")]` trên `UsersController.cs`, `TrainersController.cs` |
| FR-USR-04 | `UserService.UpdateAsync` — 422 `ROLE_TRANSITION_NOT_ALLOWED` |
| FR-USR-05 | `UserService` (UpdateStatusAsync, ResetPasswordAsync) + `IAuditService` |
| FR-MEM-01 | `Features/Members/MemberService.cs` (CreateAsync, 3 nhánh email) |
| FR-MEM-02 | `MemberService.SearchAsync` + `Common/PagedResult.cs` |
| FR-MEM-03 | `Common/PersonValidation.cs` |
| FR-MEM-04 | `IsDeleted` trên `Entities/MemberProfile.cs` |
| FR-MEM-05 | Kiểm tra ownership theo `UserId` trong `MembersController.cs` |
| FR-MEM-06 | `MemberService.GetMineAsync` — tự tạo hồ sơ |
| FR-PT-PROF-01 | `Features/Trainers/TrainerService.cs` (CreateAsync một bước) |
| FR-ACC-01 | `Features/Account/AccountService.cs` |
| FR-ACC-02 | `Infrastructure/CloudinaryAvatarStorage.cs` |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| 4 slice cho một spec | 4 nhóm endpoint khác chủ thể + khác quyền; gộp lại sẽ thành một `UserService` khổng lồ rẽ nhánh theo role | Gộp 1 slice → controller đầy `if (role == …)`, khó test từng luồng |
| `GET /members/me` có side-effect (D-103) | Bù cho việc `/auth/register` không tạo profile; sửa ở tầng register sẽ đụng spec 001 đang chạy | Trả 404 bắt FE gọi thêm POST → thêm một vòng request cho mọi hội viên mới |
| 3 bảng profile gần giống nhau | Thuộc tính thực sự khác nhau theo role | Một bảng `profiles` + cột `Type` → cột null tuỳ role, mất ràng buộc NOT NULL |
| Chưa có `MemberServiceTests.cs` | `MemberService` là service phức tạp nhất slice (3 nhánh email) nhưng hiện chỉ được phủ black-box | Bỏ qua → không chấp nhận; xem T-034 |
