# Feature Specification: Meal Journal & Daily Calorie Summary

**Feature Branch**: `007-nutrition-calorie`
**Created**: 2026-05-30
**Status**: Implemented (spec đồng bộ theo code 2026-07-15)
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 06_FEATURE_SPECS (F4), 03_SRS (UC-16..21), 04 (FR-MEAL/CAL), ADR-04

> EARS legend như spec 001. Mọi path dưới `/api/v1`. Ngày tính theo giờ VN (`AppClock`).

---

## 1. Context & Goal
Member ghi nhật ký bữa ăn từ food database (nhập tay — ADR-04), hệ thống tính calo/macro theo ngày so với mục tiêu. Member **chưa có gói tập active** chỉ được tập trên **20 món cố định** (tier miễn phí) để dùng thử; có gói active (hoặc Admin/Staff/PT) → toàn bộ kho món. Mục tiêu: theo dõi dinh dưỡng chính xác, hỗ trợ PT tư vấn.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Member | Đặt mục tiêu calo; ghi bữa ăn; xem tổng kết ngày/lịch sử; thêm món |
| PT | Xem/đặt mục tiêu + xem lịch sử calo của member được phân công |
| Admin/Staff | Xem toàn bộ; thêm món |
| System | Tính tổng calo/macro, so mục tiêu |

## 3. Functional Requirements (EARS)
- **FR-CAL-TGT-01 (Event):** WHEN Member/PT/Admin/Staff đặt mục tiêu calo (DailyCalories > 0, macro ≥ 0), THE system SHALL lưu (hoặc cập nhật) CalorieTarget theo (member, EffectiveDate). UNIQUE(MemberId, EffectiveDate).
- **FR-CAL-TGT-02 (Event):** WHEN xem mục tiêu (`GET /calorie-target`), THE system SHALL trả mục tiêu hiệu lực gần nhất ≤ hôm nay (404 `NO_TARGET` nếu chưa đặt).
- **FR-FOOD-01 (Event):** WHEN tìm món (`GET /food-items`), THE system SHALL trả FoodItem active khớp tên (không phân biệt DẤU + hoa/thường, collation `Latin1_General_100_CI_AI`), phân trang. WHERE người gọi là Member **chưa có gói active**, THE system SHALL giới hạn universe = **20 món đầu (A→Z)** trước khi lọc từ khoá.
- **FR-FOOD-02 (Event):** WHEN Member/Admin/Staff thêm món (`POST /food-items`), THE system SHALL find-or-create theo tên: trùng tên → trả món có sẵn (200), ngược lại tạo mới (201).
- **FR-MEAL-01 (Event):** WHEN Member thêm món vào bữa (khẩu phần > 0), THE system SHALL tạo/ghép MealLog theo (member, ngày, bữa) và tạo MealLogItem, **snapshot Calories = CaloriesPerUnit × Quantity**.
- **FR-MEAL-02 (Unwanted):** IF khẩu phần ≤ 0 hoặc mealType không hợp lệ, THEN 422.
- **FR-MEAL-03 (Event):** WHEN cùng món được thêm lại trong cùng bữa/ngày, THE system SHALL cộng dồn khẩu phần + calo.
- **FR-CAL-01 (Ubiquitous):** THE system SHALL tính Daily Summary = Σ calo trong ngày; remaining = target − consumed (null nếu chưa đặt target). Summary còn trả **macro** (protein/carb/fat consumed/target/remaining).
- **FR-CAL-02 (Optional):** WHERE người gọi là PT, THE system SHALL chỉ cho xem/đặt của member được phân công active.

## 4. Non-functional Requirements
- **NFR-01:** Cập nhật summary < 500ms.
- **NFR-02:** **Calo** snapshot tại thời điểm ghi (không đổi khi FoodItem sửa sau). Macro theo ngày lấy từ `food_items` hiện tại (live) — xem §10.
- **NFR-03:** Food search có phân trang (mặc định 20, tối đa 100).

## 5. Data Model
- **food_items**(Id, Name[UNIQUE], Unit, CaloriesPerUnit DECIMAL(8,2), ProteinG?, CarbG?, FatG?, IsActive, **ServingSize** DECIMAL(8,2) mặc định 100, **Source** {Admin/AI} mặc định Admin, CreatedAt)
- **meal_logs**(Id, MemberId→member_profiles, LogDate DATE, MealType TINYINT{1 Breakfast,2 Lunch,3 Dinner,4 Snack}, CreatedAt) — index (MemberId, LogDate)
- **meal_log_items**(Id, MealLogId→meal_logs, FoodItemId→food_items, Quantity DECIMAL(8,2)>0, Calories DECIMAL(8,2) snapshot)
- **calorie_targets**(Id, MemberId, EffectiveDate DATE, DailyCalories DECIMAL(8,2), ProteinG?, CarbG?, FatG?, CreatedAt) — UNIQUE(MemberId, EffectiveDate)

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/members/{id}/calorie-target | Member(self), PT(assigned), Admin/Staff | {dailyCalories, effectiveDate?, proteinG?, carbG?, fatG?} | 200/201 | 403, 404, 422 |
| GET | /api/v1/members/{id}/calorie-target | Member(self), PT(assigned), Admin/Staff | — | 200 CalorieTargetResponse | 403, 404 `NO_TARGET` |
| GET | /api/v1/members/{id}/calorie-summary?date= | Member(self), PT(assigned), Admin/Staff | — | 200 CalorieSummaryResponse | 403, 404 |
| GET | /api/v1/members/{id}/calorie-history?from=&to= | Member(self), PT(assigned), Admin/Staff | — | 200 (mảng, mặc định 7 ngày) | 403, 404, 422 |
| GET | /api/v1/food-items?query=&page=&pageSize= | tất cả (Member giới hạn tier) | — | 200 (paged FoodItemResponse) | 401 |
| POST | /api/v1/food-items | Member, Admin, Staff | {name, unit, caloriesPerUnit, proteinG?, carbG?, fatG?} | 200/201 | 400 |
| POST | /api/v1/meal-logs | Member(self), Admin, Staff | {memberId, logDate, mealType, items:[{foodItemId, quantity}]} | 201 MealLogResponse | 403, 404, 422 |
| GET | /api/v1/meal-logs?memberId=&date= | Member(self), Admin, Staff, PT(assigned) | — | 200 (mảng MealLogResponse) | 403 |

### 6.1. Response contract (camelCase)
- **FoodItemResponse:** `{ id, name, unit, caloriesPerUnit, proteinG, carbG, fatG, isActive }`.
- **CalorieTargetResponse:** `{ id, memberId, effectiveDate, dailyCalories, proteinG, carbG, fatG }`.
- **MealLogResponse:** `{ id, memberId, logDate, mealType, totalCalories, items:[{ id, foodItemId, foodName, quantity, calories, proteinG, carbG, fatG }] }` — macro/món = giá trị/đơn vị × khẩu phần.
- **CalorieSummaryResponse:** `{ date, consumed, target, remaining, consumedProteinG, consumedCarbG, consumedFatG, targetProteinG, targetCarbG, targetFatG, remainingProteinG, remainingCarbG, remainingFatG }`.

## 7. Error Handling (EARS Unwanted)
- IF khẩu phần ≤ 0, THEN 422 `INVALID_QUANTITY`.
- IF FoodItem không tồn tại/không active, THEN 404 `FOOD_NOT_FOUND`.
- IF target ≤ 0 hoặc macro < 0, THEN 422 `INVALID_TARGET`.
- IF PT/Member xem của member không thuộc mình, THEN 403 `FORBIDDEN`.
- IF khoảng ngày history không hợp lệ (from > to), THEN 422 `VALIDATION_ERROR`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given món trong DB, When thêm 2 đơn vị, Then summary tăng đúng 2×calo/đơn vị.
- [ ] **AC-02:** Given khẩu phần 0, When thêm món, Then 422.
- [ ] **AC-03:** Given món chưa có, When thêm món mới, Then lưu và dùng ngay (201); trùng tên → trả món cũ (200).
- [ ] **AC-04:** Given target 2000 + đã ăn 1500, When xem summary, Then remaining=500.
- [ ] **AC-05:** Given món thêm 2 lần cùng bữa, When tính, Then khẩu phần cộng dồn.
- [ ] **AC-06:** Given FoodItem bị sửa calo sau đó, When xem log cũ, Then calo giữ nguyên (snapshot).
- [ ] **AC-07:** Given Member chưa có gói active, When tìm món, Then chỉ thấy/tìm được trong 20 món cố định.

## 9. Out of Scope
- Tự định lượng calo từ ảnh (gợi ý tên + ước lượng — spec 009), đồng bộ app dinh dưỡng bên thứ ba.

## 10. Ghi chú triển khai
- Mỗi món trong `GET /meal-logs` trả kèm `proteinG/carbG/fatG` để FE tự cộng tổng macro (không có endpoint gộp).
- **Snapshot macro (NFR-02, còn mở):** `meal_log_items` chỉ snapshot `Calories`; macro lịch sử lấy từ `food_items` sống → cần DB team thêm cột snapshot macro nếu muốn tuyệt đối đúng khi admin sửa món.
