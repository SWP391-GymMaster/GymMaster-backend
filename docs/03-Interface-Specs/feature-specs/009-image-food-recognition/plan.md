# Implementation Plan: Image Food Recognition Assist (AI — Gemini)

**Feature Branch**: `009-image-food-recognition` | **Spec**: [spec.md](spec.md)
**Status**: `Implemented`
**Input**: `docs/03-Interface-Specs/feature-specs/009-image-food-recognition/spec.md`

> **Enhancement** cho [spec 007](../007-nutrition-calorie/plan.md) — bổ sung, **không thay thế** luồng nhập tay.

---

## 1. Summary

Member upload ảnh bữa ăn → Gemini Vision (`gemini-2.5-flash`) nhận diện **nhiều món** kèm ước lượng dinh dưỡng và khối lượng gram → hệ thống đối chiếu `food_items`:
- khớp → trả món có sẵn (`resultSource = "Database"`, không cần xác nhận);
- không khớp → trả **nháp AI** (`resultSource = "AI"`, `requiresConfirmation = true`).

Ba nguyên tắc thiết kế chi phối toàn bộ feature:

1. **Con người quyết định cuối** — AI không bao giờ tự ghi dữ liệu. Quét ảnh **không tạo** `FoodItem`, **không tạo** `MealLog`. Chỉ khi member bấm xác nhận mới lưu.
2. **Stateless** — không có bảng nào cho feature này. Ảnh xử lý trong bộ nhớ rồi bỏ; kết quả quét trả thẳng cho FE.
3. **Không chặn luồng chính** — Gemini lỗi/timeout → 502, member vẫn nhập tay bình thường qua spec 007.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | `IHttpClientFactory` (`AddHttpClient<IFoodImageAnalyzer, GeminiService>`) — gọi REST API Gemini trực tiếp, không dùng SDK |
| **Mô hình AI** | `gemini-2.5-flash` (Gemini Vision) — **đã đổi từ Google Cloud Vision** |
| **Storage** | **Không có bảng riêng** — tái dùng `food_items` của spec 007 (`Source = "AI"`, `Unit = "g"`, `ServingSize = 100`) |
| **Cấu hình** | `Options/GeminiOptions.cs` — `ApiKey`, `TimeoutSeconds` (mặc định 20), `MaxImageBytes` (5MB) |
| **Testing** | xUnit — `FoodScanServiceTests.cs`, `GeminiServiceTests.cs` |
| **Target Platform** | Cloud Run + Cloud SQL |
| **Constraints** | Ảnh JPG/PNG ≤ 5MB (controller giới hạn request 6MB); chỉ Member **có gói active** |
| **Scale/Scope** | 2 endpoint, 0 bảng mới |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| SEC-05 — API key chỉ ở server, không hard-code | ✅ PASS | `GeminiOptions.ApiKey` từ User Secrets/env (NFR-03) |
| SAFE-09 — AI hỗ trợ, không tự quyết định thay người dùng | ✅ PASS | `requiresConfirmation = true`; không tự tạo `FoodItem`/`MealLog` (FR-IMG-03) |
| SAFE-12 — dịch vụ ngoài lỗi không chặn luồng nghiệp vụ | ✅ PASS | 502 + fallback nhập tay spec 007 (FR-IMG-04) |
| GBL-02 — không lặp business rule | ✅ PASS | gác gói dùng lại `MembershipLifecycle` (003); tạo món dùng find-or-create của spec 007 |
| AUDIT-01 — hành động quan trọng ghi AuditLog | ✅ PASS | `CONFIRM_AI_FOOD` khi lưu món |
| SAFE-07 — không lưu dữ liệu cá nhân không cần thiết | ✅ PASS | ảnh không lưu trữ, xử lý trong bộ nhớ rồi bỏ |
| ARCH-02 — wrapper `ApiResponse<T>` | ✅ PASS | cả 2 endpoint |

## 4. Project Structure

```text
backend/GymMaster.API/
├── Features/Nutrition/
│   ├── FoodScanController.cs      # route "api/v1/foods" — scan-image, confirm-ai-food
│   ├── IFoodScanService.cs · FoodScanService.cs   # ★ đối chiếu DB ↔ kết quả AI
│   └── FoodScanDtos.cs            # FoodScanResponse, FoodScanItem, ScannedFood, FoodNutritionDraft
├── Infrastructure/
│   ├── IFoodImageAnalyzer.cs      # ★ cổng trừu tượng — cô lập nhà cung cấp AI
│   └── GeminiService.cs           # triển khai bằng Gemini REST API
├── Options/GeminiOptions.cs       # ApiKey, TimeoutSeconds, MaxImageBytes
└── Entities/FoodItem.cs           # dùng chung spec 007 — Source {Admin, AI}

database/
└── 009_food_scan_columns.sql      # thêm ServingSize + Source vào food_items

tests/GymMaster.Api.Tests/
├── FoodScanServiceTests.cs        # logic đối chiếu DB/AI (mock IFoodImageAnalyzer)
└── GeminiServiceTests.cs          # parse response + xử lý lỗi
```

**Structure Decision**: `IFoodImageAnalyzer` là **cổng trừu tượng** đặt ở `Infrastructure/`, tách hẳn khỏi `FoodScanService`. Giá trị của việc này đã được kiểm chứng thực tế: dự án **đã đổi từ Google Cloud Vision sang Gemini Vision** mà chỉ phải viết lại `GeminiService.cs`, không đụng `FoodScanService` hay controller. Đây cũng là lý do unit test mock được nhà cung cấp AI.

## 5. Design Decisions

> **Chi tiết hoá ADR dự án**: [D-08](../../../06-Management/decision-log.md) (AI Vision = enhancement only, người xác nhận) → **D-903** · [D-21](../../../06-Management/decision-log.md) (đổi Cloud Vision → Gemini) → **D-901** (chính cổng `IFoodImageAnalyzer` làm cho lần đổi đó chỉ tốn 1 file).

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-901 | **Cổng `IFoodImageAnalyzer`** tách khỏi nghiệp vụ | Nhà cung cấp AI là thứ dễ thay nhất; đã thực sự đổi Vision → Gemini | Thêm một lớp trừu tượng cho đúng một implementation |
| D-902 | **Stateless** — không bảng, không lưu ảnh, không lưu lịch sử quét | Ảnh bữa ăn là dữ liệu cá nhân; không lưu là an toàn nhất và rẻ nhất | Không xem lại được lịch sử quét; không có dữ liệu để cải thiện độ chính xác |
| D-903 | Quét **không tạo** `FoodItem`/`MealLog`, phải xác nhận | AI ước lượng có sai số; tự ghi sẽ làm bẩn kho món và sai nhật ký calo | Thêm một bước bấm cho người dùng |
| D-904 | Gemini lỗi/timeout → **502**, không 500 | 502 nói đúng bản chất: lỗi ở dịch vụ thượng nguồn, không phải lỗi hệ thống | FE phải xử lý riêng mã 502 để hiện gợi ý nhập tay |
| D-905 | Chỉ Member **có gói active** dùng được → 403 `MEMBERSHIP_REQUIRED` | Mỗi lần quét tốn tiền API — gắn với gói trả phí; dùng lại `MembershipLifecycle` (003) | Người dùng free không trải nghiệm được tính năng nổi bật nhất |
| D-906 | Món AI lưu `Unit = "g"`, `ServingSize = 100` cố định | Gemini trả dinh dưỡng **trên 100g** — chuẩn hoá về đúng đơn vị đó, không phải quy đổi | Món tính theo "cái"/"tô" phải sửa tay sau khi xác nhận |
| D-907 | Cột `Source {Admin, AI}` trên `food_items` | Phân biệt được món do người nhập và món do AI ước lượng khi rà soát chất lượng kho | Thêm cột vào bảng của spec 007 (migration `009_food_scan_columns.sql`) |
| D-908 | Nhận diện **nhiều món** trong một ảnh, kèm `estimatedGrams` | Bữa ăn Việt Nam hiếm khi một món; trả một món sẽ vô dụng | Prompt phức tạp hơn, kết quả nhiễu hơn, phải lọc trùng tên (NFR-04) |
| D-909 | Đối chiếu tên theo **khớp chính xác hoặc chứa**, collation không dấu | Gemini trả "Phở bò tái" phải khớp được "Phở bò" trong kho | Khớp "chứa" có thể ghép nhầm món gần giống |
| D-910 | `confirm-ai-food` là **find-or-create** theo tên (200/201) | Dùng đúng cơ chế của spec 007 → không sinh món trùng | Client phải phân biệt 200 (món cũ) với 201 (món mới) |
| D-911 | Timeout cấu hình được, mặc định **20s** | Gemini Vision chậm hơn API thường; 20s là ngưỡng người dùng còn chờ được | Request có thể treo tới 20s trước khi báo lỗi |

## 6. Data Flow

```text
Quét ảnh:
  POST /api/v1/foods/scan-image   (multipart: image)
    → FoodScanController  (giới hạn request 6MB)
        ├─ role Member VÀ có Membership Active còn hạn?
        │     (MembershipLifecycle.IsActiveOn — spec 003)   → 403 MEMBERSHIP_REQUIRED
        ├─ ảnh JPG/PNG, ≤ 5MB, không rỗng?                 → 422 INVALID_FILE
        └─ FoodScanService.ScanAsync
             ├─ IFoodImageAnalyzer.AnalyzeAsync(bytes)      ← GeminiService
             │     └─ POST Gemini REST (gemini-2.5-flash), timeout 20s
             │           lỗi/timeout                        → 502  (D-904)
             │     ← [{ name, confidence, calo/macro per 100g, estimatedGrams }]
             ├─ lọc **trùng tên** trong cùng lần quét        (NFR-04)
             └─ với mỗi món: tra food_items (khớp chính xác | chứa, collation không dấu)
                   ├─ khớp    → { resultSource: "Database", food, requiresConfirmation: false }
                   └─ không   → { resultSource: "AI", draft, requiresConfirmation: true }
    → 200 FoodScanResponse { items: [...] }
       ★ KHÔNG tạo FoodItem, KHÔNG tạo MealLog ở bước này   (D-903)

Xác nhận món AI:
  POST /api/v1/foods/confirm-ai-food {name, caloriesPerUnit, proteinG?, carbsG?, fatG?}
    ├─ tên rỗng / dinh dưỡng < 0     → 400 VALIDATION_ERROR
    ├─ find-or-create theo tên: trùng → 200 món cũ | mới → 201
    │     lưu { Unit: "g", ServingSize: 100, Source: "AI" }   (D-906, D-907)
    └─ AuditLog "CONFIRM_AI_FOOD"
  → sau đó member ghi MealLog qua luồng thường của spec 007
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-IMG-01 | `Infrastructure/GeminiService.cs` + `IFoodImageAnalyzer.cs` |
| FR-IMG-02 | `Features/Nutrition/FoodScanService.cs` — đối chiếu `food_items`, gắn `resultSource` |
| FR-IMG-03 | `FoodScanService` — `confirm-ai-food` find-or-create, `Source = "AI"` |
| FR-IMG-04 | `GeminiService` — timeout + map lỗi → 502 |
| FR-IMG-05 | `FoodScanController.cs` — gác gói active (dùng `MembershipLifecycle` — spec 003) |
| FR-IMG-06 | `FoodScanController.cs` — validate định dạng + kích thước ảnh |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| Thêm lớp trừu tượng `IFoodImageAnalyzer` cho một implementation | Đã trả lời bằng thực tế: đổi Vision → Gemini chỉ sửa 1 file | Gọi thẳng Gemini trong `FoodScanService` → lần đổi nhà cung cấp phải sửa cả nghiệp vụ và test |
| Không lưu ảnh/lịch sử quét (D-902) | Ảnh bữa ăn là dữ liệu cá nhân; lưu là thêm rủi ro và chi phí | Lưu để cải thiện model → cần policy dữ liệu cá nhân, ngoài phạm vi đồ án |
| Feature phụ thuộc dịch vụ ngoài có thể fail | Đã cô lập: fail chỉ mất tính năng phụ, luồng nhập tay không ảnh hưởng | Coi AI là bắt buộc → Gemini sập là nhật ký ăn sập theo |
| Khớp tên kiểu "chứa" (D-909) | Tên món AI trả thường dài hơn tên trong kho | Chỉ khớp chính xác → hầu như luôn ra `resultSource = "AI"`, kho món phình nhanh |
| Chưa cam kết độ chính xác định lượng | Đã ghi rõ Out of Scope; con người xác nhận là lớp kiểm soát cuối | — |
| Món AI cố định `Unit = "g"` (D-906) | Gemini trả dinh dưỡng chuẩn trên 100g | Cho AI tự chọn đơn vị → dữ liệu không so sánh được giữa các món |
