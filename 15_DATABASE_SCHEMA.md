# 15 — Database Schema (SQL Server, EF Core 10 Code-First)

> Schema mức cột. Canonical DB = **SQL Server** (provider `Microsoft.EntityFrameworkCore.SqlServer`), EF Core 10 Code-First. **Toàn bộ 24 bảng dưới đây ĐÃ CODE** (spec 001–010). Đồng bộ theo `GymMasterDbContext.OnModelCreating` ngày 2026-07-15.
>
> ## Quy ước backend ĐANG dùng (BẮT BUỘC khớp khi tạo DB)
> - Tên bảng: **snake_case, chữ thường** → `users`, `member_profiles`, `membership_packages`…
> - `Id`: **BIGINT IDENTITY(1,1)**.
> - User ↔ Role: qua bảng phụ `user_roles` (nhiều-nhiều) — KHÔNG để `RoleId` trực tiếp trên `users`.
> - `users.Status`: lưu **chuỗi** `'active'` / `'locked'` (NVARCHAR). Enum nghiệp vụ khác: **TINYINT**.
> - Thời gian: **DATETIME2** (lưu UTC; "hôm nay" nghiệp vụ tính theo giờ VN GMT+7 ở tầng code `AppClock`). Boolean: **BIT**. Chuỗi: **NVARCHAR**. Ngày thuần: **DATE**. Tiền/đo lường: **DECIMAL**.
> - Soft-delete: cột `IsDeleted BIT DEFAULT 0` (users, *_profiles).
> - **Backend KHÔNG tự tạo/sửa schema** (không EnsureCreated/Migrate lúc chạy) — schema do team DB nạp từ `database/GymMaster_SQLServer_Final.sql`. Seeder chỉ nạp roles + 4 tài khoản demo + hồ sơ member/PT demo.

---

# 1. Tổng quan bảng (24 bảng — tất cả ĐÃ CODE)
| # | Bảng | Nhóm | Spec | Mô tả |
|---|---|---|---|---|
| 1 | `users` | Auth | 001 | Tài khoản đăng nhập mọi role |
| 2 | `roles` | Auth | 001 | admin/staff/pt/member |
| 3 | `user_roles` | Auth | 001 | Liên kết User–Role (nhiều-nhiều) |
| 4 | `refresh_tokens` | Auth | 001 | Refresh token JWT |
| 5 | `password_reset_tokens` | Auth | 001 | OTP quên/đặt lại mật khẩu |
| 6 | `member_profiles` | Member | 002 | Hồ sơ hội viên |
| 7 | `staff_profiles` | Staff | 002 | Hồ sơ cá nhân admin/staff |
| 8 | `trainer_profiles` | PT | 002 | Hồ sơ PT |
| 9 | `audit_logs` | System | 008 | Nhật ký hành động mutating |
| 10 | `membership_packages` | Membership | 003 | Gói tập (kèm SupportsPT) |
| 11 | `memberships` | Membership | 003 | Gói đã mua của member |
| 12 | `payments` | Payment | 003/010 | Thanh toán (thủ công + VNPay) |
| 13 | `check_ins` | Operation | 004 | Lượt check-in |
| 14 | `trainer_assignments` | Training | 005 | Phân công PT–Member |
| 15 | `workout_plans` | Training | 005 | Giáo án |
| 16 | `workout_exercises` | Training | 005 | Bài tập trong giáo án |
| 17 | `exercise_catalog` | Training | 005 | Danh mục bài tập (unique theo tên) |
| 18 | `trainer_notes` | Training | 005 | Ghi chú PT |
| 19 | `progress_logs` | Progress | 006 | Tiến độ cơ thể |
| 20 | `food_items` | Nutrition | 007/009 | CSDL món ăn (kèm nguồn AI) |
| 21 | `meal_logs` | Nutrition | 007 | Bữa ăn theo ngày |
| 22 | `meal_log_items` | Nutrition | 007 | Món trong bữa (snapshot calo) |
| 23 | `calorie_targets` | Nutrition | 007 | Mục tiêu calo/macro |

---

# 2. Chi tiết cột

## 2.1 `users`
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| Email | NVARCHAR(255) | UNIQUE (filtered IsDeleted=0), NOT NULL | định danh đăng nhập |
| Phone | NVARCHAR(30) | NULL, UNIQUE (filtered Phone IS NOT NULL AND IsDeleted=0) | |
| PasswordHash | NVARCHAR(255) | NOT NULL | **BCrypt cost 12** |
| FullName | NVARCHAR(150) | NOT NULL | |
| AvatarUrl | NVARCHAR(500) | NULL | URL Cloudinary / ảnh Google |
| Status | NVARCHAR(20) | NOT NULL | **chuỗi** `'active'` / `'locked'` |
| FailedLoginCount | INT | NOT NULL DEFAULT 0 | đếm sai mật khẩu |
| LoginWindowStartedAt | DATETIME2 | NULL | mốc cửa sổ 15' brute-force |
| LockedUntil | DATETIME2 | NULL | khoá tạm tới thời điểm này |
| LastLoginAt | DATETIME2 | NULL | |
| IsDeleted | BIT | NOT NULL DEFAULT 0 | |
| CreatedAt / UpdatedAt | DATETIME2 | NOT NULL | |

> ❗ KHÔNG có cột `RoleId` — role lấy qua `user_roles`.

## 2.2 `roles`
Id BIGINT PK · Name NVARCHAR(30) UNIQUE (`admin`/`staff`/`pt`/`member`) · Description NVARCHAR(255) NULL.

## 2.3 `user_roles`
UserId BIGINT (FK→users) · RoleId BIGINT (FK→roles) · **PK kép (UserId, RoleId)**.

## 2.4 `refresh_tokens`
Id PK · UserId FK→users · TokenHash NVARCHAR(255) (BCrypt) · ExpiresAt DATETIME2 (7 ngày) · RevokedAt DATETIME2 NULL (rotate/logout) · CreatedAt.

## 2.5 `password_reset_tokens`
Id PK · UserId FK→users · TokenHash NVARCHAR(255) (**băm OTP 6 số**) · ExpiresAt DATETIME2 (30 phút) · UsedAt DATETIME2 NULL · **AttemptCount INT** (số lần nhập sai; ≥3 → vô hiệu) · CreatedAt.

## 2.6 `member_profiles`
Id PK · UserId FK→users **UNIQUE (1-1)** · DateOfBirth DATETIME2? · Gender NVARCHAR(20)? · Address NVARCHAR(255)? · EmergencyContact NVARCHAR(100)? · JoinedAt DATETIME2 · IsDeleted BIT · CreatedAt/UpdatedAt.

## 2.7 `staff_profiles`
Id PK · UserId FK→users **UNIQUE (1-1)** · DateOfBirth? · Gender NVARCHAR(20)? · Address NVARCHAR(255)? · EmergencyContact NVARCHAR(100)? · IsDeleted BIT · CreatedAt/UpdatedAt. *(Cho admin/staff — thông tin cá nhân.)*

## 2.8 `trainer_profiles`
Id PK · UserId FK→users **UNIQUE (1-1)** · Specialty NVARCHAR(150)? · Bio NVARCHAR(1000)? · Gender NVARCHAR(20)? · DateOfBirth? · Address NVARCHAR(255)? · EmergencyContact NVARCHAR(100)? · YearsOfExperience INT? · IsDeleted BIT · CreatedAt/UpdatedAt.

## 2.9 `audit_logs`
Id PK · UserId BIGINT **NULL** FK→users (actor) · Action NVARCHAR(100) · Entity NVARCHAR(60) · EntityId BIGINT · Metadata NVARCHAR(MAX) JSON (không PII nhạy cảm) · CreatedAt · INDEX(Entity, EntityId).

## 2.10 `membership_packages`
Id PK · Name NVARCHAR(100) **UNIQUE** · Description NVARCHAR(500)? · DurationDays SMALLINT · Price DECIMAL(12,2) · **SupportsPT BIT** (0=thường,1=có PT) · IsActive BIT · CreatedAt/UpdatedAt.

## 2.11 `memberships`
Id PK · MemberId FK→member_profiles (INDEX) · PackageId FK→membership_packages · StartDate **DATE** · EndDate **DATE** · Status **TINYINT** (0 PendingPayment,1 Active,2 Expired,3 Cancelled) · CreatedByUserId · CreatedAt/UpdatedAt?.

## 2.12 `payments`
Id PK · MembershipId FK→memberships (INDEX) · Amount DECIMAL(12,2) · PaymentMethod **TINYINT** (1 Cash,2 Transfer,3 Card) · Status **TINYINT** (0 Pending,1 Paid,2 Refunded) · PaidAt DATETIME2? · CreatedByUserId · CreatedAt/UpdatedAt?.

## 2.13 `check_ins`
Id PK · MemberId FK→member_profiles · CheckInAt DATETIME2 (UTC) · CreatedBy FK→users **NULL** (null = member tự check-in) · INDEX(MemberId, CheckInAt).

## 2.14 `trainer_assignments`
Id PK · MemberId FK→member_profiles · TrainerId FK→trainer_profiles · StartDate DATE · EndDate DATE? · Status **TINYINT** (1 Active,2 Ended) · CreatedByUserId · CreatedAt/UpdatedAt? · INDEX(MemberId,Status), INDEX(TrainerId,Status).

## 2.15 `workout_plans`
Id PK · MemberId FK · TrainerId FK · Title NVARCHAR(150) · Goal NVARCHAR(255)? · StartDate DATE · EndDate DATE? · Status **TINYINT** (1 Active,2 Completed,3 Cancelled) · CreatedAt/UpdatedAt? · INDEX(MemberId,Status), INDEX(TrainerId,Status).

## 2.16 `workout_exercises`
Id PK · WorkoutPlanId FK→workout_plans (**CASCADE**) · ExerciseId FK→exercise_catalog · SortOrder SMALLINT · Sets TINYINT? · Reps SMALLINT? · WeightKg DECIMAL(6,2)? · DurationMinutes SMALLINT? · RestSeconds SMALLINT? · Note NVARCHAR(255)? · **UNIQUE(WorkoutPlanId, SortOrder)**, INDEX(ExerciseId).

## 2.17 `exercise_catalog`
Id PK · Name NVARCHAR(150) **UNIQUE** · MuscleGroup NVARCHAR(80)? · Description NVARCHAR(500)? · IsActive BIT. *(Bài tập nhập theo tên → tra/tạo bản ghi ở đây.)*

## 2.18 `trainer_notes`
Id PK · TrainerId FK→trainer_profiles · MemberId FK→member_profiles · NoteDate DATE · Content NVARCHAR(1000) · CreatedByUserId? · CreatedAt/UpdatedAt? · INDEX(MemberId,NoteDate), INDEX(TrainerId,NoteDate).

## 2.19 `progress_logs`
Id PK · MemberId FK→member_profiles · MeasuredAt DATETIME2 · WeightKg DECIMAL(5,2)? · BodyFatPercent DECIMAL(5,2)? · ChestCm DECIMAL(5,2)? · WaistCm DECIMAL(5,2)? · HipCm DECIMAL(5,2)? · Note NVARCHAR(500)? · CreatedByUserId? · CreatedAt · INDEX(MemberId,MeasuredAt).

## 2.20 `food_items`
Id PK · Name NVARCHAR(150) **UNIQUE** · Unit NVARCHAR(30) · CaloriesPerUnit DECIMAL(8,2) · ProteinG DECIMAL(8,2)? · CarbG DECIMAL(8,2)? · FatG DECIMAL(8,2)? · IsActive BIT · **ServingSize DECIMAL(8,2)** (mặc định 100) · **Source NVARCHAR(20)** (`Admin`/`AI`) · CreatedAt.

## 2.21 `meal_logs`
Id PK · MemberId FK→member_profiles · LogDate **DATE** · MealType **TINYINT** (1 Breakfast,2 Lunch,3 Dinner,4 Snack) · CreatedAt · INDEX(MemberId, LogDate).

## 2.22 `meal_log_items`
Id PK · MealLogId FK→meal_logs · FoodItemId FK→food_items · Quantity DECIMAL(8,2) · Calories DECIMAL(8,2) (**snapshot** lúc ghi).

## 2.23 `calorie_targets`
Id PK · MemberId · EffectiveDate **DATE** · DailyCalories DECIMAL(8,2) · ProteinG?/CarbG?/FatG? DECIMAL(8,2) · CreatedAt · **UNIQUE(MemberId, EffectiveDate)**.

---

# 3. Quan hệ chính
- `users` 1—N `user_roles` N—1 `roles` (mỗi user hiện gắn 1 role).
- `users` 1—1 `member_profiles` / `staff_profiles` / `trainer_profiles` (UserId UNIQUE, theo role).
- `users` 1—N `refresh_tokens`, `password_reset_tokens`, `audit_logs` (actor).
- `member_profiles` 1—N `memberships` 1—N `payments`; 1—N `check_ins`, `progress_logs`, `meal_logs`, `calorie_targets`, `trainer_assignments`, `workout_plans`, `trainer_notes`.
- `membership_packages` 1—N `memberships`. `trainer_profiles` 1—N `trainer_assignments`/`workout_plans`/`trainer_notes`.
- `workout_plans` 1—N `workout_exercises` N—1 `exercise_catalog`. `meal_logs` 1—N `meal_log_items` N—1 `food_items`.

# 4. Index & ràng buộc quan trọng
- UNIQUE: `users.Email`, `users.Phone` (filtered), `roles.Name`, `member_profiles.UserId`, `staff_profiles.UserId`, `trainer_profiles.UserId`, `membership_packages.Name`, `exercise_catalog.Name`, `food_items.Name`, `workout_exercises(WorkoutPlanId,SortOrder)`, `calorie_targets(MemberId,EffectiveDate)`.
- PK kép: `user_roles(UserId, RoleId)`.
- Enum nghiệp vụ dùng **TINYINT** (memberships/payments/trainer_assignments/workout_plans/meal_logs Status/Type). `users.Status` là chuỗi.
- Soft-delete (users, *_profiles) + lazy-expire membership ở tầng code (`MembershipLifecycle`).

# 5. Map use case → bảng (tất cả đã code)
| Use case | Bảng chính |
|---|---|
| Auth (UC-01/02, spec 001) | users, roles, user_roles, refresh_tokens, password_reset_tokens |
| User/Member/PT (UC-03/04/05, spec 002) | users, *_profiles |
| Sell/Renew/Pay (UC-06/07/08, spec 003/010) | membership_packages, memberships, payments |
| Check-in (UC-09, spec 004) | check_ins, memberships |
| Assign PT / Workout / Note (UC-10..13, spec 005) | trainer_assignments, workout_plans, workout_exercises, exercise_catalog, trainer_notes |
| Progress & 360 (UC-14/15, spec 006) | progress_logs (+ tổng hợp nhiều bảng) |
| Meal/Calorie (UC-16..21, spec 007/009) | food_items, meal_logs, meal_log_items, calorie_targets |
| Dashboard & Audit (UC-22/23, spec 008) | payments, memberships, check_ins, audit_logs |

# 6. Ghi chú đồng bộ (cho team DB)
- Toàn bộ bảng là contract chuẩn từ code backend. Nguồn schema thực tế: `database/GymMaster_SQLServer_Final.sql` (22 bảng gốc + `SupportsPT`). Backend map theo `GymMasterDbContext`.
- Cách chắc chắn 100%: `dotnet ef migrations script` để xuất SQL đúng y hệt code.
- Bản schema cũ (RoleId trực tiếp / Status TINYINT trên users / tên PascalCase) đã lỗi thời.
