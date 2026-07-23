# Implementation Plan: PT Assignment, Workout Plan & Trainer Notes

**Feature Branch**: `005-pt-training` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)
**Status**: `Implemented` — **as-built plan** (đồng bộ ngược từ code đang chạy)
**Input**: `docs/03-Interface-Specs/feature-specs/005-pt-training/spec.md`

---

## 1. Summary

Feature nhiều ràng buộc quyền nhất hệ thống. Ba nghiệp vụ (phân công PT, giáo án, ghi chú) đều xoay quanh **một bất biến duy nhất**: *"PT chỉ được thao tác trên hội viên đang có `TrainerAssignment` Status=Active với mình"*. Bất biến này được kiểm ở **Service layer** (NFR-02), không phải ở controller, để mọi đường vào đều đi qua cùng một cửa.

Điểm kiến trúc đáng chú ý: quyền dùng PT **không lưu cờ trên member** mà suy ra động từ gói đang hiệu lực (spec 003 — `Package.SupportsPT`). Hệ quả: gói hết hạn là mất quyền PT ngay, không cần job đồng bộ, nhưng mỗi lần phân công phải JOIN sang `membership_packages`.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | EF Core 10 (SqlServer) |
| **Storage** | SQL Server — `trainer_assignments`, `workout_plans`, `workout_exercises`, `exercise_catalog`, `trainer_notes` |
| **Testing** | xUnit — `tests/GymMaster.Api.Tests/AssignmentServiceTests.cs` |
| **Target Platform** | Cloud Run + Cloud SQL |
| **Performance Goals** | Lưu giáo án < 500ms (NFR-01) |
| **Constraints** | Tối đa **1 assignment Active/member** (D-12); capacity gợi ý 10 member/PT |
| **Scale/Scope** | 13 endpoint, 4 controller, 5 bảng |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| GBL-05 — identity từ JWT claim | ✅ PASS | mọi kiểm quyền dùng `CurrentUserId` |
| GBL-04 — kiểm quyền ở Service layer, không ở controller | ✅ PASS | `AssignmentService`, `WorkoutPlanService`, `TrainerNoteService` (NFR-02) |
| GBL-02 — không lặp business rule | ✅ PASS | điều kiện gói PT dùng lại `MembershipLifecycle` + `Package.SupportsPT` (spec 003) |
| AUDIT-01 — hành động quan trọng ghi AuditLog | ✅ PASS | `ASSIGN_PT` (NFR-03) · `CREATE/UPDATE/DELETE_WORKOUT_PLAN` · `CREATE/UPDATE/DELETE_TRAINER_NOTE` — cả 3 service đều inject `IAuditService` |
| DATA-01 — không xoá cứng | ⚠️ LỆCH CÓ CHỦ Ý | `DELETE /workout-plans/{id}` và `/trainer-notes/{id}` xoá cứng — xem [Complexity Tracking](#8-complexity-tracking) |
| ARCH-02 — wrapper `ApiResponse<T>` | ✅ PASS | mọi action |

## 4. Project Structure

```text
backend/GymMaster.API/Features/Training/
├── AssignmentsController.cs      # route "api/v1/assignments" — Admin phân công + danh sách ứng viên
├── IAssignmentService.cs · AssignmentService.cs
├── PtController.cs               # route "api/v1/pt" — góc nhìn của PT (members, check-in hộ)
├── WorkoutPlansController.cs     # route "api/v1/workout-plans" — sửa/xoá theo id
├── IWorkoutPlanService.cs · WorkoutPlanService.cs
├── ExercisesController.cs        # route "api/v1/exercises" — catalog
├── TrainerNotesController.cs     # route "api/v1/trainer-notes" — sửa/xoá theo id
├── ITrainerNoteService.cs · TrainerNoteService.cs
├── PtTrainingDtos.cs             # TrainerAssignmentResponse, WorkoutPlanResponse, WorkoutExerciseResponse
└── (ProgressService.cs, MemberProgressController.cs — thuộc spec 006)

backend/GymMaster.API/Entities/
├── TrainerAssignment.cs          # Status {1 Active, 2 Ended}
├── WorkoutPlan.cs                # Status {1 Active, 2 Completed, 3 Cancelled}
├── WorkoutExercise.cs            # UNIQUE(WorkoutPlanId, SortOrder), cascade theo plan
├── ExerciseCatalog.cs            # Name UNIQUE
└── TrainerNote.cs

database/
└── 006_spec5_pt_training.sql     # tạo 5 bảng của feature

tests/GymMaster.Api.Tests/
└── AssignmentServiceTests.cs     # (chưa có WorkoutPlanServiceTests / TrainerNoteServiceTests — T-042/T-043)
```

**Structure Decision**: các endpoint tạo giáo án/ghi chú nằm dưới `/members/{id}/…` (đọc theo hội viên) còn sửa/xoá nằm dưới `/workout-plans/{id}` và `/trainer-notes/{id}` (thao tác theo bản ghi). Hai gốc route ⇒ hai controller, nhưng **chung một service** — điều kiện quyền chỉ tồn tại ở một nơi.

## 5. Design Decisions

> **Chi tiết hoá ADR dự án**: [D-12](../../../06-Management/decision-log.md) (tối đa 1 PT active) → D-501, D-502.
> **Ngoại lệ có chủ ý**: [D-09](../../../06-Management/decision-log.md) quy định soft delete cho dữ liệu nghiệp vụ, nhưng **D-509** ở đây xoá cứng giáo án/ghi chú — lý do ở [Complexity Tracking](#8-complexity-tracking); đây cũng là gốc của việc B-02 trong [BACKLOG](../BACKLOG.md).

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-501 | Mô hình PT **1-1**: tối đa 1 assignment Active/member (D-12) | Trách nhiệm huấn luyện rõ ràng; đơn giản hoá mọi truy vấn quyền | Không mô hình hoá được phòng gym có PT chính + PT phụ |
| D-502 | Phân công PT mới → **tự đóng** assignment cũ, không báo lỗi | Đổi PT là thao tác thường ngày; bắt Admin gỡ trước rồi gán sau là thừa một bước | Đổi nhầm sẽ âm thầm kết thúc assignment cũ, chỉ AuditLog ghi lại |
| D-503 | Quyền dùng PT **suy ra từ gói**, không lưu cờ trên member | Hết hạn gói là mất quyền ngay, không cần job đồng bộ | Mỗi lần phân công phải JOIN `memberships` × `membership_packages` |
| D-504 | Không đủ điều kiện gói → **409** `PACKAGE_PT_REQUIRED` (không phải 403) | Đây là xung đột trạng thái dữ liệu, không phải thiếu quyền truy cập | 409 cho lỗi nghiệp vụ hơi lệch quy ước REST thuần |
| D-505 | Danh sách ứng viên (`candidates/members`) **lọc sẵn** member đủ điều kiện | Admin không chọn nhầm rồi mới bị 409 | Query nặng hơn; phải đồng bộ với luật của D-503 |
| D-506 | Bài tập nhập **theo tên**, tra/tự tạo `exercise_catalog` (không phân biệt hoa thường) | PT gõ tên bài tập tự nhiên, không phải chọn từ danh mục cố định | Catalog dễ phình vì lỗi chính tả (`"Ben Press"` ≠ `"Bench Press"`) |
| D-507 | Sửa giáo án có gửi `exercises` → **thay toàn bộ** danh sách | Diff từng bài tập phức tạp hơn nhiều so với giá trị mang lại | Mất `Id` bài tập cũ; client phải gửi đủ danh sách, không gửi được delta |
| D-508 | `UNIQUE(WorkoutPlanId, SortOrder)` | Thứ tự bài tập là dữ liệu nghiệp vụ, không được trùng | Chèn bài vào giữa phải đánh số lại phần đuôi |
| D-509 | Xoá **cứng** giáo án/ghi chú (cascade `workout_exercises`) | Đây là bản nháp huấn luyện, không phải chứng từ; giữ lại chỉ làm rối | Lệch DATA-01; xoá nhầm không khôi phục được |
| D-510 | Capacity PT = **10** chỉ là số hiển thị gợi ý, không phải ràng buộc cứng | Giúp Admin cân tải mà không chặn nghiệp vụ khi cao điểm | Có thể vượt 10 mà hệ thống không cảnh báo |

## 6. Data Flow

```text
Phân công PT:
  POST /assignments {memberId, trainerId}         [Admin]
    → AssignmentService.CreateAsync
        ├─ member/trainer tồn tại?                          → 404
        ├─ member có Membership Active AND EndDate ≥ hôm nay(VN)
        │  AND Package.SupportsPT == true ?                 → 409 PACKAGE_PT_REQUIRED
        │     (dùng MembershipLifecycle + SupportsPT — spec 003)
        ├─ đang có assignment Active? → Status=Ended, EndDate=hôm nay   (D-502)
        ├─ INSERT trainer_assignments { Status=Active }
        └─ AuditLog "ASSIGN_PT"

Cửa quyền dùng chung (mọi thao tác của PT):
  actor là PT → tìm trainer_profiles theo CurrentUserId
             → tồn tại trainer_assignments { TrainerId, MemberId, Status=Active } ?
             → không: 403 FORBIDDEN

Tạo giáo án:
  POST /members/{id}/workout-plans {title, exercises:[{name, sets, reps, note, orderIndex}]}
    → WorkoutPlanService
        ├─ cửa quyền ở trên
        ├─ exercises rỗng                → 422 EMPTY_PLAN
        ├─ mỗi bài: tra exercise_catalog theo Name (case-insensitive)
        │           không có → INSERT catalog mới            (D-506)
        └─ INSERT workout_plans + workout_exercises (SortOrder = orderIndex)

Sửa/xoá:
  PUT/DELETE /workout-plans/{id}  → chỉ PT **chủ plan** (TrainerId khớp) → 403 nếu không
  PUT/DELETE /trainer-notes/{id}  → chỉ PT **chủ note**                  → 403 nếu không

Đọc:
  Member  → chỉ giáo án/ghi chú của chính mình
  PT      → chỉ của member được phân công active
  Admin   → tất cả
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-PT-01 | `AssignmentService.CreateAsync` + `IAuditService` (`ASSIGN_PT`) |
| FR-PT-02 | `AssignmentService` — tự đóng assignment cũ (Status→Ended, EndDate=hôm nay) |
| FR-PT-03 | Cửa quyền chung trong `AssignmentService` / `WorkoutPlanService` / `TrainerNoteService` |
| FR-PT-05 | `AssignmentService` — 409 `PACKAGE_PT_REQUIRED`, dùng `Package.SupportsPT` (spec 003) |
| FR-WP-01 | `WorkoutPlanService.CreateAsync` + `Entities/ExerciseCatalog.cs` |
| FR-WP-02 | `WorkoutPlanService.UpdateAsync` — thay toàn bộ danh sách bài tập |
| FR-WP-03 | `WorkoutPlanService.DeleteAsync` — cascade `workout_exercises` |
| FR-NOTE-01 | `TrainerNoteService.CreateAsync` |
| FR-NOTE-02, 03 | `TrainerNoteService` — kiểm chủ sở hữu note |
| FR-PT-04 | Kiểm ownership theo role ở cả 3 service |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| **Xoá cứng** giáo án/ghi chú, lệch DATA-01 (D-509) | Là bản nháp huấn luyện chứ không phải chứng từ tài chính; soft-delete sẽ làm mọi query phải lọc thêm `IsDeleted` | Soft-delete → thêm cột + filter ở 6 query, đổi lại giá trị gần bằng 0 |
| **Xoá cứng** nhưng có bù bằng audit | `DELETE_WORKOUT_PLAN` / `DELETE_TRAINER_NOTE` được ghi vào `audit_logs` trước khi bản ghi biến mất → vẫn truy được ai xoá, lúc nào, bản ghi nào | Không ghi audit → xoá cứng thành mất dấu hoàn toàn, không chấp nhận được |
| 4 controller cho một slice | 4 gốc route khác nhau (`/assignments`, `/pt`, `/workout-plans`, `/trainer-notes`) — ràng buộc `[Route]` của ASP.NET Core | Gộp → route tuyệt đối rải rác, khó tra |
| Catalog bài tập tự sinh theo tên (D-506) | PT cần gõ tự do; danh mục cố định sẽ thiếu bài | Bắt chọn từ danh mục → phải có màn quản trị catalog, ngoài phạm vi |
| Thay toàn bộ exercises khi update (D-507) | Diff danh sách có thứ tự là bài toán phức tạp | Diff từng bài → nhiều mã lỗi biên, rủi ro sai thứ tự cao |
| Chưa có test cho `WorkoutPlanService` / `TrainerNoteService` | Chỉ `AssignmentService` có unit test | Bỏ qua → không chấp nhận; xem T-042, T-043 |
