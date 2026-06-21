# 15 — Database Schema (SQL Server, EF Core 10 Code-First)

> Schema mức cột. Canonical DB = **SQL Server** (provider `Microsoft.EntityFrameworkCore.SqlServer`), EF Core 10 Code-First.
>
> ## ⚠️ QUAN TRỌNG cho team DB
> Backend **đã implement** theo **spec 001 (Auth) + spec 002 (User/Member/PT)**. Các bảng đánh dấu **✅ ĐÃ CODE** dưới đây là **nguồn chuẩn** — DB phải khớp **ĐÚNG** (tên bảng, tên cột, kiểu) thì backend mới chạy được. Các bảng **⬜ THIẾT KẾ** thuộc spec 003–008 (chưa code) — sẽ chuẩn hoá khi làm tới.
>
> ## Quy ước backend ĐANG dùng (BẮT BUỘC khớp khi tạo DB)
> - Tên bảng: **snake_case, chữ thường** → `users`, `roles`, `user_roles`, `member_profiles`… (KHÔNG dùng `Users`, `MemberProfiles`).
> - `Id`: **BIGINT IDENTITY(1,1)**.
> - User ↔ Role: qua **bảng phụ `user_roles` (nhiều-nhiều)** — KHÔNG để `RoleId` trực tiếp trên `users`.
> - `users.Status`: lưu **chuỗi** `'active'` / `'locked'` (NVARCHAR) — KHÔNG phải TINYINT.
> - Thời gian: **DATETIME2** (UTC). Boolean: **BIT**. Chuỗi: **NVARCHAR** (Unicode).
> - Soft-delete: cột `IsDeleted BIT DEFAULT 0`.

---

# 1. Tổng quan bảng
| # | Bảng (tên thật) | Nhóm | Trạng thái | Mô tả |
|---|---|---|---|---|
| 1 | `users` | Auth | ✅ ĐÃ CODE | Tài khoản đăng nhập mọi role |
| 2 | `roles` | Auth | ✅ ĐÃ CODE | admin/staff/pt/member |
| 3 | `user_roles` | Auth | ✅ ĐÃ CODE | Liên kết User–Role (nhiều-nhiều) |
| 4 | `refresh_tokens` | Auth | ✅ ĐÃ CODE | Refresh token JWT |
| 5 | `password_reset_tokens` | Auth | ✅ ĐÃ CODE | Token quên/đặt lại mật khẩu |
| 6 | `member_profiles` | Member | ✅ ĐÃ CODE | Hồ sơ hội viên |
| 7 | `trainer_profiles` | PT | ✅ ĐÃ CODE | Hồ sơ PT |
| 8 | `audit_logs` | System | ✅ ĐÃ CODE | Nhật ký hành động mutating |
| 9 | `membership_packages` | Membership | ⬜ THIẾT KẾ | Gói tập (spec 003) |
| 10 | `memberships` | Membership | ⬜ THIẾT KẾ | Gói đã mua của member (spec 003) |
| 11 | `payments` | Payment | ⬜ THIẾT KẾ | Thanh toán (spec 003) |
| 12 | `check_ins` | Operation | ⬜ THIẾT KẾ | Lượt check-in (spec 004) |
| 13 | `trainer_assignments` | Training | ⬜ THIẾT KẾ | Phân công PT–Member (spec 005) |
| 14 | `workout_plans` | Training | ⬜ THIẾT KẾ | Giáo án (spec 005) |
| 15 | `workout_exercises` | Training | ⬜ THIẾT KẾ | Bài tập trong giáo án (spec 005) |
| 16 | `trainer_notes` | Training | ⬜ THIẾT KẾ | Ghi chú PT (spec 005) |
| 17 | `progress_logs` | Progress | ⬜ THIẾT KẾ | Tiến độ cơ thể (spec 006) |
| 18 | `food_items` | Nutrition | ⬜ THIẾT KẾ | CSDL món ăn (spec 007) |
| 19 | `meal_logs` / `meal_log_items` | Nutrition | ⬜ THIẾT KẾ | Bữa ăn + món (spec 007) |
| 20 | `calorie_targets` | Nutrition | ⬜ THIẾT KẾ | Mục tiêu calo (spec 007) |

---

# 2. Chi tiết cột — ✅ BẢNG ĐÃ CODE (DB phải khớp đúng)

## 2.1 `users`
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY(1,1) | |
| Email | NVARCHAR(255) | UNIQUE, NOT NULL | định danh đăng nhập |
| Phone | NVARCHAR(30) | NULL, UNIQUE (filtered: WHERE Phone IS NOT NULL) | |
| PasswordHash | NVARCHAR(255) | NOT NULL | **BCrypt cost ≥12** |
| FullName | NVARCHAR(150) | NOT NULL | |
| Status | NVARCHAR(20) | NOT NULL | **chuỗi** `'active'` / `'locked'` |
| FailedLoginCount | INT | NOT NULL, DEFAULT 0 | đếm sai mật khẩu |
| LoginWindowStartedAt | DATETIME2 | NULL | mốc cửa sổ 15' đếm brute-force |
| LockedUntil | DATETIME2 | NULL | khóa tạm tới thời điểm này |
| LastLoginAt | DATETIME2 | NULL | |
| IsDeleted | BIT | NOT NULL, DEFAULT 0 | soft delete |
| CreatedAt | DATETIME2 | NOT NULL | |
| UpdatedAt | DATETIME2 | NOT NULL | |

> ❗ KHÔNG có cột `RoleId` — role lấy qua bảng `user_roles`.

## 2.2 `roles`
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Id | BIGINT | PK, IDENTITY |
| Name | NVARCHAR(30) | UNIQUE, NOT NULL — `admin`/`staff`/`pt`/`member` (chữ thường) |
| Description | NVARCHAR(255) | NULL |

## 2.3 `user_roles` (nhiều-nhiều)
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| UserId | BIGINT | PK (cùng RoleId), FK → users.Id |
| RoleId | BIGINT | PK (cùng UserId), FK → roles.Id |

> PK kép `(UserId, RoleId)`. Mỗi user hiện tại gắn 1 role, nhưng mô hình hỗ trợ nhiều.

## 2.4 `refresh_tokens`
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| UserId | BIGINT | FK → users.Id, NOT NULL | |
| TokenHash | NVARCHAR(255) | NOT NULL | lưu bản băm, không plaintext |
| ExpiresAt | DATETIME2 | NOT NULL | 7 ngày |
| RevokedAt | DATETIME2 | NULL | rotate/logout |
| CreatedAt | DATETIME2 | NOT NULL | |

## 2.5 `password_reset_tokens`
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| UserId | BIGINT | FK → users.Id, NOT NULL | |
| TokenHash | NVARCHAR(255) | NOT NULL | bản băm |
| ExpiresAt | DATETIME2 | NOT NULL | 30 phút |
| UsedAt | DATETIME2 | NULL | đánh dấu đã dùng |
| CreatedAt | DATETIME2 | NOT NULL | |

## 2.6 `member_profiles`
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| UserId | BIGINT | FK → users.Id, **UNIQUE** (1-1) | |
| DateOfBirth | DATETIME2 | NULL | |
| Gender | NVARCHAR(20) | NULL | |
| Address | NVARCHAR(255) | NULL | |
| EmergencyContact | NVARCHAR(100) | NULL | |
| JoinedAt | DATETIME2 | NOT NULL | ngày tham gia |
| IsDeleted | BIT | NOT NULL, DEFAULT 0 | |
| CreatedAt | DATETIME2 | NOT NULL | |
| UpdatedAt | DATETIME2 | NOT NULL | |

## 2.7 `trainer_profiles`
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| UserId | BIGINT | FK → users.Id, **UNIQUE** (1-1) | |
| Specialty | NVARCHAR(150) | NULL | chuyên môn |
| Bio | NVARCHAR(1000) | NULL | giới thiệu |
| Gender | NVARCHAR(20) | NULL | |
| DateOfBirth | DATETIME2 | NULL | |
| YearsOfExperience | INT | NULL | số năm kinh nghiệm (≥0) |
| IsDeleted | BIT | NOT NULL, DEFAULT 0 | |
| CreatedAt | DATETIME2 | NOT NULL | |
| UpdatedAt | DATETIME2 | NOT NULL | |

## 2.8 `audit_logs`
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| UserId | BIGINT | **NULL**, FK → users.Id | actor (NULL nếu hệ thống) |
| Action | NVARCHAR(100) | NOT NULL | vd `CREATE_MEMBER`, `UPDATE_USER` |
| Entity | NVARCHAR(60) | NOT NULL | vd `MemberProfile` |
| EntityId | BIGINT | NOT NULL | |
| Metadata | NVARCHAR(MAX) | NULL | JSON, KHÔNG chứa PII nhạy cảm |
| CreatedAt | DATETIME2 | NOT NULL | |

---

# 2b. Chi tiết cột — ⬜ BẢNG THIẾT KẾ (spec 003–008, CHƯA code)

> Các bảng này là **bản thiết kế dự kiến**, sẽ chốt khi implement spec tương ứng. Tên bảng/cột cuối cùng theo code lúc đó (giữ quy ước snake_case, BIGINT, DATETIME2). Tham khảo thêm `05_DATABASE_SPEC.md`.

## `membership_packages` (spec 003)
Id PK · Name · Description · DurationDays SMALLINT · Price DECIMAL(12,2) · **SupportsPT BIT NOT NULL DEFAULT 0** (0=gói thường, 1=gói có PT) · IsActive BIT · CreatedAt/UpdatedAt.
> `SupportsPT` thêm bằng `database/008_package_supports_pt.sql`. Quyết định một gói có hỗ trợ PT hay không; quyền PT của hội viên **suy ra động** từ gói còn hiệu lực (spec 003 FR-PKG-04, gác PT ở spec 005 FR-PT-05) — KHÔNG lưu cờ PT ở `users`/`member_profiles`.

## `memberships` (spec 003)
Id PK · MemberId FK→member_profiles · PackageId FK→membership_packages · StartDate DATE · EndDate DATE · Status (0=PendingPayment,1=Active,2=Expired,3=Cancelled) · CreatedAt/UpdatedAt.

## `payments` (spec 003)
Id PK · MembershipId FK→memberships · Amount DECIMAL(12,2) CHECK ≥0 · Method (1=Cash,2=Transfer,3=Card) · Status (0=Pending,1=Paid,2=Refunded) · PaidAt · CreatedBy FK→users · CreatedAt.

## `check_ins` (spec 004)
Id PK · MemberId FK→member_profiles INDEX · CheckInAt DATETIME2 (UTC) · CreatedBy FK→users (NULL nếu self).

## `meal_logs` / `meal_log_items` (spec 007)
**meal_logs:** Id PK · MemberId FK · LogDate DATE · MealType (1=Breakfast,2=Lunch,3=Dinner,4=Snack) · CreatedAt.
**meal_log_items:** Id PK · MealLogId FK · FoodItemId FK · Quantity DECIMAL(8,2) CHECK >0 · Calories DECIMAL(8,2).

*(Các bảng còn lại — membership_packages, trainer_assignments, workout_plans, workout_exercises, trainer_notes, progress_logs, food_items, calorie_targets — theo cùng quy ước; cột chính nêu ở `05_DATABASE_SPEC.md`, chốt khi code.)*

---

# 3. Quan hệ chính (phần ĐÃ CODE)
- `users` 1—N `user_roles` N—1 `roles` (nhiều-nhiều; mỗi user hiện gắn 1 role).
- `users` 1—1 `member_profiles` (qua UserId UNIQUE) — chỉ với role member.
- `users` 1—1 `trainer_profiles` (qua UserId UNIQUE) — chỉ với role pt.
- `users` 1—N `refresh_tokens`, 1—N `password_reset_tokens`.
- Mọi hành động mutating quan trọng → 1 dòng `audit_logs`.

*(Quan hệ nghiệp vụ khác — membership/payment/checkin/workout/meal — thuộc spec 003–008.)*

# 4. Index & ràng buộc quan trọng (phần ĐÃ CODE)
- UNIQUE: `users.Email`; `users.Phone` (filtered, bỏ qua NULL); `roles.Name`; `member_profiles.UserId`; `trainer_profiles.UserId`.
- PK kép: `user_roles(UserId, RoleId)`.
- INDEX: `audit_logs(Entity, EntityId)`.
- FK ON DELETE NO ACTION cho dữ liệu nghiệp vụ (dùng soft-delete). `password_reset_tokens`/`refresh_tokens` có thể CASCADE theo user.
- `users.Status` lưu chuỗi `'active'`/`'locked'` (KHÔNG TINYINT). Các enum nghiệp vụ khác (spec sau) sẽ dùng TINYINT + CHECK.

# 5. Map use case → bảng
| Use case | Bảng chính | Trạng thái |
|---|---|---|
| Login / Auth (UC-01/02, spec 001) | `users`, `roles`, `user_roles`, `refresh_tokens`, `password_reset_tokens` | ✅ |
| Quản lý User/Member/PT (UC-03/04/05, spec 002) | `users`, `roles`, `user_roles`, `member_profiles`, `trainer_profiles` | ✅ |
| Audit (UC-23) | `audit_logs` | ✅ |
| Sell/Renew (UC-07/08) | `memberships`, `payments`, `membership_packages` | ⬜ spec 003 |
| Check-in (UC-09) | `check_ins`, `memberships` | ⬜ spec 004 |
| Assign PT / Workout (UC-10/12) | `trainer_assignments`, `workout_plans`, `workout_exercises` | ⬜ spec 005 |
| Progress (UC-15) | `progress_logs` | ⬜ spec 006 |
| Meal/Calorie (UC-17/20) | `meal_logs`, `meal_log_items`, `food_items`, `calorie_targets` | ⬜ spec 007 |
| Dashboard (UC-22) | `payments`, `memberships`, `check_ins` | ⬜ spec 008 |

---

# 6. Ghi chú đồng bộ (cho team DB)
- Phần **✅ ĐÃ CODE** là contract chuẩn từ code backend (spec 001 + 002). Nếu tạo DB tay, **khớp đúng tên/kiểu** ở mục 2.1–2.8 thì backend chạy được ngay.
- Cách chắc chắn 100% (khuyến nghị): khi backend bật EF Migration, chạy `dotnet ef migrations script` để **xuất file SQL đúng y hệt code** → dùng file đó tạo DB thay vì viết tay.
- Bản schema cũ (RoleId trực tiếp / Status TINYINT / tên PascalCase) đã **lỗi thời** — không dùng nữa; chuẩn theo file này.
</content>
