# Safety Constraints — GymMaster

**Phiên bản:** 1.1 · **Phạm vi:** an toàn dữ liệu · an toàn khi dùng AI · an toàn khi phụ thuộc dịch vụ ngoài

> Luật ở đây bảo vệ thứ **không sửa lại được**: dữ liệu đã mất, thông tin đã rò rỉ, tiền đã trừ sai. Vi phạm luật ở [business.md](business.md) làm sai số liệu; vi phạm luật ở đây làm **hỏng không hồi phục**.
>
> Luật gốc về bảo mật (`SEC-01`…`SEC-05`, `DATA-01`, `AUDIT-01`) nằm ở [`CONSTITUTION.md`](../../../CONSTITUTION.md) Layer 1. File này bổ sung phần chưa được viết thành luật.

---

## Bảng tra nhanh

| ID | Luật | Thi hành ở | Trạng thái |
|---|---|---|---|
| **SAFE-01** | Audit log **append-only** | `IAuditService` — chỉ 1 hàm `LogAsync` | ✅ |
| **SAFE-02** | Không log mật khẩu / token / OTP / PII | `AuthService` · `AuditService` | ✅ |
| **SAFE-03** | Dữ liệu lịch sử phải **snapshot** | `meal_log_items.Calories` | ⚠️ **PARTIAL** — macro đọc live |
| **SAFE-04** | Không tin client cho quyết định về **tiền** | `VnPayService` | ✅ |
| **SAFE-05** | Endpoint `AllowAnonymous` phải có **cơ chế xác thực thay thế** | `VnPayLibrary` HMAC-SHA512 | ✅ |
| **SAFE-06** | Callback bên ngoài phải **idempotent** | `VnPayService` — 2 lớp chặn | ✅ |
| **SAFE-07** | Không lưu dữ liệu cá nhân không cần thiết | `FoodScanService` · `CloudinaryAvatarStorage` | ✅ |
| **SAFE-08** | Xoá cứng phải có **audit log** | `WorkoutPlanService` · `TrainerNoteService` | ✅ |
| **SAFE-09** | AI **hỗ trợ**, không tự quyết thay người dùng | `FoodScanService` — `requiresConfirmation` | ✅ |
| **SAFE-10** | Không cam kết độ chính xác định lượng của AI | Out of Scope spec 009 | ✅ |
| **SAFE-11** | Ranh giới khi dùng AI trong phát triển | `ai-workflow.md` §5 | ✅ |
| **SAFE-12** | Dịch vụ ngoài lỗi **không chặn** luồng chính | `GeminiService` · `VnPayService` | ✅ |
| **SAFE-13** | Cô lập nhà cung cấp sau **cổng trừu tượng** | `IFoodImageAnalyzer` · `IAvatarStorage` · `IEmailSender` | ✅ |
| **SAFE-14** | Gọi dịch vụ ngoài phải có **timeout** | `GeminiOptions.TimeoutSeconds` | ✅ |

**Giá trị đã kiểm chứng trong code:**

| Hằng số | Nơi khai báo | Giá trị |
|---|---|---|
| Timeout Gemini | `GeminiOptions.cs:19` | `20` giây |
| Giới hạn ảnh | `GeminiOptions.cs:17` | `5 MB` |
| Hạn link VNPay | `VnPayService` | **15 phút** (ngắn hơn TTL đơn 30 phút) |
| `IAuditService` | `IAuditService.cs` | **1 hàm duy nhất** `LogAsync` — không có sửa/xoá |
| `AllowAnonymous` | `VnPayController.cs:31, 41` | **2 chỗ duy nhất**, đều verify HMAC-SHA512 |

---

## Dữ liệu

### SAFE-01 · Audit log là **append-only**
`IAuditService` **SHALL** chỉ có hàm ghi. **KHÔNG** có API sửa/xoá audit log.
*Vì sao:* audit sửa được thì không còn là audit.
📍 `Features/Dashboard/IAuditService.cs` · spec 008 NFR-03

### SAFE-02 · Không ghi mật khẩu / token / OTP / PII đầy đủ vào log và metadata
Áp cho cả `ILogger` lẫn trường `audit_logs.Metadata` — metadata chỉ chứa id và trường nghiệp vụ.
📍 `AuthService` · `AuditService` · spec 001 NFR-01, spec 002 NFR-03, spec 008 FR-AUD-03

### SAFE-03 · Dữ liệu lịch sử phải **snapshot**, không đọc live từ nguồn có thể đổi
Giá trị đã ghi vào nhật ký **SHALL** giữ nguyên khi dữ liệu gốc bị sửa về sau.
📍 `meal_log_items.Calories` lưu `CaloriesPerUnit × Quantity` · spec 007 NFR-02

> ⚠️ **Đang vi phạm một phần.** Calo đã snapshot đúng, nhưng **macro** (protein/carb/fat) vẫn đọc live từ `food_items` → Admin sửa món là **số liệu dinh dưỡng lịch sử đổi theo**. Cần team DB thêm 3 cột — việc **B-01** (ưu tiên P1) trong [BACKLOG](../../03-Interface-Specs/feature-specs/BACKLOG.md).

### SAFE-04 · Không tin dữ liệu từ client cho quyết định về tiền
Số tiền, trạng thái kích hoạt, quyền truy cập **SHALL** tính ở server. Callback từ cổng thanh toán phải **đối chiếu số tiền** trước khi kích hoạt.
📍 `VnPayService` · spec 010 FR-VNP-05/08/09

### SAFE-05 · Endpoint `AllowAnonymous` phải có **cơ chế xác thực thay thế**
Không thể yêu cầu JWT (webhook, callback) thì bắt buộc verify chữ ký. VNPay IPN + Return dùng **HMAC-SHA512**, verify ở **cả hai** đường.
📍 `Infrastructure/VnPayLibrary.cs` · spec 010 FR-VNP-04, NFR-01 · liên quan `CONSTITUTION.md` SEC-02

### SAFE-06 · Callback từ bên ngoài phải **idempotent**
Cổng thanh toán gửi lại nhiều lần theo thiết kế → xử lý trùng **KHÔNG** được kích hoạt lại (double-activation = cộng nhầm hạn gói).
📍 `VnPayService` — đã `Paid` thì trả `RspCode 02` · spec 010 FR-VNP-06

### SAFE-07 · Không lưu dữ liệu cá nhân không cần thiết
Ảnh bữa ăn xử lý **trong bộ nhớ rồi bỏ**, không lưu file, không lưu lịch sử quét. Avatar chỉ lưu URL, DB không chứa nhị phân.
📍 `FoodScanService` (spec 009) · `CloudinaryAvatarStorage` (spec 002)

### SAFE-08 · Xoá cứng phải có audit log
`CONSTITUTION.md` DATA-01 mặc định soft delete. Mọi chỗ xoá cứng **SHALL** ghi audit log, vì dữ liệu không khôi phục được thì audit là dấu vết duy nhất còn lại.
📍 `DELETE /workout-plans/{id}` → `DELETE_WORKOUT_PLAN` · `DELETE /trainer-notes/{id}` → `DELETE_TRAINER_NOTE` (`Features/Training/WorkoutPlanService.cs`, `TrainerNoteService.cs`)

> Đây là **hai chỗ xoá cứng duy nhất** trong hệ thống. Lý do thiết kế (bản nháp huấn luyện, không phải chứng từ) ghi ở [`005-pt-training/plan.md`](../../03-Interface-Specs/feature-specs/005-pt-training/plan.md) D-509 §8. Còn thiếu: một dòng ADR ở [`decision-log.md`](../../06-Management/decision-log.md) ghi nhận ngoại lệ này của DATA-01 → việc **B-02** trong [BACKLOG](../../03-Interface-Specs/feature-specs/BACKLOG.md) (đã hạ xuống P4, chỉ là tài liệu).

---

## Dùng AI

### SAFE-09 · AI **hỗ trợ**, không tự quyết định thay người dùng
Kết quả AI **SHALL** ở dạng nháp cần xác nhận. Quét ảnh **KHÔNG** tự tạo `FoodItem`, **KHÔNG** tự tạo `MealLog`.
📍 `FoodScanService` — `requiresConfirmation = true` · spec 009 FR-IMG-03 · ADR **D-08**

### SAFE-10 · Không cam kết độ chính xác định lượng của AI
Con người xác nhận là lớp kiểm soát cuối. Ghi rõ ở Out of Scope của spec 009.

### SAFE-11 · Ranh giới khi dùng AI trong phát triển
- **KHÔNG** dán secret / connection string thật vào prompt.
- **KHÔNG** để AI tự quyết business rule chưa chốt — báo lại thay vì tự đoán.
- **KHÔNG** merge code AI chưa qua Validation Gate ([`test-plan.md`](../../04-Test-Specs/test-plan.md) §6).
- Agent **KHÔNG** được xoá file, sửa `CONSTITUTION.md`, push `main`, thêm dependency mới, hay chạy migration destructive khi chưa có người xác nhận.
📍 chi tiết ở [`06-Management/ai-workflow.md`](../../06-Management/ai-workflow.md) §5 và `CONSTITUTION.md` mục AI Agent Policy

---

## Phụ thuộc dịch vụ ngoài

### SAFE-12 · Dịch vụ ngoài lỗi **không được chặn luồng nghiệp vụ chính**
Gemini lỗi/timeout → 502, nhập tay vẫn chạy. VNPay sập → luồng thanh toán thủ công vẫn giữ làm fallback. Cloudinary chưa cấu hình → 500 rõ ràng, không làm hỏng luồng sửa hồ sơ.
📍 `GeminiService` (spec 009) · `VnPayService` (spec 010) · `CloudinaryAvatarStorage` (spec 002)

### SAFE-13 · Cô lập nhà cung cấp sau một cổng trừu tượng
Dịch vụ ngoài **SHALL** đứng sau interface (`IFoodImageAnalyzer`, `IAvatarStorage`, `IEmailSender`) để đổi nhà cung cấp không lan vào nghiệp vụ.
*Đã được kiểm chứng:* đổi Google Cloud Vision → Gemini chỉ phải viết lại `GeminiService.cs` (ADR **D-21**).

### SAFE-14 · Gọi dịch vụ ngoài phải có timeout
Không để request treo vô hạn. `Gemini:TimeoutSeconds` mặc định 20s; link thanh toán VNPay hết hạn sau 15 phút.
📍 `Options/GeminiOptions.cs` · `VnPayOptions`

---

## Tình trạng tuân thủ

| Luật | Trạng thái | Việc cần làm |
|---|---|---|
| SAFE-03 snapshot lịch sử | ⚠️ **PARTIAL** — macro đọc live | [B-01](../../03-Interface-Specs/feature-specs/BACKLOG.md) (P1, chặn bởi team DB) |
| SAFE-08 xoá cứng có audit | ✅ PASS — audit đã ghi; chỉ thiếu dòng ADR | [B-02](../../03-Interface-Specs/feature-specs/BACKLOG.md) (P4, tài liệu) |
| SAFE-01 audit append-only | ✅ PASS — 34 action / 13 service | — |
| Còn lại | ✅ PASS | — |
