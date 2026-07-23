# Implementation Plan: Operations Dashboard & Audit Log

**Feature Branch**: `008-dashboard-audit` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)
**Status**: `Implemented`
**Input**: `docs/03-Interface-Specs/feature-specs/008-dashboard-audit/spec.md`

---

## 1. Summary

Hai thành phần ngược chiều nhau về hướng phụ thuộc, nhưng cùng nằm trong slice `Dashboard/`:

- **Dashboard** — *đọc xuôi*: read-model aggregate từ `payments`, `memberships`, `check_ins`, `trainer_assignments`. Slice này phụ thuộc gần như mọi feature khác nhưng **không ai phụ thuộc ngược lại nó**.
- **AuditService** — *ghi ngược*: là hạ tầng dùng chung mà **7 slice khác inject vào**. Nó nằm ở đây vì audit log chỉ có một người tiêu thụ là màn tra cứu của Admin, nhưng về bản chất là cross-cutting concern.

Nguyên tắc xuyên suốt: **số liệu tính từ dữ liệu thật, không mock, không cache**; kỳ trống trả `0` chứ không báo lỗi.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | EF Core 10 (SqlServer) |
| **Storage** | SQL Server — `audit_logs` (ghi); dashboard đọc `payments`, `memberships`, `check_ins`, `trainer_assignments` |
| **Metadata** | `NVARCHAR(MAX)` chứa JSON |
| **Đồng hồ** | Mốc ngày/tháng/giờ cao điểm theo **giờ VN** (`Common/AppClock.cs`) |
| **Testing** | xUnit — `tests/GymMaster.Api.Tests/DashboardServiceTests.cs` |
| **Target Platform** | Cloud Run + Cloud SQL |
| **Performance Goals** | Dashboard < 2s với ~1000 hội viên (NFR-01) |
| **Constraints** | Audit log **append-only**; sức chứa cơ sở cố định = 50 |
| **Scale/Scope** | 2 endpoint, 1 bảng ghi + 1 read-model, 15 chỉ số |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| AUDIT-01 — mọi hành động mutating quan trọng phải ghi audit | ✅ PASS | **36 action** trên 15 service — xem [danh sách đầy đủ](#71-các-action-audit-đang-được-ghi). Ngoại lệ có chủ ý duy nhất: login/logout/refresh (spec 001, xem `001-auth-rbac/plan.md` §8) |
| SAFE-01 — audit log append-only, không sửa/xoá qua API | ✅ PASS | chỉ có endpoint `GET /audit-logs`; `IAuditService` chỉ có hàm ghi (NFR-03) |
| SAFE-02 — không ghi mật khẩu/token/PII đầy đủ vào metadata | ✅ PASS | metadata chỉ chứa id + trường nghiệp vụ (FR-AUD-03) |
| GBL-05 — `UserId` lấy từ JWT claim | ✅ PASS | `AuditService` đọc qua `IHttpContextAccessor` |
| ARCH-02 — wrapper `ApiResponse<T>` / `PagedResult<T>` | ✅ PASS | cả 2 endpoint |
| GBL-01 — mốc thời gian nghiệp vụ theo giờ VN | ✅ PASS | `AppClock` cho `revenueByMonth`, `peakHour`, `checkinsByDay` |
| BIZ-16 — số liệu từ dữ liệu thật, không mock | ✅ PASS | mọi chỉ số là aggregate query |

## 4. Project Structure

```text
backend/GymMaster.API/Features/Dashboard/
├── DashboardController.cs      # route "api/v1/dashboard" — [Authorize(Roles="admin")]
├── IDashboardService.cs · DashboardService.cs   # ★ 15 chỉ số aggregate
├── DashboardDtos.cs            # DashboardSummaryResponse, AuditLogResponse
├── AuditLogsController.cs      # route "api/v1/audit-logs" — tra cứu
├── IAuditService.cs            # ★ hạ tầng dùng chung — 7 slice khác inject
└── AuditService.cs             # ghi audit, lấy UserId từ JWT

backend/GymMaster.API/Entities/
└── AuditLog.cs                 # UserId nullable, Action(100), Entity(60), EntityId, Metadata JSON, CreatedAt

backend/GymMaster.API/Program.cs   # AddScoped<IAuditService, AuditService>() — đăng ký đầu tiên
tests/GymMaster.Api.Tests/DashboardServiceTests.cs
```

**Structure Decision**: `IAuditService` đặt trong slice `Dashboard/` thay vì `Infrastructure/`. Đây là **lựa chọn có tranh cãi** — về bản chất nó là cross-cutting concern nên đúng ra thuộc `Infrastructure/`. Giữ ở đây vì bảng `audit_logs` chỉ có một nơi tiêu thụ (màn tra cứu của Admin), nên contract ghi và contract đọc tiến hoá cùng nhau. Đánh đổi: 7 slice khác `using GymMaster.API.Features.Dashboard` chỉ để ghi log.

## 5. Design Decisions

> **Chi tiết hoá ADR dự án**: [D-10](../../../06-Management/decision-log.md) (audit log cho action mutating) → D-806, D-807, D-809 · [D-11](../../../06-Management/decision-log.md) (API contract).
> **Liên quan**: [D-14](../../../06-Management/decision-log.md) đặt coverage ≥ 80% business logic — hiện **chưa đạt**, xem [BACKLOG](../BACKLOG.md) B-03…B-07.

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-801 | Dashboard **tính lúc gọi**, không cache, không bảng tổng hợp | Số liệu luôn khớp DB; không có nguy cơ lệch cache — đúng yêu cầu "dữ liệu thật, không mock" | Mỗi lần mở là hàng loạt aggregate query → phải giữ trong 2s (NFR-01) |
| D-802 | Kỳ trống → trả **0**, không trả lỗi | Phòng gym mới mở/ngày vắng là chuyện bình thường, không phải lỗi hệ thống | Không phân biệt được "không có dữ liệu" với "có dữ liệu bằng 0" |
| D-803 | Mặc định kỳ = **tháng này theo giờ VN** | Chủ phòng gym quan tâm doanh thu tháng hiện tại | Container chạy UTC mà không qua `AppClock` sẽ lệch kỳ ở đầu/cuối tháng |
| D-804 | Sức chứa cơ sở **hardcode = 50** để tính `facilityLoadPercent` | Đồ án một cơ sở; đưa vào config sẽ thừa | Nhiều cơ sở hoặc đổi sức chứa phải sửa code |
| D-805 | Giờ cao điểm tính trên **30 ngày gần nhất**, gom theo giờ VN | Đủ mẫu để ổn định, đủ gần để phản ánh hiện trạng | Phòng gym mới mở < 30 ngày sẽ ra kết quả nhiễu |
| D-806 | `AuditService` lấy `UserId` từ `IHttpContextAccessor`, service gọi không phải truyền vào | Không thể "quên" truyền hoặc truyền sai người thực hiện | Phụ thuộc HttpContext → gọi từ background job sẽ ra `UserId = null` |
| D-807 | `AuditLog.UserId` **nullable** | Có hành động do hệ thống thực hiện (VNPay IPN — spec 010, member tự check-in) | Phải phân biệt "hệ thống làm" với "thiếu dữ liệu" khi đọc |
| D-808 | Metadata là **JSON tự do** trong `NVARCHAR(MAX)` | Mỗi loại hành động cần trường khác nhau; schema cứng sẽ chật | Không truy vấn/lọc theo trường trong metadata được (chỉ `search` chuỗi) |
| D-809 | Audit **append-only** — không có API sửa/xoá | Audit sửa được thì không còn là audit (NFR-03) | Ghi nhầm không xoá được; bảng chỉ lớn dần |
| D-810 | `revenueByMonth` nhãn dạng `"T6"` (tiếng Việt) tính sẵn ở backend | FE chỉ hiển thị, không phải tự map tháng | Muốn đa ngôn ngữ phải sửa backend |

## 6. Data Flow

```text
GET /api/v1/dashboard/summary?from=&to=          [Admin]
  → DashboardController → DashboardService.GetSummaryAsync
      ├─ from > to                             → 422 INVALID_RANGE
      ├─ không truyền → mặc định tháng này (giờ VN)          (D-803)
      ├─ revenue                ← Σ payments Status=Paid trong kỳ
      ├─ activeCount/expiredCount ← memberships (đã lazy-expire — spec 003)
      ├─ checkinsByDay          ← check_ins gom theo ngày VN  (spec 004)
      ├─ pendingPaymentAmount/Count ← memberships PendingPayment
      ├─ revenueByMonth         ← 6 tháng gần nhất, nhãn "T6" (D-810)
      ├─ recentlyExpired        ← top 10 hết hạn gần đây (initials, tên, gói, ngày)
      ├─ facilityLoadPercent    ← check-in hôm nay / **50**   (D-804)
      ├─ ptSessionPercent / generalAreaPercent  ← trainer_assignments (spec 005)
      ├─ previousMonthRevenue · newMembershipsThisMonth
      └─ peakHourStart/End      ← 30 ngày gần nhất, gom theo giờ VN (D-805)
  → DashboardSummaryResponse   (mọi chỉ số = 0 nếu kỳ trống — D-802)

Ghi audit (chiều ngược — 7 slice gọi vào):
  UserService · MemberService (002)
  MembershipService · PaymentService (003)
  AssignmentService (005)
  FoodScanService (009)
  VnPayService (010)
      → IAuditService.LogAsync(action, entity, entityId, metadata)
          ├─ UserId ← JWT claim qua IHttpContextAccessor  (D-806, null nếu hệ thống — D-807)
          └─ INSERT audit_logs { Action, Entity, EntityId, Metadata JSON, CreatedAt }

GET /api/v1/audit-logs?userId=&action=&from=&to=&search=&page=   [Admin]
  → lọc → sắp xếp thời gian **giảm dần** → JOIN users lấy userDisplayName
  → PagedResult<AuditLogResponse>
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-DASH-01 | `Features/Dashboard/DashboardService.cs` — revenue, active/expired, check-in hôm nay, pending |
| FR-DASH-02 | `DashboardService` — lọc `from`/`to`, mặc định tháng này (giờ VN) |
| FR-DASH-03 | `DashboardService` — 9 chỉ số phái sinh (revenueByMonth, recentlyExpired, facilityLoad, peakHour…) |
| FR-DASH-04 | `DashboardService` — kỳ trống trả 0 |
| FR-DASH-05 | `[Authorize(Roles="admin")]` trên `DashboardController.cs` |
| FR-AUD-01 | `Features/Dashboard/AuditService.cs` + lời gọi từ 7 slice |
| FR-AUD-02 | `AuditLogsController.cs` — lọc + `PagedResult`, kèm `userDisplayName` |
| FR-AUD-03 | Quy ước dựng metadata trong từng service gọi (chỉ id + trường nghiệp vụ) |

### 7.1. Các action audit đang được ghi

**36 action / 15 service:**

| Slice | Service | Action |
|---|---|---|
| 001 Auth | `AccountService` | `UPDATE_ACCOUNT` · `UPDATE_PERSONAL_PROFILE` · `UPDATE_AVATAR` · `CREATE_STAFF_PROFILE` |
| 002 Members | `UserService` | `CREATE_USER` · `UPDATE_USER` · `DELETE_USER` · `UPDATE_USER_STATUS` · `RESET_PASSWORD` |
| | `MemberService` | `CREATE_MEMBER` · `UPDATE_MEMBER` · `DELETE_MEMBER` |
| | `TrainerService` | `CREATE_TRAINER` · `UPDATE_TRAINER` |
| 003 Billing | `MembershipPackageService` | `CREATE_PACKAGE` · `UPDATE_PACKAGE` |
| | `MembershipService` | `SELL_MEMBERSHIP` · `CONFIRM_PAYMENT` · `RENEW_MEMBERSHIP` · `REQUEST_RENEWAL` · `CANCEL_MEMBERSHIP` |
| 004 Check-in | `CheckInService` | `CREATE_CHECKIN` *(quầy + PT check-in hộ)* |
| 005 PT | `AssignmentService` | `ASSIGN_PT` |
| | `WorkoutPlanService` | `CREATE_WORKOUT_PLAN` · `UPDATE_WORKOUT_PLAN` · `DELETE_WORKOUT_PLAN` |
| | `TrainerNoteService` | `CREATE_TRAINER_NOTE` · `UPDATE_TRAINER_NOTE` · `DELETE_TRAINER_NOTE` |
| 006 Progress | `ProgressService` | `CREATE_PROGRESS` · `UPDATE_PROGRESS` |
| 007 Nutrition | `NutritionService` | `CREATE_MEAL_LOG` · `SET_CALORIE_TARGET` |
| | `FoodItemService` | `CREATE_FOOD` |
| 009 Food AI | `FoodScanService` | `CONFIRM_AI_FOOD` |
| 010 VNPay | `VnPayService` | `VNPAY_PAYMENT` |

**Không ghi audit — có lý do:**

| Service | Vì sao |
|---|---|
| `AuthService` | login/logout/refresh cố ý không ghi để `audit_logs` không bị nhiễu (spec 001 §8) |
| `PaymentService` | `CONFIRM_PAYMENT` đã được `MembershipService` ghi — tránh ghi trùng một giao dịch |
| `DashboardService` | chỉ đọc, không mutating |
| `GeminiService` | tầng hạ tầng; `FoodScanService` ghi `CONFIRM_AI_FOOD` thay |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| `IAuditService` nằm trong slice `Dashboard/` thay vì `Infrastructure/` | Contract ghi và contract đọc audit tiến hoá cùng nhau; chỉ có một nơi tiêu thụ | Để ở `Infrastructure/` → đúng lý thuyết hơn nhưng phải đồng bộ 2 nơi mỗi lần đổi metadata. **Nếu sau này có nơi tiêu thụ thứ hai thì nên chuyển** |
| Dashboard là slice phụ thuộc **nhiều nhất** hệ thống | Bản chất là màn tổng hợp; coupling chỉ ở tầng đọc | FE tự gọi và tự cộng → logic nghiệp vụ (giờ cao điểm, tải cơ sở) rơi xuống FE |
| Không cache, tính lại mỗi lần gọi (D-801) | Yêu cầu "số liệu thật, khớp DB" | Cache 5 phút → Admin bán gói xong không thấy doanh thu đổi, mất niềm tin vào số liệu |
| Sức chứa hardcode 50 (D-804) | Đồ án một cơ sở duy nhất | Đưa vào config/DB → thêm hạ tầng cho đúng một con số |
| `AuditLog.Metadata` không truy vấn được theo trường (D-808) | Mỗi hành động có hình dạng dữ liệu khác nhau | Schema cứng → không đủ chỗ cho hành động mới; bảng phụ key-value → phức tạp gấp bội |
| `AuthService` không ghi audit login/logout/refresh | Ghi mọi lần đăng nhập làm phình `audit_logs` và nhiễu dashboard; audit tập trung vào hành động **thay đổi dữ liệu** | Ghi hết → bảng audit mất giá trị tra cứu (xem `001-auth-rbac/plan.md` §8) |
