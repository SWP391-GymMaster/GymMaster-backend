---
description: "Task list — PT Assignment, Workout Plan & Trainer Notes"
---

# Tasks: PT Assignment, Workout Plan & Trainer Notes

**Feature**: `005-pt-training`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 38/41 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code, `[ ]` = còn nợ.

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 Tạo slice `backend/GymMaster.API/Features/Training/`
- [X] T002 Migration `database/006_spec5_pt_training.sql` — tạo 5 bảng của feature

## Phase 2: Foundational (chặn mọi user story)

- [X] T003 `Entities/TrainerAssignment.cs` — `Status {1 Active, 2 Ended}`, `StartDate`, `EndDate` nullable
- [X] T004 [P] `Entities/ExerciseCatalog.cs` — `Name` UNIQUE, `MuscleGroup`, `IsActive`
- [X] T005 [P] `Entities/WorkoutPlan.cs` + `WorkoutExercise.cs` — `UNIQUE(WorkoutPlanId, SortOrder)`, cascade delete theo plan
- [X] T006 [P] `Entities/TrainerNote.cs`
- [X] T007 ★ **Cửa quyền dùng chung**: hàm kiểm "PT có assignment Active với member này" đặt ở **Service layer** → **NFR-02**, **FR-PT-03**
- [X] T008 Đăng ký DI `IAssignmentService`, `IWorkoutPlanService`, `ITrainerNoteService` trong `Program.cs`

**Checkpoint**: có bảng + cửa quyền → các user story bắt đầu được.

---

## Phase 3: US1 — Admin phân công PT (P1) 🎯 MVP

**Goal**: Admin gán PT cho hội viên đủ điều kiện, đổi PT không cần thao tác gỡ.
**Independent Test**: phân công cho member có gói `SupportsPT` còn hạn → 201; member gói thường → 409 `PACKAGE_PT_REQUIRED`; phân công PT khác cho member đã có PT → assignment cũ `Ended`, mới `Active`.

- [X] T009 [US1] `PtTrainingDtos.cs` — `TrainerAssignmentResponse`
- [X] T010 [US1] `AssignmentService.CreateAsync` — tạo assignment `Active` → **FR-PT-01**
- [X] T011 [US1] Kiểm điều kiện gói PT: `Membership.Status=Active` AND `EndDate ≥ hôm nay (VN)` AND `Package.SupportsPT=true`, **dùng lại** `MembershipLifecycle` của spec 003 → **FR-PT-05**, **ARCH-03**
- [X] T012 [US1] Không đủ điều kiện → 409 `PACKAGE_PT_REQUIRED` → **FR-PT-05**
- [X] T013 [US1] Tự đóng assignment cũ (Status→`Ended`, `EndDate`=hôm nay) trước khi tạo mới, **không trả lỗi** → **FR-PT-02**
- [X] T014 [US1] Ghi AuditLog `ASSIGN_PT` → **NFR-03**
- [X] T015 [US1] `GET /assignments/candidates/members?query=&includeAssigned=` — **chỉ liệt kê member đủ điều kiện PT**
- [X] T016 [US1] `GET /assignments/candidates/trainers?query=&specialty=` — kèm `assignedCount` / `capacity=10` (số gợi ý, không chặn)
- [X] T017 [US1] `AssignmentsController.cs` — `[Authorize(Roles="admin")]`
- [X] T018 [US1] Unit test `tests/GymMaster.Api.Tests/AssignmentServiceTests.cs`

**Checkpoint**: có quan hệ PT ↔ member → US2/US3 mới có cơ sở kiểm quyền.

---

## Phase 4: US2 — PT lập giáo án (P1)

**Goal**: PT tạo giáo án kèm danh sách bài tập cho hội viên của mình.
**Independent Test**: PT tạo plan ≥1 bài tập cho member được phân công → 201, bài tập mới tự vào catalog; tạo cho member người khác → 403; plan rỗng → 422.

- [X] T019 [US2] `GET /exercises` (`ExercisesController.cs`) — trả catalog `IsActive`
- [X] T020 [US2] `WorkoutPlanService.CreateAsync` — qua cửa quyền T007, lưu plan `Active` → **FR-WP-01**
- [X] T021 [US2] Bài tập **nhập theo tên**: tra `exercise_catalog` không phân biệt hoa/thường, không có thì tạo mới → **FR-WP-01**
- [X] T022 [US2] Plan rỗng (0 bài tập) → 422 `EMPTY_PLAN`; thiếu tiêu đề → 400 `VALIDATION_ERROR`
- [X] T023 [US2] `POST/GET /members/{id}/workout-plans`
- [X] T024 [US2] `WorkoutPlanService.UpdateAsync` — cập nhật tiêu đề/mục tiêu/ngày/status; có gửi `exercises` thì **thay toàn bộ** → **FR-WP-02**
- [X] T025 [US2] `WorkoutPlanService.DeleteAsync` — xoá plan + bài tập (cascade), 204 → **FR-WP-03**
- [X] T026 [US2] `WorkoutPlansController.cs` — sửa/xoá chỉ cho **PT chủ plan** (403 nếu không)
- [X] T027 [US2] `WorkoutExerciseResponse { id, name, muscleGroup, orderIndex, sets, reps, note }` — `reps` trả về dạng chuỗi

---

## Phase 5: US3 — PT ghi chú luyện tập (P2)

**Goal**: PT lưu nhận xét theo buổi cho hội viên của mình.
**Independent Test**: PT ghi note cho member được phân công → 201; sửa note của PT khác → 403.

- [X] T028 [US3] `TrainerNoteService.CreateAsync` — lưu kèm `NoteDate` + tác giả → **FR-NOTE-01**
- [X] T029 [US3] `POST/GET /members/{id}/notes` — qua cửa quyền T007
- [X] T030 [US3] `PUT/DELETE /trainer-notes/{id}` — chỉ **chủ note**, 403 nếu không → **FR-NOTE-02, 03**
- [X] T031 [US3] Thiếu nội dung → 400 `VALIDATION_ERROR`

---

## Phase 6: US4 — Góc nhìn PT & Member (P2)

**Goal**: PT thấy danh sách hội viên mình kèm; hội viên xem được giáo án/ghi chú của mình.
**Independent Test**: PT gọi `GET /pt/members` chỉ thấy member được phân công; Member gọi `GET /members/me/workout-plans` thấy giáo án của mình.

- [X] T032 [US4] `PtController.cs` — `GET /pt/members` trả member đang được phân công cho PT đăng nhập
- [X] T033 [US4] PT chưa có hồ sơ `trainer_profiles` → 404 `TRAINER_NOT_FOUND`
- [X] T034 [US4] Ownership khi đọc: Member chỉ của mình · PT chỉ member được phân công · Admin tất cả → **FR-PT-04**
- [X] T035 [US4] Self-service `GET /members/me/workout-plans`, `GET /members/me/notes`
- [X] T036 [US4] (dùng chung với spec 004) `POST /pt/members/{id}/checkins`, `GET /pt/checkins/today` trong `PtController.cs`

---

## Phase 7: Polish & Cross-cutting

- [ ] T038 **Còn nợ** — unit test `tests/GymMaster.Api.Tests/WorkoutPlanServiceTests.cs` cho luồng tra/tạo `exercise_catalog` theo tên và cho `UpdateAsync` thay toàn bộ exercises (D-506, D-507 là hai chỗ rủi ro nhất)
- [ ] T039 **Còn nợ** — unit test `tests/GymMaster.Api.Tests/TrainerNoteServiceTests.cs` cho kiểm chủ sở hữu note
- [X] T040 AuditLog cho giáo án + ghi chú: `WorkoutPlanService` ghi `CREATE/UPDATE/DELETE_WORKOUT_PLAN`, `TrainerNoteService` ghi `CREATE/UPDATE/DELETE_TRAINER_NOTE` → AUDIT-01 phủ đủ

---

## Dependencies & Execution Order

- **Phụ thuộc ngoài**: spec 002 (`member_profiles`, `trainer_profiles`), spec 003 (`MembershipLifecycle` + `Package.SupportsPT` — **không có gói PT thì không phân công được**).
- **T007 (cửa quyền) chặn US2 và US3**.
- **US1 chặn US2, US3, US4**: chưa có assignment thì mọi thao tác PT đều 403.
- **US2, US3 độc lập với nhau** → làm song song được.
- **Feature phụ thuộc ngược**: spec 004 (PT check-in hộ), spec 006 (360° hiển thị `assignedPT`, PT ghi tiến độ hộ member).

```text
[002·003] → Setup → Foundational(T007 ★) → US1 ─┬→ US2
                                                ├→ US3  → US4 → Polish
                                                └→ [004 · 006 dùng assignment]
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01 | T010, T014 | `AssignmentServiceTests.cs` |
| AC-02 | T013 | `AssignmentServiceTests.cs` |
| AC-07 | T011, T012 | `AssignmentServiceTests.cs` |
| AC-03 | T032 | black-box |
| AC-04 | T020, T021 | **chưa có unit test — T038** |
| AC-05 | T007, T026 | black-box |
| AC-06 | T034, T035 | black-box |
| AC-08 | T025, T030 | **chưa có unit test — T038/T039** |
