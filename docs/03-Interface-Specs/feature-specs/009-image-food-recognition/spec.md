# Feature Specification: Image Food Recognition Assist (AI — Gemini)

**Feature Branch**: `009-image-food-recognition`
**Created**: 2026-05-30
**Status**: Implemented (spec đồng bộ theo code 2026-07-15 — đã đổi từ Google Cloud Vision sang **Gemini Vision**)
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: srs-use-cases (UC-26), requirements (ENH-01), ADR-04, D-08, OQ-09

> EARS legend như spec 001. Mọi path dưới `/api/v1`. **Enhancement** — bổ sung cho nhật ký ăn (spec 007), không thay thế nhập tay.

---

## 1. Context & Goal
Member (có gói tập active) upload ảnh bữa ăn; hệ thống dùng **Gemini Vision** (`gemini-2.5-flash`) để **nhận diện nhiều món** trong ảnh, kèm **ước lượng dinh dưỡng** (calo/đạm/tinh bột/béo trên 100g) và **khối lượng ước lượng (gram)**. Món khớp database → dùng bản ghi có sẵn; món chưa có → trả **nháp AI** để member xác nhận trước khi lưu. Con người là người quyết định cuối. Tính năng này **chỉ dành cho hội viên có gói tập** (không phải member free/khách).

## 2. Actors
| Actor | Vai trò |
|---|---|
| Member (có gói active) | Upload ảnh, xem nhiều món nhận diện, xác nhận lưu món AI |
| System | Gọi Gemini, đối chiếu FoodItem, trả kết quả Database/AI |
| External (Gemini API) | Phân tích ảnh → danh sách món + dinh dưỡng ước lượng |

## 3. Functional Requirements (EARS)
- **FR-IMG-01 (Event):** WHEN Member (có gói active) upload ảnh hợp lệ (`POST /foods/scan-image`), THE system SHALL gửi ảnh tới Gemini và nhận danh sách món (tên, độ tin cậy, calo/macro ước lượng, gram).
- **FR-IMG-02 (Event):** WHEN có kết quả, THE system SHALL với mỗi món: nếu khớp `food_items` (khớp chính xác hoặc chứa tên, collation không dấu) → trả `resultSource="Database"` (kèm `food`, `requiresConfirmation=false`); nếu không → trả `resultSource="AI"` với `draft` (nháp dinh dưỡng, `requiresConfirmation=true`).
- **FR-IMG-03 (Event):** WHEN Member xác nhận một món AI (`POST /foods/confirm-ai-food`), THE system SHALL lưu FoodItem mới (`Unit="g"`, `ServingSize=100`, `Source="AI"`) — find-or-create theo tên; sau đó món dùng để ghi MealLog (spec 007). KHÔNG tự tạo MealLog từ ảnh.
- **FR-IMG-04 (Unwanted):** IF Gemini lỗi/timeout, THEN 502 (fallback nhập tay ở spec 007, không chặn luồng).
- **FR-IMG-05 (Unwanted):** IF Member không có gói tập active (hoặc không phải role Member), THEN 403 `MEMBERSHIP_REQUIRED`.
- **FR-IMG-06 (Unwanted):** IF ảnh không phải JPG/PNG hoặc > 5MB (hoặc rỗng), THEN 422 `INVALID_FILE`.

## 4. Non-functional Requirements
- **NFR-01:** Gọi Gemini có timeout (`Gemini:TimeoutSeconds`, mặc định 20s); lỗi → 502, không block luồng nhập tay.
- **NFR-02:** Ảnh ≤ 5MB (`Gemini:MaxImageBytes`), JPG/PNG; controller giới hạn 6MB request.
- **NFR-03:** `Gemini:ApiKey` chỉ ở server (User Secrets/env), không hard-code.
- **NFR-04:** Trùng tên món trong một lần quét được lọc bỏ (mỗi tên xuất hiện 1 lần).

## 5. Data Model
- **Tái dùng `food_items`** (spec 007): món Aic lưu `Source="AI"`, `Unit="g"`, `ServingSize=100`.
- **KHÔNG có bảng MealImageUploads** — kết quả quét trả thẳng cho FE (stateless), không lưu ảnh/kết quả trong DB (chỉ AuditLog `CONFIRM_AI_FOOD` khi lưu món).
- Ảnh không lưu trữ (xử lý trong bộ nhớ rồi bỏ).

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/foods/scan-image | Member (có gói active) | multipart: image (JPG/PNG ≤5MB) | 200 FoodScanResponse | 403, 422, 502 |
| POST | /api/v1/foods/confirm-ai-food | Member (có gói active) | {name, caloriesPerUnit, proteinG?, carbsG?, fatG?} | 200/201 ScannedFood | 400, 403 |

**FoodScanResponse:** `{ items: [ FoodScanItem ] }`.
**FoodScanItem:** `{ recognizedName, confidence, resultSource: "Database"|"AI", requiresConfirmation, food?: ScannedFood, draft?: FoodNutritionDraft, estimatedGrams }`.
**ScannedFood:** `{ id, name, unit, servingSize, caloriesPerUnit, proteinG, carbsG, fatG, source }`.
**FoodNutritionDraft:** `{ name, unit, servingSize, caloriesPerUnit, proteinG, carbsG, fatG, source }`.

## 7. Error Handling (EARS Unwanted)
- IF ảnh sai định dạng/quá lớn/rỗng, THEN 422 `INVALID_FILE`.
- IF Member không có gói active, THEN 403 `MEMBERSHIP_REQUIRED`.
- IF Gemini lỗi/timeout, THEN 502 (mã lỗi từ analyzer) — hướng dẫn nhập tay.
- IF confirm với tên rỗng hoặc dinh dưỡng < 0, THEN 400 `VALIDATION_ERROR`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given Member có gói + ảnh hợp lệ, When quét, Then nhận danh sách nhiều món (Database/AI) kèm gram ước lượng.
- [ ] **AC-02:** Given món chưa có DB, When quét, Then trả `resultSource="AI"` + `draft`, `requiresConfirmation=true`; chưa có FoodItem/MealLog nào được tạo.
- [ ] **AC-03:** Given Member xác nhận món AI, When confirm, Then lưu FoodItem `Source="AI"` (trùng tên → trả món cũ) và dùng để ghi MealLog.
- [ ] **AC-04:** Given Member KHÔNG có gói active, When quét, Then 403 `MEMBERSHIP_REQUIRED`.
- [ ] **AC-05:** Given ảnh > 5MB hoặc không phải JPG/PNG, When quét, Then 422 `INVALID_FILE`.
- [ ] **AC-06:** Given Gemini timeout, When quét, Then 502, member vẫn nhập tay được (spec 007).

## 9. Out of Scope
- Tự tạo MealLog trực tiếp từ ảnh (luôn cần member xác nhận khẩu phần ở spec 007).
- Lưu trữ ảnh/lịch sử quét trong DB; nhận diện offline; cam kết độ chính xác định lượng.
