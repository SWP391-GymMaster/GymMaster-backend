# Feature Specification: Meal Journal & Daily Calorie Summary

**Feature Branch**: `007-nutrition-calorie`
**Created**: 2026-05-30
**Status**: Approved
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 06_FEATURE_SPECS (F4), 03_SRS (UC-16..21), 04 (FR-MEAL/CAL), ADR-04

> EARS legend như spec 001.

---

## 1. Context & Goal
Member ghi nhật ký bữa ăn từ food database (nhập tay — ADR-04), hệ thống tính calo/macro theo ngày so với mục tiêu. Mục tiêu: theo dõi dinh dưỡng chính xác, hỗ trợ PT tư vấn.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Member | Đặt mục tiêu calo; ghi bữa ăn; xem tổng kết ngày/lịch sử |
| PT | Xem lịch sử calo của member được phân công |
| System | Tính tổng calo, so mục tiêu, cập nhật summary |

## 3. Functional Requirements (EARS)
- **FR-CAL-TGT-01 (Event):** WHEN Member/PT đặt mục tiêu calo (> 0), THE system SHALL lưu CalorieTarget hiệu lực từ ngày chỉ định.
- **FR-FOOD-01 (Event):** WHEN Member tìm món, THE system SHALL trả danh sách FoodItem khớp tên (không phân biệt hoa thường).
- **FR-FOOD-02 (Optional):** WHERE món không có trong database, THE system SHALL cho Member thêm Custom Food (tên + calo/đơn vị).
- **FR-MEAL-01 (Event):** WHEN Member thêm món vào bữa ăn với khẩu phần > 0, THE system SHALL tạo MealLogItem và tính lại Daily Calorie Summary.
- **FR-MEAL-02 (Unwanted):** IF khẩu phần ≤ 0, THEN THE system SHALL từ chối với 422.
- **FR-MEAL-03 (Event):** WHEN cùng món được thêm lại trong cùng bữa, THE system SHALL cộng dồn khẩu phần.
- **FR-CAL-01 (Ubiquitous):** THE system SHALL tính Daily Summary = Σ(calo/đơn vị × khẩu phần) của mọi món trong ngày, và remaining = target − consumed.
- **FR-CAL-02 (Optional):** WHERE người gọi là PT, THE system SHALL chỉ cho xem lịch sử calo của member được phân công.

## 4. Non-functional Requirements
- **NFR-01:** Cập nhật summary < 500ms sau khi thêm món.
- **NFR-02:** Calo lưu tại thời điểm ghi (snapshot) để không đổi khi FoodItem bị sửa sau.
- **NFR-03:** Food search có phân trang.

## 5. Data Model
- **FoodItems**(Id, Name, Unit, CaloriesPerUnit DECIMAL(8,2), ProteinG?, CarbG?, FatG?, IsCustom BIT, CreatedByUserId nullable, IsDeleted, CreatedAt)
- **MealLogs**(Id, MemberId→MemberProfiles, LogDate DATE, MealType{Breakfast,Lunch,Dinner,Snack}, CreatedAt)
- **MealLogItems**(Id, MealLogId→MealLogs, FoodItemId→FoodItems, Quantity DECIMAL(8,2)>0, Calories DECIMAL(8,2) snapshot)
- **CalorieTargets**(Id, MemberId, TargetCalories, EffectiveFrom DATE, CreatedAt)
- Xem `15_DATABASE_SCHEMA.md` §2.7.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/members/{id}/calorie-target | Member(self), PT(assigned) | {targetCalories, effectiveFrom} | 201 | 403, 422 |
| GET | /api/food-items?query=&page= | Member, PT, Admin | — | 200 (paged) | 401 |
| POST | /api/food-items | Member | {name, unit, caloriesPerUnit} | 201 (custom) | 400 |
| POST | /api/meal-logs | Member(self) | {logDate, mealType, items:[{foodItemId, quantity}]} | 201 | 404, 422 |
| GET | /api/members/{id}/calorie-summary?date= | Member(self), PT(assigned) | — | 200 {consumed, target, remaining} | 403 |
| GET | /api/members/{id}/calorie-history?from=&to= | Member(self), PT(assigned) | — | 200 (daily list) | 403 |

## 7. Error Handling (EARS Unwanted)
- IF khẩu phần ≤ 0, THEN 422 `INVALID_QUANTITY`.
- IF FoodItem không tồn tại, THEN 404 `FOOD_NOT_FOUND` (gợi ý Add Custom Food).
- IF target ≤ 0, THEN 422 `INVALID_TARGET`.
- IF PT xem calo member không thuộc mình, THEN 403 `FORBIDDEN`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given món có trong DB, When Member thêm khẩu phần 2 đơn vị, Then summary tăng đúng = 2×calo/đơn vị.
- [ ] **AC-02:** Given khẩu phần 0, When thêm món, Then 422.
- [ ] **AC-03:** Given món không có, When Member add custom food, Then lưu và dùng được ngay.
- [ ] **AC-04:** Given target 2000 + đã ăn 1500, When xem summary, Then remaining=500.
- [ ] **AC-05:** Given món được thêm 2 lần cùng bữa, When tính, Then khẩu phần cộng dồn.
- [ ] **AC-06:** Given FoodItem bị sửa calo sau đó, When xem log cũ, Then calo giữ nguyên (snapshot).

## 9. Out of Scope
- Tự định lượng calo từ ảnh (chỉ gợi ý tên — spec 009 enhancement), gợi ý thực đơn AI, quét barcode dinh dưỡng (secondary), đồng bộ app dinh dưỡng bên thứ ba.

## 10. Cần bàn rõ với team — open items (2026-06-12)
> Các đề xuất nâng cấp CHƯA chốt, cần thống nhất với team trước khi làm. Code 007 hiện vẫn đúng spec §6 ở trên; các mục này là hướng mở rộng.

- **Gộp 1 API tổng quan theo ngày (daily-overview)** thay vì để FE ghép nhiều call:
  `GET /api/v1/members/{memberId}/nutrition/daily-overview?date=` → `{ date, consumed, target, remaining, protein, carb, fat, meals: [ { mealType, items: [ { foodName, quantity, calories, protein, carb, fat } ] } ] }`.
  **Cần chốt**: làm gộp, hay giữ `calorie-summary` + `meal-logs` tách như §6. (Làm được ngay phía BE, không đụng DB.)
- **Snapshot macro cho lịch sử (NFR-02)**: `meal_log_items` hiện CHỈ snapshot `Calories` → tổng calo lịch sử đúng, nhưng **macro (đạm/tinh bột/béo) đang lấy từ `food_items` sống** nên sẽ lệch nếu admin sửa món. Để đúng NFR-02 **cần DB team thêm cột** `FoodNameSnapshot/ProteinGSnapshot/CarbGSnapshot/FatGSnapshot` (giữ `FoodItemId` để biết món gốc). Backend sẽ ưu tiên đọc snapshot khi có cột.
- **Online food search / barcode (secondary/Future)**: proxy Open Food Facts — `GET /api/v1/food-items/online-search?query=`, `GET /api/v1/food-items/barcode/{barcode}`; chọn món online → lưu vào `food_items` rồi dùng như món local. §9 đã liệt barcode là out-of-scope MVP → làm sau khi team đồng ý.
