# Feature Specification: Progress Tracking & Member 360° Profile

**Feature Branch**: `006-progress-tracking`
**Created**: 2026-05-30
**Status**: Implemented (spec đồng bộ theo code 2026-07-15)
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: srs-use-cases (UC-14, UC-15), product-scope (MVP-10, MVP-11)

> EARS legend như spec 001. Mọi path dưới `/api/v1`.

---

## 1. Context & Goal
Member/PT ghi tiến độ luyện tập (cân nặng, tỉ lệ mỡ, số đo ngực/eo/hông) theo thời gian; tổng hợp "Member 360°" gom hồ sơ + membership (hiện tại + lịch sử) + check-in + PT + tiến độ + dinh dưỡng trong một màn hình. Mục tiêu: theo dõi tiến bộ và ra quyết định huấn luyện. **Chưa hỗ trợ ảnh tiến độ** (xem Out of Scope).

## 2. Actors
| Actor | Vai trò |
|---|---|
| Member | Ghi/xem tiến độ của mình; xem 360° của mình |
| PT | Ghi/xem tiến độ member được phân công active; xem 360° |
| Admin/Staff | Xem 360° mọi member |
| System | Tổng hợp dữ liệu nhiều nguồn |

## 3. Functional Requirements (EARS)
- **FR-PROG-01 (Event):** WHEN Member/PT ghi tiến độ với ≥1 chỉ số trong khoảng hợp lệ (Weight 20–300kg, BodyFat 0–70%, Chest/Waist/Hip 30–200cm) và thời điểm đo không ở tương lai (so theo giờ VN), THE system SHALL lưu ProgressLog. **1 ngày = 1 bản ghi**: ghi lại cùng ngày sẽ **đè** bản cũ (update) thay vì tạo điểm mới.
- **FR-PROG-02 (Optional):** WHERE người ghi là PT, THE system SHALL chỉ cho ghi cho member được phân công active.
- **FR-PROG-03 (Event):** WHEN xem lịch sử tiến độ, THE system SHALL trả danh sách tăng dần theo `measuredAt` để vẽ biểu đồ.
- **FR-360-01 (Event):** WHEN truy cập Member 360°, THE system SHALL tổng hợp: hồ sơ, membership hiện tại + toàn bộ lịch sử, check-in gần đây (tối đa 5), tiến độ (timeline), tóm tắt dinh dưỡng hôm nay, PT đang phân công active.
- **FR-360-02 (Unwanted):** IF người gọi không có quyền trên member (không phải self / assigned PT / Admin / Staff), THEN 403.
- **FR-360-03 (Ubiquitous):** THE system SHALL suy `currentMembership` = gói `Active` còn hạn (EndDate lớn nhất) → nếu không có thì đơn `PendingPayment` mới nhất → nếu không có thì **null** (KHÔNG bọc đơn Cancelled/Expired làm "gói hiện tại").

## 4. Non-functional Requirements
- **NFR-01:** Tổng hợp 360° < 1.5s (đồng bộ trạng thái membership hết hạn trước khi trả).
- **NFR-02:** `checkInAt`/`assignedAt` ép `Kind=Utc` để FE hiển thị đúng giờ địa phương.
- **NFR-03:** Số đo dùng đơn vị nhất quán (kg, cm); ghi chú ≤ 500 ký tự.

## 5. Data Model
- **progress_logs**(Id, MemberId→member_profiles, MeasuredAt DATETIME2, WeightKg DECIMAL(5,2)?, BodyFatPercent DECIMAL(5,2)?, ChestCm?, WaistCm?, HipCm?, Note NVARCHAR(500)?, CreatedByUserId?, CreatedAt) — index (MemberId, MeasuredAt).
- 360° là read-model tổng hợp từ: member_profiles, memberships (+payments để suy paymentStatus), check_ins, trainer_assignments, progress_logs, meal_logs/calorie_targets.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/members/{id}/progress | Member(self), PT(assigned), Admin/Staff | {measuredAt?, weightKg?, bodyFatPct?, chestCm?, waistCm?, hipCm?, note?} | 201 (mới) / 200 (đè cùng ngày) | 403, 404, 422 |
| GET | /api/v1/members/{id}/progress | Member(self), PT(assigned), Admin/Staff | — | 200 (timeline) | 403, 404 |
| GET | /api/v1/members/{id}/profile-360 | Member(self), PT(assigned), Admin/Staff | — | 200 (aggregate) | 403, 404 |
| GET | /api/v1/members/{id}/360 | Admin/Staff/PT(assigned)/Member(self) | — | 200 (alias) | 403, 404 |
| GET | /api/v1/members/me/profile-360 | Member(self) | — | 200 | 401, 404 |

> **Một implementation duy nhất:** cả 3 route 360 gọi chung `ProgressService.GetProfile360Async`. `{id}/profile-360` là canonical.

### 6.1. Response 360 — Contract cho FE (`Profile360Response`, camelCase)
```jsonc
{
  "member":            { "id", "memberCode": "MEM-000012", "fullName", "email", "avatarUrl",
                         "phone", "status", "dateOfBirth", "gender" },
  "currentMembership": { "id", "packageId", "packageName", "supportsPT", "startDate", "endDate",
                         "status", "paymentStatus" },                       // | null
  "membershipHistory": [ /* Membership360, mới→cũ */ ],
  "recentCheckIns":    [ { "id", "checkInAt": "…Z" } ],                     // tối đa 5
  "progressTimeline":  [ { "id","memberId","measuredAt","weightKg","bodyFatPercent",
                           "chestCm","waistCm","hipCm","note","createdAt" } ],
  "nutritionSummary":  { "date","consumed","target","remaining", … macros },// | null (spec 007)
  "assignedPT":        { "id","fullName","specialty","assignedAt": "…Z" }    // | null
}
```
| Field | Nguồn | Ghi chú |
|---|---|---|
| `member.memberCode` | 002 | `MEM-{id:D6}` |
| `currentMembership.status/paymentStatus` | 003 | **PascalCase**; paymentStatus suy từ bảng payments (Paid/Pending) |
| `nutritionSummary` | 007 | tính sẵn (consumed/target/remaining + macros); FE chỉ hiển thị |
| `assignedPT` | 005 | PT đang active; `id` = TrainerId, `assignedAt` = ngày phân công |

## 7. Error Handling (EARS Unwanted)
- IF chỉ số ngoài khoảng hợp lệ hoặc ngày đo ở tương lai hoặc note quá dài, THEN 422 `INVALID_MEASUREMENT`.
- IF không có chỉ số nào, THEN 422 `INVALID_MEASUREMENT`.
- IF không có quyền trên member, THEN 403 `FORBIDDEN`.
- IF member không tồn tại, THEN 404 `NOT_FOUND`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given Member, When ghi cân nặng hợp lệ, Then lưu ProgressLog + hiện trên timeline (201).
- [ ] **AC-02:** Given PT, When ghi tiến độ cho member không thuộc mình, Then 403.
- [ ] **AC-03:** Given đã có bản ghi hôm nay, When ghi lại cùng ngày, Then đè bản cũ (200), không tạo điểm mới.
- [ ] **AC-04:** Given Member có dữ liệu nhiều nguồn, When mở 360°, Then thấy membership + check-in + PT + tiến độ + dinh dưỡng.
- [ ] **AC-05:** Given chỉ số ngoài khoảng (vd cân nặng 5kg) hoặc ngày tương lai, When ghi, Then 422.
- [ ] **AC-06:** Given member chỉ còn đơn Cancelled/Expired, When mở 360°, Then `currentMembership=null`.

## 9. Out of Scope
- **Ảnh tiến độ** (upload/Blob) — chưa triển khai ở bản này.
- Phân tích AI dự đoán tiến bộ, so sánh nhiều member, mục tiêu SMART tự động, đồng bộ wearable.
