---
description: "Task list — Image Food Recognition Assist (AI — Gemini)"
---

# Tasks: Image Food Recognition Assist (AI — Gemini)

**Feature**: `009-image-food-recognition`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 22/23 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code, `[ ]` = còn nợ.

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 `Options/GeminiOptions.cs` — `ApiKey`, `TimeoutSeconds` (mặc định 20), `MaxImageBytes` (5MB) + binding trong `Program.cs`
- [X] T002 `ApiKey` chỉ ở server (User Secrets / biến môi trường), **không hard-code** → **NFR-03**
- [X] T003 Migration `database/009_food_scan_columns.sql` — thêm `ServingSize` + `Source` vào `food_items`

## Phase 2: Foundational

- [X] T004 ★ `Infrastructure/IFoodImageAnalyzer.cs` — **cổng trừu tượng** cô lập nhà cung cấp AI → D-901
- [X] T005 `Infrastructure/GeminiService.cs` — gọi REST API `gemini-2.5-flash`, đăng ký bằng `AddHttpClient<IFoodImageAnalyzer, GeminiService>()` trong `Program.cs`
- [X] T006 Prompt yêu cầu trả **nhiều món** kèm calo/macro **trên 100g** + `estimatedGrams` → **FR-IMG-01**, D-908
- [X] T007 Timeout theo `GeminiOptions.TimeoutSeconds`; lỗi/timeout → map thành **502** → **FR-IMG-04**, **NFR-01**, D-904
- [X] T008 `Entities/FoodItem.cs` — bổ sung `Source {Admin, AI}` (dùng chung spec 007) → D-907
- [X] T009 Đăng ký DI `IFoodScanService` → `FoodScanService` trong `Program.cs`
- [X] T010 Unit test `tests/GymMaster.Api.Tests/GeminiServiceTests.cs` — parse response + xử lý lỗi

**Checkpoint**: gọi được Gemini và có cổng mock được → US1 bắt đầu.

---

## Phase 3: US1 — Quét ảnh nhận diện món (P1) 🎯 MVP

**Goal**: hội viên chụp bữa ăn thay vì gõ tay từng món.
**Independent Test**: Member có gói + ảnh JPG 2MB → 200 với danh sách nhiều món, món có trong kho → `resultSource="Database"`, món lạ → `resultSource="AI"` + `draft`; kiểm DB: **chưa có** `FoodItem`/`MealLog` nào được tạo.

- [X] T011 [US1] `FoodScanDtos.cs` — `FoodScanResponse`, `FoodScanItem`, `ScannedFood`, `FoodNutritionDraft`
- [X] T012 [US1] `FoodScanController.cs` route `api/v1/foods`, giới hạn request **6MB**
- [X] T013 [US1] Validate ảnh: JPG/PNG, ≤ 5MB, không rỗng → 422 `INVALID_FILE` → **FR-IMG-06**, **NFR-02**
- [X] T014 [US1] Gác quyền: role Member **và** có Membership Active còn hạn — dùng lại `MembershipLifecycle.IsActiveOn` (spec 003) → 403 `MEMBERSHIP_REQUIRED` → **FR-IMG-05**, D-905
- [X] T015 [US1] `FoodScanService.ScanAsync` — gọi `IFoodImageAnalyzer`, xử lý ảnh **trong bộ nhớ, không lưu trữ** → D-902
- [X] T016 [US1] Lọc **trùng tên** trong cùng lần quét (mỗi tên xuất hiện 1 lần) → **NFR-04**
- [X] T017 [US1] ★ Đối chiếu `food_items`: khớp chính xác **hoặc chứa**, collation không dấu → `resultSource = "Database"` (kèm `food`, `requiresConfirmation=false`) | `"AI"` (kèm `draft`, `requiresConfirmation=true`) → **FR-IMG-02**, D-909
- [X] T018 [US1] ★ Quét **KHÔNG** tạo `FoodItem`, **KHÔNG** tạo `MealLog` → **FR-IMG-03**, D-903
- [X] T019 [US1] Unit test `tests/GymMaster.Api.Tests/FoodScanServiceTests.cs` (mock `IFoodImageAnalyzer`)

---

## Phase 4: US2 — Xác nhận món AI (P1)

**Goal**: người dùng là người quyết định cuối, kho món không bị AI làm bẩn.
**Independent Test**: `POST /foods/confirm-ai-food` tên mới → 201, `Source="AI"`, `Unit="g"`, `ServingSize=100`; gọi lại cùng tên → 200 trả món cũ; sau đó ghi `MealLog` bằng món đó qua luồng spec 007.

- [X] T020 [US2] `confirm-ai-food` — **find-or-create** theo tên: trùng → 200 món cũ, mới → 201 → **FR-IMG-03**, D-910
- [X] T021 [US2] Lưu với `Unit = "g"`, `ServingSize = 100`, `Source = "AI"` → D-906
- [X] T022 [US2] Validate: tên rỗng hoặc dinh dưỡng < 0 → 400 `VALIDATION_ERROR`
- [X] T023 [US2] Ghi AuditLog `CONFIRM_AI_FOOD` (spec 008) → **SEC-05**

---

## Phase 5: Polish & Cross-cutting

- [X] T024 [P] **Refactor đã thực hiện**: đổi nhà cung cấp từ Google Cloud Vision → **Gemini Vision**, chỉ viết lại `GeminiService.cs`, không đụng `FoodScanService`/controller (minh chứng giá trị của T004)
- [ ] T026 **Còn nợ** — test cho AC-06 (Gemini timeout → 502 và luồng nhập tay spec 007 vẫn chạy). `GeminiServiceTests.cs` có phủ xử lý lỗi ở tầng analyzer, nhưng chưa có test đầu-cuối chứng minh fallback không bị chặn

---

## Dependencies & Execution Order

- **Phụ thuộc ngoài**:
  - spec 003 — `MembershipLifecycle` quyết định ai được dùng tính năng (T014);
  - spec 007 — bảng `food_items` + cơ chế find-or-create + luồng ghi `MealLog` sau khi xác nhận;
  - spec 008 — `IAuditService` cho `CONFIRM_AI_FOOD`.
- **T004 (cổng trừu tượng) chặn T005 và T019**: không có interface thì không mock được để test.
- **US1 → US2**: xác nhận là bước tiếp theo của quét.
- **Không có feature nào phụ thuộc ngược** vào 009 — đây là enhancement thuần, gỡ ra hệ thống vẫn chạy đủ.

```text
[003 gác gói · 007 food_items · 008 audit]
                 ↓
Setup → Foundational(IFoodImageAnalyzer ★) → US1 → US2 → Polish
                                                    ↓
                                    [ghi MealLog qua luồng spec 007]
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01 | T015, T017 | `FoodScanServiceTests.cs` |
| AC-02 | T017, T018 | `FoodScanServiceTests.cs` |
| AC-03 | T020, T021 | `FoodScanServiceTests.cs` |
| AC-04 | T014 | black-box (token Member không gói → 403) |
| AC-05 | T013 | black-box (upload ảnh 6MB) |
| AC-06 | T007 | **chưa có test đầu-cuối — T026** |
