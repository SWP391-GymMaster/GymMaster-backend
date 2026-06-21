# Feature Specification: PT Assignment, Workout Plan & Trainer Notes

**Feature Branch**: `005-pt-training`
**Created**: 2026-05-30
**Status**: Approved
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 06_FEATURE_SPECS (F3), 03_SRS (UC-10/11/12/13), 04 (FR-PT/WP/NOTE), D-12

> EARS legend như spec 001.

---

## 1. Context & Goal
Admin phân công PT cho hội viên (mô hình 1-1, D-12); PT lập giáo án + bài tập và ghi chú luyện tập cho hội viên của mình. Mục tiêu: ràng buộc quyền sở hữu (PT chỉ thao tác trên member được phân công), tối đa 1 PT active/member.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Admin | Phân công/đổi PT cho Member |
| PT | Xem member được phân công; tạo giáo án, bài tập, ghi chú |
| Member | Xem giáo án/ghi chú của mình (read) |
| System | Kiểm tra quyền sở hữu, đóng assignment cũ, ghi AuditLog |

## 3. Functional Requirements (EARS)
- **FR-PT-01 (Event):** WHEN Admin phân công PT cho Member chưa có PT active, THE system SHALL tạo TrainerAssignment `Active` và ghi AuditLog `ASSIGN_PT`.
- **FR-PT-02 (Unwanted):** IF Member đã có PT active, THEN THE system SHALL đóng assignment cũ (set EndedAt) trước khi tạo mới, đảm bảo tối đa 1 active.
- **FR-PT-03 (Optional):** WHERE PT thao tác trên Member KHÔNG được phân công cho mình, THE system SHALL trả 403.
- **FR-PT-05 (Unwanted):** IF Member KHÔNG có Membership đang hiệu lực thuộc gói hỗ trợ PT (tức KHÔNG thỏa: `Status=Active` AND `EndDate>=hôm nay` AND `Package.SupportsPT=true`), THEN THE system SHALL từ chối phân công/đặt lịch PT với 422 `NO_PT_PACKAGE`. → Hội viên thường (gói không PT) hoặc gói PT đã hết hạn KHÔNG vào được luồng PT. Thuộc tính `SupportsPT` định nghĩa ở spec 003 (FR-PKG-03/04).
- **FR-WP-01 (Event):** WHEN PT tạo WorkoutPlan cho Member được phân công, THE system SHALL lưu plan kèm danh sách WorkoutExercise.
- **FR-WP-02 (Event):** WHEN PT thêm/sửa bài tập trong plan của mình, THE system SHALL lưu (tên, sets, reps, ghi chú).
- **FR-NOTE-01 (Event):** WHEN PT ghi TrainerNote cho Member được phân công, THE system SHALL lưu note kèm timestamp và tác giả.
- **FR-PT-04 (Optional):** WHERE người gọi là Member, THE system SHALL chỉ cho xem giáo án/ghi chú của chính mình.

## 4. Non-functional Requirements
- **NFR-01:** Lưu giáo án < 500ms.
- **NFR-02:** Quyền sở hữu kiểm tra ở Service layer dựa trên TrainerAssignment active.
- **NFR-03:** Mọi phân công/đổi PT ghi AuditLog.

## 5. Data Model
- **TrainerAssignments**(Id, MemberId→MemberProfiles, TrainerId→TrainerProfiles, Status{Active,Ended}, AssignedAt, EndedAt nullable, CreatedAt)
- **WorkoutPlans**(Id, MemberId, TrainerId, Title, StartDate, Status, CreatedAt, UpdatedAt)
- **WorkoutExercises**(Id, WorkoutPlanId→WorkoutPlans, Name, Sets, Reps, Note, OrderIndex)
- **TrainerNotes**(Id, MemberId, TrainerId, Content, CreatedAt)
- Xem `15_DATABASE_SCHEMA.md` §1/§3.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/assignments | Admin | {memberId, trainerId} | 201 | 404, 422 (`NO_PT_PACKAGE` nếu member không có gói PT còn hạn) |
| GET | /api/pt/members | PT | — | 200 (assigned list) | 401, 403 |
| POST | /api/members/{id}/workout-plans | PT(assigned) | {title, exercises[]} | 201 | 403, 404 |
| PUT | /api/workout-plans/{id} | PT(owner) | {…} | 200 | 403, 404 |
| POST | /api/members/{id}/notes | PT(assigned) | {content} | 201 | 403, 404 |
| GET | /api/members/{id}/workout-plans | PT(assigned), Member(self), Admin | — | 200 | 403, 404 |

## 7. Error Handling (EARS Unwanted)
- IF Member/PT không tồn tại, THEN 404 `NOT_FOUND`.
- IF Member đã có PT active (và không cho phép ghi đè), THEN 422 `ALREADY_ASSIGNED`.
- IF Member không có gói hỗ trợ PT đang hiệu lực, THEN 422 `NO_PT_PACKAGE` (xem FR-PT-05).
- IF PT thao tác trên member không thuộc mình, THEN 403 `FORBIDDEN`.
- IF plan rỗng (0 bài tập) khi yêu cầu ≥1, THEN 422 `EMPTY_PLAN`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given Member chưa có PT, When Admin phân công, Then TrainerAssignment Active + AuditLog.
- [ ] **AC-02:** Given Member đã có PT active, When phân công PT mới, Then assignment cũ Ended, mới Active (vẫn tối đa 1).
- [ ] **AC-07:** Given Member chỉ có gói thường (SupportsPT=false) hoặc gói PT đã hết hạn, When Admin phân công PT, Then 422 `NO_PT_PACKAGE`. Given Member có gói PT đang hiệu lực, When phân công, Then 201.
- [ ] **AC-03:** Given PT đăng nhập, When xem danh sách member, Then chỉ thấy member được phân công.
- [ ] **AC-04:** Given PT, When tạo plan + ≥1 bài tập cho member của mình, Then lưu thành công.
- [ ] **AC-05:** Given PT, When tạo plan cho member người khác, Then 403.
- [ ] **AC-06:** Given Member, When xem giáo án của mình, Then thấy plan + bài tập.

## 9. Out of Scope
- Lịch buổi tập theo slot/booking, video hướng dẫn bài tập, thư viện bài tập dùng chung, chat PT-Member.
