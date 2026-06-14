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

**Đã bổ sung (2026-06-15):** mỗi món trong `GET /api/v1/meal-logs` nay trả kèm `proteinG/carbG/fatG` (= giá trị/đơn vị × khẩu phần, giống cách tính calo) để **FE tự cộng tổng macro** — KHÔNG làm endpoint gộp `daily-overview`. Vẫn là endpoint cũ, không thêm endpoint, không đụng DB. Lưu ý: macro tính từ `food_items` hiện tại (live), calo thì đã snapshot — đồng nhất với mục "snapshot macro" còn mở dưới đây. Có test ở `tests/GymMaster.Api.Tests/NutritionServiceTests.cs`.

- **Gộp 1 API tổng quan theo ngày (daily-overview)** thay vì để FE ghép nhiều call:
  `GET /api/v1/members/{memberId}/nutrition/daily-overview?date=` → `{ date, consumed, target, remaining, protein, carb, fat, meals: [ { mealType, items: [ { foodName, quantity, calories, protein, carb, fat } ] } ] }`.
  **Cần chốt**: làm gộp, hay giữ `calorie-summary` + `meal-logs` tách như §6. (Làm được ngay phía BE, không đụng DB.)
- **Snapshot macro cho lịch sử (NFR-02)**: `meal_log_items` hiện CHỈ snapshot `Calories` → tổng calo lịch sử đúng, nhưng **macro (đạm/tinh bột/béo) đang lấy từ `food_items` sống** nên sẽ lệch nếu admin sửa món. Để đúng NFR-02 **cần DB team thêm cột** `FoodNameSnapshot/ProteinGSnapshot/CarbGSnapshot/FatGSnapshot` (giữ `FoodItemId` để biết món gốc). Backend sẽ ưu tiên đọc snapshot khi có cột.
### Online food search — bản tối thiểu (ĐÃ CODE 2026-06-15)
> Proxy server-side đúng tầm SWP391, **KHÔNG đụng cấu trúc DB**. Tính năng phụ.

**Đã triển khai (BE-only):** `GET /api/v1/food-items/online-search?query=` qua `FoodOnlineSearchService` (HttpClient + `IMemoryCache` TTL 6h + timeout 5s + lỗi-thì-rỗng) → đăng ký ở `Program.cs`. `POST /api/v1/food-items` đã thành **Find-or-Create** (trùng tên trả món cũ 200, KHÔNG còn 409). Test: bóc tách JSON OFF + làm sạch + Find-or-Create ở `tests/GymMaster.Api.Tests`. **Lưu ý:** cú gọi OFF thật chỉ verify khi chạy máy có internet (không unit-test offline). `barcode` + cooldown + `meta.found_existing` chưa làm (để sau). Chi tiết thiết kế bên dưới.

- **Endpoint**: `GET /api/v1/food-items/online-search?query=` (FE đã có client sẵn). `GET /api/v1/food-items/barcode/{barcode}` = **tùy chọn** (§9 xếp barcode secondary).
- **Luồng**: tìm kho nội bộ trước → thiếu thì hội viên **bấm "Tìm online"** → BE gọi Open Food Facts (timeout 5s) → **lỗi/quá giờ trả `[]`** để FE tự lùi về kết quả local.
- **Limit (tránh vượt 10 req/phút của OFF)**: (1) chỉ gọi khi **bấm tay** (không search-as-you-type); (2) **nhớ tạm** kết quả online theo từ khóa bằng `IMemoryCache` (TTL ~vài giờ) — **chỉ cache dữ liệu online công khai, KHÔNG cache dữ liệu nội bộ/member**; (3) tùy chọn cooldown vài giây/người.
- **Lưu món = Find-or-Create**: sửa `POST /api/v1/food-items` → trùng tên thì **trả món cũ (200) + `meta.found_existing`** thay vì 409.
- **Chống món rác (DB-free)**: validate (tên ≤150, calo/macro trong khoảng 0..max) + Find-or-Create chống trùng + admin ẩn bằng `IsActive` có sẵn.
- **Attribution**: ghi nguồn "Dữ liệu từ Open Food Facts" (ODbL yêu cầu).
- **Lưu ý triển khai (đã kiểm code 2026-06-12)**:
  - `food_items` đã đủ cột (Name **max 150**, Unit 30, macro, IsActive) → **không thêm cột**. Cắt tên OFF về **≤150** (không phải 100 như tài liệu FE đoán).
  - Có **unique index trên `Name`** → hiện trùng tên trả **409 sạch (không crash 500)**; Find-or-Create chỉ đổi 409→trả-món-cũ, nên bắt thêm `DbUpdateException` cho trường hợp đua hiếm.
  - Cần thêm `builder.Services.AddMemoryCache()` + `AddHttpClient()` trong `Program.cs` (không đụng DB).
  - `AddAsync` đã validate số âm; thêm chặn **trên** (vd calo ≤ 10000) nếu muốn.
- **❌ KHÔNG làm**: Redis, USDA/đa nhà cung cấp, circuit-breaker, member-private (cần cột `CreatedByUserId` — chưa có), cron crawl, Elasticsearch.
- **Công sức**: ~0.5–1 ngày, BE-only, gọn trong Part Y.
