# Feature Specification: PT Assignment, Workout Plan & Trainer Notes

**Feature Branch**: `005-pt-training`
**Created**: 2026-05-30
**Status**: Implemented (spec đồng bộ theo code 2026-07-15)
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: archive/06_FEATURE_SPECS (F3), srs-use-cases (UC-10/11/12/13), requirements (FR-PT/WP/NOTE), D-12

> EARS legend như spec 001. Mọi path dưới `/api/v1`.

---

## 1. Context & Goal
Admin phân công PT cho hội viên (mô hình 1-1, D-12); PT lập giáo án + bài tập và ghi chú luyện tập cho hội viên của mình. Mục tiêu: ràng buộc quyền sở hữu (PT chỉ thao tác trên member được phân công active), tối đa 1 PT active/member, chỉ hội viên có gói hỗ trợ PT còn hạn mới vào luồng PT.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Admin | Phân công/đổi PT cho Member; xem danh sách ứng viên member/PT |
| PT | Xem member được phân công; tạo/sửa/xoá giáo án, bài tập, ghi chú |
| Member | Xem giáo án/ghi chú của mình (read) |
| System | Kiểm tra quyền sở hữu + gói PT, đóng assignment cũ, ghi AuditLog |

## 3. Functional Requirements (EARS)
- **FR-PT-01 (Event):** WHEN Admin phân công PT cho Member đủ điều kiện, THE system SHALL tạo TrainerAssignment `Active` và ghi AuditLog `ASSIGN_PT`.
- **FR-PT-02 (Unwanted):** IF Member đã có PT active, THEN THE system SHALL **tự đóng assignment cũ** (Status→Ended, EndDate=hôm nay) trước khi tạo mới, đảm bảo tối đa 1 active (không trả lỗi).
- **FR-PT-03 (Optional):** WHERE PT thao tác trên Member KHÔNG được phân công active cho mình, THE system SHALL trả 403.
- **FR-PT-05 (Unwanted):** IF Member KHÔNG có Membership đủ điều kiện PT (không thoả `Status=Active` AND `EndDate ≥ hôm nay` AND `Package.SupportsPT=true`), THEN THE system SHALL từ chối phân công với **409 `PACKAGE_PT_REQUIRED`**. → Danh sách ứng viên member (`candidates/members`) chỉ liệt kê member đủ điều kiện.
- **FR-WP-01 (Event):** WHEN PT tạo WorkoutPlan cho Member được phân công (≥1 bài tập), THE system SHALL lưu plan `Active` kèm danh sách WorkoutExercise; **bài tập nhập theo tên** — hệ thống tra `exercise_catalog` (không phân biệt hoa/thường) hoặc tạo mới bản ghi catalog.
- **FR-WP-02 (Event):** WHEN PT sửa giáo án của mình (`PUT /workout-plans/{id}`), THE system SHALL cập nhật tiêu đề/mục tiêu/ngày/status và (nếu gửi `exercises`) **thay toàn bộ** danh sách bài tập.
- **FR-WP-03 (Event):** WHEN PT xoá giáo án của mình (`DELETE /workout-plans/{id}`), THE system SHALL xoá plan + bài tập kèm theo (204).
- **FR-NOTE-01 (Event):** WHEN PT ghi TrainerNote cho Member được phân công, THE system SHALL lưu note kèm NoteDate + tác giả.
- **FR-NOTE-02/03 (Event):** WHEN PT sửa/xoá ghi chú của **chính mình** (`PUT/DELETE /trainer-notes/{id}`), THE system SHALL cập nhật/xoá (403 nếu không phải chủ note).
- **FR-PT-04 (Optional):** WHERE người gọi là Member, THE system SHALL chỉ cho xem giáo án/ghi chú của chính mình; PT chỉ xem member được phân công; Admin xem tất cả.

## 4. Non-functional Requirements
- **NFR-01:** Lưu giáo án < 500ms.
- **NFR-02:** Quyền sở hữu kiểm tra ở Service layer dựa trên TrainerAssignment active.
- **NFR-03:** Mọi phân công/đổi PT ghi AuditLog.

## 5. Data Model
- **trainer_assignments**(Id, MemberId→member_profiles, TrainerId→trainer_profiles, Status TINYINT{1 Active,2 Ended}, StartDate, EndDate nullable, CreatedByUserId, CreatedAt, UpdatedAt)
- **workout_plans**(Id, MemberId, TrainerId, Title, Goal, StartDate, EndDate?, Status TINYINT{1 Active,2 Completed,3 Cancelled}, CreatedAt, UpdatedAt)
- **workout_exercises**(Id, WorkoutPlanId→workout_plans[cascade], ExerciseId→exercise_catalog, SortOrder, Sets, Reps, WeightKg?, DurationMinutes?, RestSeconds?, Note) — UNIQUE(WorkoutPlanId, SortOrder)
- **exercise_catalog**(Id, Name[UNIQUE], MuscleGroup?, Description?, IsActive)
- **trainer_notes**(Id, TrainerId, MemberId, NoteDate, Content, CreatedByUserId, CreatedAt, UpdatedAt)

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/assignments | Admin | {memberId, trainerId} | 201 TrainerAssignmentResponse | 404, 409 `PACKAGE_PT_REQUIRED` |
| GET | /api/v1/assignments/candidates/members?query=&includeAssigned= | Admin | — | 200 {items, total} (chỉ member đủ điều kiện PT) | 401, 403 |
| GET | /api/v1/assignments/candidates/trainers?query=&specialty= | Admin | — | 200 {items, total} (kèm assignedCount/capacity=10) | 401, 403 |
| GET | /api/v1/pt/members | PT | — | 200 (member được phân công) | 401, 404 |
| GET | /api/v1/exercises | tất cả | — | 200 (catalog active) | 401 |
| POST | /api/v1/members/{id}/workout-plans | PT(assigned) | {title, goal?, startDate?, endDate?, exercises:[{name, sets?, reps?, note?, orderIndex}]} | 201 | 403, 404, 422 `EMPTY_PLAN` |
| GET | /api/v1/members/{id}/workout-plans | PT(assigned), Member(self), Admin | — | 200 | 403, 404 |
| PUT | /api/v1/workout-plans/{id} | PT(owner) | {title?, goal?, startDate?, endDate?, status?, exercises?} | 200 | 403, 404, 422 |
| DELETE | /api/v1/workout-plans/{id} | PT(owner) | — | 204 | 403, 404 |
| POST | /api/v1/members/{id}/notes | PT(assigned) | {content} | 201 | 403, 404 |
| GET | /api/v1/members/{id}/notes | PT(assigned), Member(self), Admin | — | 200 | 403, 404 |
| PUT | /api/v1/trainer-notes/{id} | PT(owner) | {content} | 200 | 400, 403, 404 |
| DELETE | /api/v1/trainer-notes/{id} | PT(owner) | — | 204 | 403, 404 |

> Member self-service: `GET /members/me/workout-plans`, `GET /members/me/notes` (spec 002/006).
> **WorkoutExerciseResponse:** `{ id, name, muscleGroup, orderIndex, sets, reps, note }` (reps trả về dạng chuỗi).

## 7. Error Handling (EARS Unwanted)
- IF Member/PT/plan/note không tồn tại, THEN 404 `NOT_FOUND`.
- IF Member không có gói hỗ trợ PT đang hiệu lực, THEN 409 `PACKAGE_PT_REQUIRED`.
- IF PT thao tác trên member/plan/note không thuộc mình, THEN 403 `FORBIDDEN`.
- IF plan rỗng (0 bài tập), THEN 422 `EMPTY_PLAN`.
- IF thiếu tiêu đề giáo án/nội dung ghi chú, THEN 400 `VALIDATION_ERROR`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given Member đủ điều kiện chưa có PT, When Admin phân công, Then TrainerAssignment Active + AuditLog.
- [ ] **AC-02:** Given Member đã có PT active, When phân công PT mới, Then assignment cũ Ended, mới Active (tối đa 1).
- [ ] **AC-07:** Given Member chỉ có gói thường hoặc gói PT hết hạn, When phân công, Then 409 `PACKAGE_PT_REQUIRED`; gói PT còn hạn → 201.
- [ ] **AC-03:** Given PT đăng nhập, When xem danh sách member, Then chỉ thấy member được phân công.
- [ ] **AC-04:** Given PT, When tạo plan ≥1 bài tập (theo tên) cho member của mình, Then lưu thành công, catalog tự tạo nếu bài mới.
- [ ] **AC-05:** Given PT, When tạo/sửa plan cho member người khác, Then 403.
- [ ] **AC-06:** Given Member, When xem giáo án của mình, Then thấy plan + bài tập.
- [ ] **AC-08:** Given PT, When xoá giáo án/ghi chú của mình, Then 204; của PT khác → 403.

## 9. Out of Scope
- Lịch buổi tập theo slot/booking, video hướng dẫn, thư viện bài tập dùng chung, chat PT-Member.
