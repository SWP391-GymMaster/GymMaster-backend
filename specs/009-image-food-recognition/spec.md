# Feature Specification: Image Food Recognition Assist (Enhancement)

**Feature Branch**: `009-image-food-recognition`
**Created**: 2026-05-30
**Status**: Approved — Enhancement (ngoài MVP, làm sau v1.0)
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 03_SRS (UC-26), 04 (ENH-01), ADR-04, D-08, OQ-09

> EARS legend như spec 001. **Lưu ý:** đây là enhancement — chỉ triển khai sau khi core + secondary ổn định.

---

## 1. Context & Goal
Member upload ảnh bữa ăn; hệ thống dùng dịch vụ nhận diện (Google Cloud Vision) để **gợi ý tên món/nguyên liệu**, giúp nhập MealLog nhanh hơn. KHÔNG tự định lượng calo, KHÔNG thay thế nhập tay. Mục tiêu: giảm ma sát nhập liệu, giữ con người là người quyết định.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Member | Upload ảnh, xác nhận/chỉnh gợi ý, nhập khẩu phần |
| System | Gọi dịch vụ nhận diện, map gợi ý → FoodItem |
| External (Vision API) | Trả nhãn/tên món gợi ý |

## 3. Functional Requirements (EARS)
- **FR-IMG-01 (Event):** WHEN Member upload ảnh bữa ăn hợp lệ, THE system SHALL gửi ảnh tới dịch vụ nhận diện và nhận danh sách gợi ý tên món/nguyên liệu.
- **FR-IMG-02 (Event):** WHEN có kết quả gợi ý, THE system SHALL map sang FoodItem tương ứng (nếu có) và hiển thị để Member xác nhận/chỉnh.
- **FR-IMG-03 (Ubiquitous):** THE system SHALL yêu cầu Member nhập khẩu phần và xác nhận trước khi tạo MealLog — KHÔNG tự lưu calo từ ảnh.
- **FR-IMG-04 (Unwanted):** IF dịch vụ nhận diện lỗi/timeout, THEN THE system SHALL fallback sang nhập tay (spec 007) không chặn luồng.
- **FR-IMG-05 (Unwanted):** IF không nhận diện được hoặc không có FoodItem khớp, THEN THE system SHALL cho Member nhập tay / Add Custom Food.
- **FR-IMG-06 (Optional):** WHERE gợi ý sai, THE system SHALL cho Member xóa/sửa từng mục trước khi lưu.

## 4. Non-functional Requirements
- **NFR-01:** Gọi Vision API có timeout (≤ 5s) + fallback; không block UI.
- **NFR-02:** Ảnh ≤ 5MB jpg/png; lưu Azure Blob, DB chỉ giữ URL.
- **NFR-03:** Không gửi PII kèm ảnh; tuân chính sách dữ liệu bên thứ ba.
- **NFR-04:** Chi phí API có hạn mức/giám sát.

## 5. Data Model
- Tái dùng **FoodItems**, **MealLogs**, **MealLogItems** (spec 007).
- **MealImageUploads**(Id, MemberId, BlobUrl, RecognizedJson NVARCHAR(MAX), Status{Pending,Confirmed,Failed}, CreatedAt) — lưu vết gợi ý, tách khỏi MealLog cho tới khi xác nhận.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/meal-images | Member(self) | multipart: image | 200 {uploadId, suggestions:[{name, foodItemId?}]} | 422, 502 |
| POST | /api/meal-images/{uploadId}/confirm | Member(self) | {items:[{foodItemId, quantity}]} | 201 (MealLog) | 404, 422 |

## 7. Error Handling (EARS Unwanted)
- IF ảnh sai định dạng/quá lớn, THEN 422 `INVALID_FILE`.
- IF Vision API lỗi/timeout, THEN 502 `RECOGNITION_UNAVAILABLE` + chỉ dẫn nhập tay.
- IF confirm với khẩu phần ≤ 0, THEN 422 `INVALID_QUANTITY`.
- IF uploadId không tồn tại/đã confirm, THEN 404/409.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given ảnh hợp lệ, When upload, Then nhận danh sách gợi ý tên món.
- [ ] **AC-02:** Given gợi ý hiển thị, When Member chưa xác nhận, Then chưa có MealLog nào được tạo.
- [ ] **AC-03:** Given Member sửa gợi ý sai + nhập khẩu phần + confirm, Then MealLog lưu món đã sửa với calo tính từ FoodItem×khẩu phần.
- [ ] **AC-04:** Given Vision API lỗi, When upload, Then luồng fallback nhập tay hoạt động, không mất dữ liệu.
- [ ] **AC-05:** Given không có FoodItem khớp, When confirm, Then Member có thể Add Custom Food.

## 9. Out of Scope
- Tự định lượng calo/khối lượng từ ảnh, ước lượng macro từ ảnh, nhận diện nhiều món phức tạp trong 1 ảnh với độ chính xác cam kết, offline recognition.
