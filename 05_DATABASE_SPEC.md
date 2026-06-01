# 05 — DATABASE SPEC

**Status:** Approved | **DBMS:** SQL Server | **ORM:** EF Core 10 (Code First)

## 1. Nguyên tắc
- Quan hệ rõ, có PK/FK, map 1-1 với use cases (SG-03).
- Soft delete (`IsDeleted`, `DeletedAt`) cho entity nghiệp vụ.
- Audit cột chuẩn: `CreatedAt`, `UpdatedAt` (UTC) cho mọi bảng chính.
- Mọi thay đổi qua EF Migration (xem `15_DATABASE_SCHEMA.md`).

## 2. Bảng chính (Core)
| Bảng | Vai trò | Quan hệ chính |
|---|---|---|
| Users | Tài khoản đăng nhập | 1-1 Members/Trainers (theo role) |
| Roles | Admin/Staff/PT/Member | n-1 với Users (hoặc enum) |
| Members | Hồ sơ hội viên | 1-1 Users; 1-n Memberships, CheckIns |
| Trainers | Hồ sơ PT | 1-1 Users; 1-n TrainerAssignments |
| MembershipPackages | Gói tập mẫu | 1-n Memberships |
| Memberships | Gói đã gán cho Member | n-1 Members, Packages; 1-n Payments |
| Payments | Giao dịch thanh toán | n-1 Memberships |
| CheckIns | Lượt đến phòng | n-1 Members |
| TrainerAssignments | Phân công PT↔Member | n-1 Trainers, Members |
| WorkoutPlans | Giáo án | n-1 Members, Trainers; 1-n WorkoutExercises |
| WorkoutExercises | Bài tập trong giáo án | n-1 WorkoutPlans |
| TrainerNotes | Ghi chú PT theo ngày | n-1 Members, Trainers |
| ProgressLogs | Tiến độ cơ thể | n-1 Members |
| AuditLogs | Log hành động | n-1 Users (actor) |

## 3. Bảng dinh dưỡng (Core/Secondary)
| Bảng | Vai trò |
|---|---|
| FoodItems | Danh mục món ăn (calo, macro/100g hoặc /khẩu phần) |
| MealLogs | Bữa ăn theo ngày của Member |
| MealLogItems | Từng món trong MealLog (FoodItem + khẩu phần) |
| CalorieTargets | Mục tiêu calo/macro mỗi ngày (Secondary) |

## 4. Quan hệ then chốt (ràng buộc nghiệp vụ)
- Member ↔ Membership: 1 Member có nhiều Membership theo thời gian; **tối đa 1 Active** tại 1 thời điểm.
- Member ↔ PT: qua TrainerAssignment; **tối đa 1 active** (A-06).
- Membership ↔ Payment: 1 membership có ≥1 payment; status Active sau khi có payment hợp lệ.
- MealLog ↔ MealLogItem: 1-n; xóa MealLog → xóa kèm items (cascade).

## 5. Ưu tiên triển khai dữ liệu (theo sprint)
1. **Sprint 1:** Users, Roles, Members, Trainers (auth + hồ sơ).
2. **Sprint 2:** Packages, Memberships, Payments, CheckIns (core operation).
3. **Sprint 3:** TrainerAssignments, WorkoutPlans/Exercises, TrainerNotes, ProgressLogs.
4. **Sprint 3+:** FoodItems, MealLogs, MealLogItems, CalorieTargets, AuditLogs.

## 6. Index gợi ý
- `Memberships(MemberId, Status, EndDate)` — cho check-in & dashboard.
- `CheckIns(MemberId, CheckInAt)` — báo cáo theo ngày.
- `MealLogItems(MealLogId)`, `MealLogs(MemberId, LogDate)` — calorie summary.
- `Users(Email)` unique; `Members(PhoneNumber)` unique.

> Schema chi tiết (kiểu cột, enum, FK, migration) ở `15_DATABASE_SCHEMA.md`.
