# Implementation Plan: Progress Tracking & Member 360° Profile

**Feature Branch**: `006-progress-tracking` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)
**Status**: `Implemented`
**Input**: `docs/03-Interface-Specs/feature-specs/006-progress-tracking/spec.md`

---

## 1. Summary

Hai nghiệp vụ khác bản chất nhưng dùng chung một service:

1. **Progress log** — ghi/đọc số đo cơ thể. Bảng đơn giản, chỉ có validate khoảng giá trị + quy tắc *1 ngày = 1 bản ghi*.
2. **Member 360°** — **read-model tổng hợp**, gom dữ liệu từ **6 nguồn thuộc 5 spec khác nhau** (002 hồ sơ, 003 membership+payment, 004 check-in, 005 PT, 006 tiến độ, 007 dinh dưỡng) thành một response duy nhất.

360° là điểm tích hợp lớn nhất hệ thống. Quyết định kiến trúc quan trọng nhất: **một implementation duy nhất** (`ProgressService.GetProfile360Async`) phục vụ cả 3 route (`/members/{id}/profile-360`, `/members/{id}/360`, `/members/me/profile-360`) — nếu tách sẽ có 3 phiên bản dữ liệu lệch nhau.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | EF Core 10 (SqlServer) |
| **Storage** | SQL Server — `progress_logs` (index `(MemberId, MeasuredAt)`); 360° không có bảng riêng |
| **Kiểu số đo** | `DECIMAL(5,2)` cho kg / % / cm |
| **Đồng hồ** | So sánh "ngày đo không ở tương lai" theo **giờ VN** (`Common/AppClock.cs`) |
| **Testing** | xUnit — `tests/GymMaster.Api.Tests/ProgressServiceTests.cs` |
| **Target Platform** | Cloud Run + Cloud SQL |
| **Performance Goals** | Tổng hợp 360° < 1.5s (NFR-01) |
| **Constraints** | Ghi chú ≤ 500 ký tự; **chưa hỗ trợ ảnh tiến độ** |
| **Scale/Scope** | 5 endpoint, 1 bảng ghi + 1 read-model |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| GBL-05 — identity từ JWT claim | ✅ PASS | ownership kiểm bằng `CurrentUserId` |
| GBL-02 — không lặp business rule | ✅ PASS | dùng lại `MembershipLifecycle` (003) và cửa quyền PT (005); 3 route 360 gọi **một** hàm |
| GBL-04 — kiểm quyền ở Service layer | ✅ PASS | `ProgressService` |
| GBL-01 — ngày nghiệp vụ theo giờ VN | ✅ PASS | `AppClock` cho quy tắc "1 ngày = 1 bản ghi" và chặn ngày tương lai |
| ARCH-02 — wrapper `ApiResponse<T>` | ✅ PASS | mọi action |
| AUDIT-01 — hành động quan trọng ghi AuditLog | ⚠️ N/A | `progress_logs` có `CreatedByUserId`, tự nó là nhật ký |
| DATA-01 — không xoá cứng | ✅ N/A | feature không có endpoint xoá |

## 4. Project Structure

```text
backend/GymMaster.API/Features/Training/
├── MemberProgressController.cs   # route "api/v1/members" — /progress, /profile-360, /360, /me/profile-360
├── IProgressService.cs
├── ProgressService.cs            # ★ ghi/đọc tiến độ + tổng hợp 360°
└── ProgressDtos.cs               # ProgressLogResponse, Profile360Response, Membership360

backend/GymMaster.API/Entities/
└── ProgressLog.cs                # WeightKg, BodyFatPercent, ChestCm, WaistCm, HipCm, Note(500)

tests/GymMaster.Api.Tests/ProgressServiceTests.cs
```

**Structure Decision**: đặt trong slice `Features/Training/` (không tạo slice `Progress/` riêng) vì tiến độ luyện tập là một phần công việc của PT — PT ghi số đo cho hội viên mình kèm, và dùng chung đúng cửa quyền assignment của spec 005. Tách slice sẽ phải nhân đôi logic kiểm phân công.

## 5. Design Decisions

> **Kế thừa ADR dự án**: [D-12](../../../06-Management/decision-log.md) (1 PT active → field `assignedPT` luôn là 0 hoặc 1 phần tử) · [D-13](../../../06-Management/decision-log.md) (`PendingPayment` → quy tắc suy `currentMembership` ở D-607).
> **Chưa có ADR dự án tương ứng, đáng cân nhắc nâng lên**: **D-601** (1 ngày = 1 bản ghi tiến độ — ảnh hưởng cách FE vẽ biểu đồ).

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-601 | **1 ngày = 1 bản ghi** — ghi lại cùng ngày thì **đè** (UPDATE), không tạo điểm mới | Biểu đồ tiến độ cần 1 điểm/ngày; cân 3 lần/ngày sẽ tạo răng cưa vô nghĩa | Mất lịch sử đo nhiều lần trong ngày; trả 200 thay vì 201 làm client phải xử lý 2 mã |
| D-602 | Validate khoảng cứng: Weight 20–300kg, BodyFat 0–70%, Chest/Waist/Hip 30–200cm | Chặn lỗi nhập liệu (gõ 5kg, 700cm) ngay tại nguồn | Vận động viên ngoài khoảng sẽ bị chặn oan |
| D-603 | Chặn `measuredAt` ở **tương lai** (so theo giờ VN) | Số đo tương lai làm hỏng timeline và mọi phép so sánh | Người dùng ở múi giờ khác nhập giờ địa phương có thể bị từ chối |
| D-604 | 360° là **read-model tính lúc gọi**, không có bảng cache | Dữ liệu luôn tươi; không có job đồng bộ, không có nguy cơ lệch cache | Mỗi lần gọi là ~6 query → phải giữ trong ngân sách 1.5s (NFR-01) |
| D-605 | **Một implementation duy nhất** cho 3 route 360 | 3 bản sao sẽ lệch nhau ngay khi thêm field | Route alias khiến API surface hơi dư thừa |
| D-606 | Đồng bộ trạng thái membership (lazy expire) **trước** khi trả 360° | Nếu không, 360° có thể hiện gói `Active` đã quá hạn | Một endpoint đọc lại ghi DB — lệch nguyên tắc "GET không side-effect" |
| D-607 | `currentMembership` = Active còn hạn (EndDate lớn nhất) → nếu không có thì Pending mới nhất → **null** | Không bao giờ hiển thị đơn `Cancelled`/`Expired` như "gói hiện tại" (nguồn gây hiểu nhầm cho FE) | FE phải xử lý `null` ở mọi chỗ dùng `currentMembership` |
| D-608 | `nutritionSummary` **tính sẵn** ở backend, FE chỉ hiển thị | Công thức calo/macro chỉ tồn tại một nơi (spec 007) | 360° phụ thuộc biên dịch vào slice `Nutrition` |
| D-609 | Ép `Kind=Utc` cho `checkInAt` / `assignedAt` khi trả ra | FE mới quy đổi đúng giờ địa phương (NFR-02) | Phải nhớ ép ở từng field, dễ sót khi thêm field mới |
| D-610 | Giới hạn `recentCheckIns` **tối đa 5** | 360° là màn tổng quan; lịch sử đầy đủ đã có endpoint riêng của spec 004 | Muốn xem thêm phải gọi API khác |

## 6. Data Flow

```text
Ghi tiến độ:
  POST /members/{id}/progress {measuredAt?, weightKg?, bodyFatPct?, chestCm?, waistCm?, hipCm?, note?}
    → ProgressService.CreateAsync
        ├─ ownership: Member(self) | PT(assigned — cửa quyền spec 005) | Admin/Staff  → 403
        ├─ không có chỉ số nào                      → 422 INVALID_MEASUREMENT
        ├─ chỉ số ngoài khoảng / note > 500 ký tự   → 422 INVALID_MEASUREMENT
        ├─ measuredAt ở tương lai (giờ VN)          → 422 INVALID_MEASUREMENT
        ├─ đã có bản ghi cùng ngày?  → UPDATE (200)      (D-601)
        └─ chưa có                   → INSERT (201)

Đọc timeline:
  GET /members/{id}/progress  → sắp xếp **tăng dần** theo measuredAt (để FE vẽ biểu đồ)

Member 360° (một hàm, 3 route):
  GET /members/{id}/profile-360   (canonical)
  GET /members/{id}/360           (alias)
  GET /members/me/profile-360     (self)
    → ProgressService.GetProfile360Async
        ├─ kiểm quyền (self | assigned PT | Admin | Staff)     → 403
        ├─ MembershipLifecycle.ExpireIfPastDue(...)            (D-606, spec 003)
        ├─ member            ← member_profiles + users     (spec 002)
        ├─ currentMembership ← Active còn hạn → Pending mới nhất → null   (D-607, spec 003)
        │      paymentStatus ← suy từ bảng payments (Paid/Pending)
        ├─ membershipHistory ← toàn bộ, sắp xếp mới→cũ      (spec 003)
        ├─ recentCheckIns    ← tối đa 5, ép Kind=Utc        (spec 004)
        ├─ progressTimeline  ← progress_logs                (spec 006)
        ├─ nutritionSummary  ← tính sẵn, có thể null        (spec 007)
        └─ assignedPT        ← assignment Active, có thể null (spec 005)
    → Profile360Response
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-PROG-01 | `ProgressService.CreateAsync` — validate khoảng + quy tắc đè cùng ngày |
| FR-PROG-02 | `ProgressService` — cửa quyền assignment (dùng lại spec 005) |
| FR-PROG-03 | `ProgressService` — sắp xếp tăng dần theo `MeasuredAt` |
| FR-360-01 | `ProgressService.GetProfile360Async` — gom 6 nguồn |
| FR-360-02 | Kiểm quyền trong `GetProfile360Async` → 403 |
| FR-360-03 | `GetProfile360Async` — quy tắc suy `currentMembership` |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| 360° phụ thuộc **5 spec khác** — coupling cao nhất hệ thống | Bản chất nghiệp vụ là màn hình tổng hợp; coupling nằm ở tầng đọc, không lan sang tầng ghi | Cho FE gọi 6 API rồi tự ghép → 6 round-trip + logic nghiệp vụ (`currentMembership`, `nutritionSummary`) rơi xuống FE |
| GET 360° có side-effect (D-606) | Không lazy-expire thì dữ liệu hiển thị sai trạng thái | Bỏ đồng bộ → 360° hiện gói hết hạn là `Active`; dùng job nền → Cloud Run scale-to-zero không chạy được |
| 3 route cho cùng một dữ liệu (D-605) | Route cũ đã được FE dùng, giữ lại để không phá client | Xoá alias → phải sửa và deploy lại FE đang chạy |
| `ProgressService` gánh cả progress lẫn 360° | 360° cần chính dữ liệu và chính cửa quyền của progress | Tách `Profile360Service` → phải inject lại toàn bộ phụ thuộc, lợi ích thấp |
| **Chưa hỗ trợ ảnh tiến độ** | Đã ghi rõ Out of Scope trong spec; cần blob storage + policy riêng | — (ngoài phạm vi bản này) |
