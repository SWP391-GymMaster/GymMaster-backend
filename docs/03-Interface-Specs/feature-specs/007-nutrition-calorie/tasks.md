---
description: "Task list — Meal Journal & Daily Calorie Summary"
---

# Tasks: Meal Journal & Daily Calorie Summary

**Feature**: `007-nutrition-calorie`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 29/31 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code, `[ ]` = còn nợ.

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 Tạo slice `backend/GymMaster.API/Features/Nutrition/`

## Phase 2: Foundational

- [X] T002 `Entities/FoodItem.cs` — `Name` UNIQUE, `CaloriesPerUnit DECIMAL(8,2)`, `ProteinG/CarbG/FatG`, `ServingSize` (mặc định 100), `Source {Admin, AI}`
- [X] T003 [P] `Entities/MealLog.cs` + `MealLogItem.cs` — `Quantity DECIMAL(8,2) > 0`, `Calories DECIMAL(8,2)` **snapshot**
- [X] T004 [P] `Entities/CalorieTarget.cs` — `UNIQUE(MemberId, EffectiveDate)`
- [X] T005 [P] `Entities/NutritionEnums.cs` — `MealType {1 Breakfast, 2 Lunch, 3 Dinner, 4 Snack}`
- [X] T006 Index `(MemberId, LogDate)` trên `meal_logs` trong `Data/GymMasterDbContext.cs`
- [X] T007 Đặt collation `Latin1_General_100_CI_AI` cho `food_items.Name` → tìm không phân biệt **dấu + hoa thường**
- [X] T008 Đăng ký DI `IFoodItemService`, `INutritionService` trong `Program.cs`

---

## Phase 3: US1 — Kho món ăn (P1) 🎯 MVP

**Goal**: người dùng tra được món và thêm món còn thiếu.
**Independent Test**: `GET /food-items?query=pho bo` ra "Phở bò"; `POST /food-items` tên đã có → 200 trả món cũ, tên mới → 201.

- [X] T009 [US1] `NutritionDtos.cs` — `FoodItemResponse { id, name, unit, caloriesPerUnit, proteinG, carbG, fatG, isActive }`
- [X] T010 [US1] `FoodItemService.SearchAsync` — lọc theo tên dùng collation CI_AI, `PagedResult` mặc định 20 / tối đa 100 → **FR-FOOD-01**, **NFR-03**
- [X] T011 [US1] ★ **Tier miễn phí**: Member không có Membership Active còn hạn → universe = **20 món đầu A→Z**, giới hạn **trước** khi lọc từ khoá; dùng lại `MembershipLifecycle.IsActiveOn` (spec 003) → **FR-FOOD-01**, D-705
- [X] T012 [US1] `FoodItemService.CreateAsync` — **find-or-create** theo tên: trùng → 200, mới → 201 → **FR-FOOD-02**
- [X] T013 [US1] `FoodItemsController.cs` route `api/v1/food-items`
- [X] T014 [US1] Unit test `tests/GymMaster.Api.Tests/FoodItemServiceTests.cs`

---

## Phase 4: US2 — Nhật ký bữa ăn (P1) 🎯 MVP

**Goal**: hội viên ghi lại đã ăn gì trong ngày.
**Independent Test**: thêm 2 đơn vị món 100 kcal → tổng ngày tăng đúng 200; thêm lại cùng món cùng bữa → khẩu phần cộng dồn, không tạo dòng mới; sửa calo món trong DB → log cũ giữ nguyên.

- [X] T015 [US2] `NutritionService` — tạo/ghép `MealLog` theo (member, `LogDate`, `MealType`) → **FR-MEAL-01**
- [X] T016 [US2] ★ **Snapshot** `Calories = CaloriesPerUnit × Quantity` khi INSERT `meal_log_items` → **NFR-02**, D-701
- [X] T017 [US2] Cộng dồn khẩu phần + calo khi thêm lại cùng món trong cùng bữa/ngày → **FR-MEAL-03**
- [X] T018 [US2] Chặn `quantity ≤ 0` / `mealType` không hợp lệ → 422 `INVALID_QUANTITY` → **FR-MEAL-02**
- [X] T019 [US2] `FoodItem` không tồn tại hoặc không active → 404 `FOOD_NOT_FOUND`
- [X] T020 [US2] `MealLogsController.cs` — `POST/GET /meal-logs`, ownership Member(self) / Admin / Staff / PT(assigned)
- [X] T021 [US2] `MealLogResponse` trả macro **từng món** (`proteinG/carbG/fatG` = giá trị/đơn vị × khẩu phần) để FE tự cộng → D-710

---

## Phase 5: US3 — Mục tiêu & tổng kết calo (P1)

**Goal**: hội viên biết hôm nay còn ăn được bao nhiêu.
**Independent Test**: đặt target 2000, ăn 1500 → `remaining = 500`; chưa đặt target → `remaining = null`, không phải 0.

- [X] T022 [US3] `NutritionService` — upsert `CalorieTarget` theo `(MemberId, EffectiveDate)`, validate `DailyCalories > 0`, macro ≥ 0 → **FR-CAL-TGT-01**
- [X] T023 [US3] `GET /members/{id}/calorie-target` — trả mục tiêu hiệu lực gần nhất ≤ hôm nay, chưa đặt → 404 `NO_TARGET` → **FR-CAL-TGT-02**
- [X] T024 [US3] `CalorieSummaryResponse` — `consumed`, `target`, `remaining` + đủ 9 field macro (consumed/target/remaining × protein/carb/fat) → **FR-CAL-01**
- [X] T025 [US3] `remaining = null` khi chưa đặt target (không trả 0) → D-709
- [X] T026 [US3] `GET /members/{id}/calorie-history?from=&to=` — mặc định 7 ngày; `from > to` → 422 `VALIDATION_ERROR`
- [X] T027 [US3] `MemberNutritionController.cs` route `api/v1/members`
- [X] T028 [US3] Cửa quyền PT: chỉ member được phân công active (dùng lại spec 005) → **FR-CAL-02**
- [X] T029 [US3] Unit test `tests/GymMaster.Api.Tests/NutritionServiceTests.cs`

---

## Phase 6: Polish & Cross-cutting

- [ ] T031 **Còn nợ (nợ kỹ thuật đã ghi nhận)** — snapshot **macro** vào `meal_log_items`: cần team DB thêm 3 cột (`ProteinG`, `CarbG`, `FatG`), sau đó sửa `NutritionService` ghi snapshot và đọc từ đó. Hiện macro lịch sử đọc live từ `food_items` nên **sẽ đổi nếu Admin sửa món** → lệch DATA-02 (spec §10, D-706)
- [X] T032 Test AC-07 tier free — `FoodItemTierTests.cs`, 6 test: giới hạn 20 món đầu A→Z, gói hết hạn rơi về tier free, Staff không bị giới hạn, trả danh sách rỗng thay vì 403

---

## Dependencies & Execution Order

- **Phụ thuộc ngoài**: spec 002 (`member_profiles`), spec 003 (`MembershipLifecycle` — quyết định tier free), spec 005 (cửa quyền PT).
- **US1 → US2**: chưa có món thì không ghi được bữa ăn.
- **US2 → US3**: summary cộng từ dữ liệu US2.
- **Feature phụ thuộc ngược**:
  - spec 006 (360°) đọc `CalorieSummaryResponse` — đổi contract summary là đụng 360°.
  - spec 009 (quét ảnh AI) tạo `FoodItem` với `Source = AI` rồi ghi qua đúng luồng US2.

```text
[002·003·005] → Setup → Foundational → US1 → US2 → US3 → Polish
                                              ↓      ↓
                                     [009 tạo món]  [006 đọc summary]
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01 | T016 | `NutritionServiceTests.cs` |
| AC-02 | T018 | `NutritionServiceTests.cs` |
| AC-03 | T012 | `FoodItemServiceTests.cs` |
| AC-04 | T024 | `NutritionServiceTests.cs` |
| AC-05 | T017 | `NutritionServiceTests.cs` |
| AC-06 | T016 | `NutritionServiceTests.cs` — **lưu ý: chỉ đúng với calo, macro vẫn đổi (T031)** |
| AC-07 | T011 | **chưa có test — T032** |
