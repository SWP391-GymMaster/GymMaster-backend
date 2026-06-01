# 15 — Database Schema (SQL Server, EF Core 10 Code-First)

> Schema chi tiết mức cột. Canonical DB = **SQL Server** (provider `Microsoft.EntityFrameworkCore.SqlServer`). Mọi đổi schema qua EF migration. Quy ước chung: `Id BIGINT PK IDENTITY(1,1)`, `CreatedAt`/`UpdatedAt DATETIME2`, soft-delete `IsDeleted BIT DEFAULT 0`.
>
> **Lưu ý SQL Server:** không có kiểu `ENUM` native → dùng `TINYINT`/`NVARCHAR` + ràng buộc `CHECK` hoặc bảng lookup. Chuỗi dùng `NVARCHAR` (Unicode). Thời gian dùng `DATETIME2`. Boolean dùng `BIT`.

---

# 1. Tổng quan bảng
| # | Bảng | Nhóm | Mô tả |
|---|---|---|---|
| 1 | Users | Auth | Tài khoản đăng nhập mọi role |
| 2 | Roles | Auth | Admin/Staff/PT/Member |
| 3 | RefreshTokens | Auth | Refresh token JWT |
| 4 | MemberProfiles | Member | Hồ sơ hội viên |
| 5 | TrainerProfiles | PT | Hồ sơ PT |
| 6 | MembershipPackages | Membership | Gói tập |
| 7 | Memberships | Membership | Gói đã mua của member |
| 8 | Payments | Payment | Thanh toán |
| 9 | CheckIns | Operation | Lượt check-in |
| 10 | TrainerAssignments | Training | Phân công PT-Member |
| 11 | WorkoutPlans | Training | Giáo án |
| 12 | WorkoutExercises | Training | Bài tập trong giáo án |
| 13 | TrainerNotes | Training | Ghi chú PT |
| 14 | ProgressLogs | Progress | Tiến độ (cân nặng, số đo, ảnh) |
| 15 | FoodItems | Nutrition | Cơ sở dữ liệu món ăn |
| 16 | MealLogs | Nutrition | Bản ghi bữa ăn |
| 17 | MealLogItems | Nutrition | Món trong bữa ăn |
| 18 | CalorieTargets | Nutrition | Mục tiêu calo |
| 19 | AuditLogs | System | Nhật ký hành động mutating |

---

# 2. Chi tiết cột (bảng trọng tâm)

## 2.1 Users
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY(1,1) | |
| Email | NVARCHAR(255) | UNIQUE, NOT NULL | định danh đăng nhập |
| PasswordHash | NVARCHAR(255) | NOT NULL | **BCrypt cost ≥12** |
| FullName | NVARCHAR(150) | NOT NULL | |
| Phone | NVARCHAR(20) | NULL, INDEX | |
| RoleId | BIGINT | FK → Roles.Id, NOT NULL | |
| Status | TINYINT | NOT NULL, DEFAULT 1, CHECK IN (1,2) | 1=Active, 2=Locked |
| FailedLoginCount | INT | NOT NULL, DEFAULT 0 | khóa tạm khi >5/15' |
| LastLoginAt | DATETIME2 | NULL | |
| IsDeleted | BIT | NOT NULL, DEFAULT 0 | soft delete |
| CreatedAt | DATETIME2 | NOT NULL | |
| UpdatedAt | DATETIME2 | NOT NULL | |

## 2.2 Roles
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Id | BIGINT | PK, IDENTITY |
| Name | NVARCHAR(20) | UNIQUE — Admin/Staff/PT/Member |
| Description | NVARCHAR(255) | NULL |

## 2.3 RefreshTokens
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| UserId | BIGINT | FK → Users.Id | |
| TokenHash | NVARCHAR(255) | NOT NULL, INDEX | không lưu plaintext |
| ExpiresAt | DATETIME2 | NOT NULL | 7 ngày |
| RevokedAt | DATETIME2 | NULL | rotate/logout |
| CreatedAt | DATETIME2 | NOT NULL | |

## 2.4 Memberships
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| MemberId | BIGINT | FK → MemberProfiles.Id | |
| PackageId | BIGINT | FK → MembershipPackages.Id | |
| StartDate | DATE | NOT NULL | |
| EndDate | DATE | NOT NULL | = StartDate + DurationDays |
| Status | TINYINT | NOT NULL, DEFAULT 0, CHECK IN (0,1,2,3) | 0=PendingPayment,1=Active,2=Expired,3=Cancelled |
| CreatedAt / UpdatedAt | DATETIME2 | NOT NULL | |

## 2.5 Payments
| Cột | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| Id | BIGINT | PK, IDENTITY | |
| MembershipId | BIGINT | FK → Memberships.Id | |
| Amount | DECIMAL(12,2) | NOT NULL, CHECK ≥ 0 | |
| Method | TINYINT | NOT NULL, CHECK IN (1,2,3) | 1=Cash,2=Transfer,3=Card |
| Status | TINYINT | NOT NULL, DEFAULT 0 | 0=Pending,1=Paid,2=Refunded |
| PaidAt | DATETIME2 | NULL | |
| CreatedBy | BIGINT | FK → Users.Id | Staff/Admin |
| CreatedAt | DATETIME2 | NOT NULL | |

## 2.6 CheckIns
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Id | BIGINT | PK, IDENTITY |
| MemberId | BIGINT | FK → MemberProfiles.Id, INDEX |
| CheckInAt | DATETIME2 | NOT NULL (UTC) |
| CreatedBy | BIGINT | FK → Users.Id (NULL nếu self) |

## 2.7 MealLogs / MealLogItems
**MealLogs:** Id PK IDENTITY · MemberId FK · LogDate DATE · MealType TINYINT CHECK IN (1,2,3,4) (1=Breakfast,2=Lunch,3=Dinner,4=Snack) · CreatedAt DATETIME2.
**MealLogItems:** Id PK IDENTITY · MealLogId FK · FoodItemId FK · Quantity DECIMAL(8,2) CHECK > 0 · Calories DECIMAL(8,2) (tính tại thời điểm lưu).

## 2.8 AuditLogs
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | BIGINT PK IDENTITY | |
| UserId | BIGINT FK → Users.Id | ai làm |
| Action | NVARCHAR(100) | vd SELL_MEMBERSHIP |
| Entity | NVARCHAR(60) | bảng/đối tượng |
| EntityId | BIGINT | |
| Metadata | NVARCHAR(MAX) | JSON trước/sau (không chứa PII nhạy cảm) |
| CreatedAt | DATETIME2 | |

*(Các bảng còn lại — TrainerProfiles, MembershipPackages, TrainerAssignments, WorkoutPlans, WorkoutExercises, TrainerNotes, ProgressLogs, FoodItems, CalorieTargets — theo cùng quy ước Id/timestamps; cột chính nêu ở `05_DATABASE_SPEC.md`.)*

---

# 3. Quan hệ chính
- Users 1—1 MemberProfiles / TrainerProfiles (theo role).
- MemberProfiles 1—N Memberships 1—N Payments.
- MemberProfiles 1—N CheckIns.
- MemberProfiles 1—1 TrainerAssignment **active** (tối đa 1) — N—N lịch sử qua TrainerAssignments.
- TrainerAssignments → WorkoutPlans 1—N WorkoutExercises.
- MemberProfiles 1—N MealLogs 1—N MealLogItems N—1 FoodItems.
- Mọi action mutating → 1 AuditLogs.

# 4. Index & ràng buộc quan trọng
- UNIQUE: Users.Email, Roles.Name.
- INDEX: CheckIns(MemberId, CheckInAt), Memberships(MemberId, Status), MealLogs(MemberId, LogDate).
- CHECK: Quantity > 0, Amount ≥ 0, EndDate ≥ StartDate (SQL Server hỗ trợ CHECK constraint native).
- FK ON DELETE NO ACTION (dùng soft-delete, không xóa cứng dữ liệu nghiệp vụ).
- Trạng thái dùng `TINYINT` + CHECK thay cho ENUM; ánh xạ enum C# trong EF (`HasConversion<byte>()`).

# 5. Map use case → bảng
| Use case | Bảng chính |
|---|---|
| Login (UC-01) | Users, Roles, RefreshTokens |
| Sell/Renew (UC-07/08) | Memberships, Payments, MembershipPackages |
| Check-in (UC-09) | CheckIns, Memberships |
| Assign PT (UC-10) | TrainerAssignments |
| Workout (UC-12) | WorkoutPlans, WorkoutExercises |
| Progress (UC-15) | ProgressLogs |
| Meal log (UC-17) | MealLogs, MealLogItems, FoodItems |
| Calorie summary (UC-20) | MealLogItems, CalorieTargets |
| Dashboard (UC-22) | Payments, Memberships, CheckIns |
| Audit (UC-23) | AuditLogs |
