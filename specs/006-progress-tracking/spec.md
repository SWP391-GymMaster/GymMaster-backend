# Feature Specification: Progress Tracking & Member 360° Profile

**Feature Branch**: `006-progress-tracking`
**Created**: 2026-05-30
**Status**: Approved
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 03_SRS (UC-14, UC-15), 02 (MVP-10, MVP-11)

> EARS legend như spec 001.

---

## 1. Context & Goal
Member/PT ghi tiến độ luyện tập (cân nặng, số đo, ảnh) theo thời gian; tổng hợp "Member 360°" gom hồ sơ + membership + check-in + giáo án + tiến độ + dinh dưỡng trong một màn hình. Mục tiêu: theo dõi tiến bộ và ra quyết định huấn luyện.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Member | Ghi/xem tiến độ của mình; xem 360° của mình |
| PT | Ghi/xem tiến độ member được phân công; xem 360° |
| Admin | Xem 360° mọi member |
| System | Tổng hợp dữ liệu nhiều nguồn, tính biến thiên |

## 3. Functional Requirements (EARS)
- **FR-PROG-01 (Event):** WHEN Member/PT ghi tiến độ với các chỉ số hợp lệ (> 0), THE system SHALL lưu ProgressLog kèm ngày đo.
- **FR-PROG-02 (Optional):** WHERE người ghi là PT, THE system SHALL chỉ cho ghi cho member được phân công.
- **FR-PROG-03 (Event):** WHEN xem lịch sử tiến độ, THE system SHALL trả danh sách theo thứ tự thời gian để vẽ biểu đồ.
- **FR-PROG-04 (Optional):** WHERE người ghi đính kèm ảnh, THE system SHALL lưu ảnh vào Azure Blob và lưu URL tham chiếu, KHÔNG lưu nhị phân trong DB.
- **FR-360-01 (Event):** WHEN truy cập Member 360°, THE system SHALL tổng hợp: hồ sơ, membership hiện tại + lịch sử, check-in gần đây, PT/giáo án, tiến độ, tóm tắt dinh dưỡng.
- **FR-360-02 (Unwanted):** IF người gọi không có quyền trên member (không phải self/assigned PT/Admin), THEN THE system SHALL trả 403.

## 4. Non-functional Requirements
- **NFR-01:** Tổng hợp 360° < 1.5s.
- **NFR-02:** Ảnh tiến độ ≤ 5MB, định dạng jpg/png; URL ký có thời hạn.
- **NFR-03:** Số đo dùng đơn vị nhất quán (kg, cm).

## 5. Data Model
- **ProgressLogs**(Id, MemberId→MemberProfiles, RecordedBy→Users, MeasuredAt DATE, WeightKg DECIMAL(5,2), BodyFatPct DECIMAL(4,1) nullable, Measurements NVARCHAR(MAX) JSON nullable, PhotoUrl NVARCHAR(500) nullable, Note, CreatedAt)
- 360° là read-model tổng hợp từ: MemberProfiles, Memberships, CheckIns, TrainerAssignments, WorkoutPlans, ProgressLogs, MealLogs/CalorieTargets.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/members/{id}/progress | Member(self), PT(assigned) | {measuredAt, weightKg, bodyFatPct?, photo?} | 201 | 403, 404, 422 |
| GET | /api/v1/members/{id}/progress | Member(self), PT(assigned), Admin | — | 200 (timeline) | 403, 404 |
| GET | /api/v1/members/{id}/profile-360 | Member(self), PT(assigned), Admin/Staff | — | 200 (aggregate) | 403, 404 |
| GET | /api/v1/members/me/profile-360 | Member(self) | — | 200 (aggregate) | 403, 404 |
| GET | /api/v1/members/{id}/360 | Admin/Staff/PT(assigned)/Member(self) | — | 200 (aggregate) | 403, 404 |

> **Một implementation duy nhất:** cả 3 route trên gọi chung `ProgressService.GetProfile360Async`
> (không còn bản trùng trong `MemberService`). `{id}/profile-360` là canonical; 2 route kia là tiện ích.

### 6.1. Response 360 — Contract cho FE (`Member360Data`)

JSON 200 (camelCase). FE code theo đúng shape này:

```jsonc
{
  "member":            { "id": 12, "memberCode": "MEM-000012", "fullName": "...", "email": "...",
                         "phone": "...", "status": "Active", "dateOfBirth": "1998-05-01", "gender": "Male" },
  "currentMembership": { "id": 8, "packageName": "Gói 3 tháng", "startDate": "2026-01-01",
                         "endDate": "2026-04-01", "status": "Active", "paymentStatus": "Paid" },   // | null
  "membershipHistory": [ /* phần tử giống currentMembership, sắp mới→cũ */ ],
  "recentCheckIns":    [ { "id": 1, "checkInAt": "2026-06-20T08:00:00Z" } ],                       // tối đa 5
  "progressTimeline":  [ { "id", "memberId", "measuredAt", "weightKg", "bodyFatPercent",
                           "chestCm", "waistCm", "hipCm", "note", "createdAt" } ],
  "nutritionSummary":  { "date": "2026-06-21", "consumed": 1800, "target": 2000, "remaining": 200 }, // | null
  "assignedPT":        { "id": 3, "fullName": "...", "specialty": "...", "assignedAt": "2026-02-01T..." } // | null
}
```

| Field | Kiểu / Nguồn | Ghi chú |
|---|---|---|
| `member.memberCode` | string, 006 | format `MEM-{id:D6}` |
| `member.status` | enum, 002 | **PascalCase** (`Active`/`Pending`/`Expired`/`Locked`) |
| `currentMembership.status` | enum, 003 | **PascalCase** (`Active`/`PendingPayment`/`Expired`/`Cancelled`) |
| `currentMembership.paymentStatus` | enum, 003 | **PascalCase** (`Paid`/`Pending`) — **suy từ bảng Payments (THẬT)**, không phải đoán |
| `membershipHistory[]` | array, 003 | toàn bộ lịch sử gói |
| `recentCheckIns[]` | array, 004 | tối đa 5, mới nhất trước |
| `progressTimeline[]` | array, 006 | tăng dần theo `measuredAt` |
| `nutritionSummary` | object, 007 | **đã tính sẵn** (consumed/target/remaining) — FE chỉ hiển thị, không tự cộng |
| `assignedPT` | object, 005 | PT đang active (bảng `trainer_assignments`); `assignedAt` = ngày phân công |

⚠️ **Casing cho FE:** các enum `status`/`paymentStatus` trả **PascalCase** để đồng bộ với spec 003 (`/memberships`) và spec 002 trên toàn hệ thống. FE map về casing hiển thị của mình (vd `"Active"` → `"active"`). Backend **không** đổi casing riêng cho 360 để tránh lệch giữa các endpoint.

## 7. Error Handling (EARS Unwanted)
- IF chỉ số ≤ 0 hoặc ngày đo trong tương lai, THEN 422 `INVALID_MEASUREMENT`.
- IF ảnh vượt kích thước/định dạng sai, THEN 422 `INVALID_FILE`.
- IF không có quyền trên member, THEN 403 `FORBIDDEN`.
- IF member không tồn tại, THEN 404 `NOT_FOUND`.
- IF upload Blob thất bại, THEN 502 `STORAGE_ERROR` (không lưu ProgressLog mồ côi).

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given Member, When ghi cân nặng hợp lệ, Then lưu ProgressLog + hiện trên timeline.
- [ ] **AC-02:** Given PT, When ghi tiến độ cho member không thuộc mình, Then 403.
- [ ] **AC-03:** Given có ảnh, When ghi tiến độ, Then ảnh lưu Blob và DB chỉ giữ URL.
- [ ] **AC-04:** Given Member có dữ liệu nhiều nguồn, When mở 360°, Then thấy membership + check-in + PT + tiến độ + dinh dưỡng.
- [ ] **AC-05:** Given chỉ số âm, When ghi, Then 422.

## 9. Out of Scope
- Phân tích AI dự đoán tiến bộ, so sánh giữa nhiều member, mục tiêu SMART tự động, đồng bộ thiết bị đeo (wearable).
