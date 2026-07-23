# Implementation Plan: Membership Packages, Sell, Renew & Payment

**Feature Branch**: `003-membership-billing` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)
**Status**: `Implemented` — **as-built plan** (đồng bộ ngược từ code đang chạy)
**Input**: `docs/03-Interface-Specs/feature-specs/003-membership-billing/spec.md`

> Tài liệu mô tả kiến trúc **đã triển khai**. Thanh toán online VNPay là phần mở rộng ở [spec 010](../010-online-payment-vnpay/plan.md), không thay thế luồng thủ công ở đây.

---

## 1. Summary

Nguồn doanh thu chính của hệ thống: quản lý gói tập mẫu → bán gói → ghi nhận thanh toán → gia hạn. Hai quyết định kiến trúc chi phối toàn bộ feature:

1. **Tách "bán" khỏi "thanh toán"** bằng trạng thái trung gian `PendingPayment` (D-13). Bán gói tạo đơn chờ; chỉ khi có payment `Paid` mới `Active`. Nhờ vậy VNPay (spec 010) chỉ cần cắm vào đúng bước xác nhận, không phải viết lại luồng bán.
2. **Một nguồn sự thật cho vòng đời** — `Features/Billing/MembershipLifecycle.cs`. Trước đây logic "gói còn hiệu lực?" bị copy 3 bản ở `MembershipService`, `ProgressService`, `VnPayService` và lệch nhau; nay gom về một static class, mọi đường đọc/ghi phán xét giống hệt nhau.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | EF Core 10 (SqlServer) |
| **Storage** | SQL Server — `membership_packages`, `memberships`, `payments` |
| **Kiểu tiền** | `DECIMAL(12,2)` — **không dùng float** (NFR-02) |
| **Đồng hồ** | `Common/AppClock.cs` — "hôm nay" theo **giờ VN (GMT+7)** (NFR-04) |
| **Testing** | xUnit — `MembershipServiceTests` · `MembershipPackageServiceTests` · `PaymentServiceTests` (78 test, PR #10) |
| **Target Platform** | Cloud Run + Cloud SQL |
| **Performance Goals** | Bán gói < 500ms (NFR-01) |
| **Constraints** | Bất biến **tối đa 1 Membership Active/member**; đơn Pending TTL 30 phút |
| **Scale/Scope** | 13 endpoint, 5 controller, 3 bảng |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| BIZ-05 — tiền dùng DECIMAL, không float | ✅ PASS | `Entities/Payment.cs`, `MembershipPackage.cs` |
| BIZ-06 — mọi giao dịch có người thực hiện | ✅ PASS | `CreatedByUserId` lấy từ JWT claim |
| AUDIT-01 — hành động quan trọng ghi AuditLog | ✅ PASS | `IAuditService` trong `MembershipService`, `PaymentService` |
| ARCH-02 — wrapper `ApiResponse<T>` / `PagedResult<T>` | ✅ PASS | mọi action |
| GBL-02 — không lặp business rule ở nhiều service | ✅ PASS | `MembershipLifecycle.cs` (đã gom 3 bản sao về 1) |
| GBL-01 — nghiệp vụ theo ngày dùng giờ VN | ✅ PASS | `AppClock` |
| DATA-01 — không xoá cứng | ✅ PASS | huỷ = đổi `Status` sang `Cancelled`, không DELETE |

## 4. Project Structure

### Source Code (thực tế trong repo)

```text
backend/GymMaster.API/Features/Billing/
├── MembershipLifecycle.cs           # ★ Luật vòng đời dùng chung (internal static)
├── PackagesController.cs            # route "api/v1/packages"
├── IMembershipPackageService.cs · MembershipPackageService.cs
├── PackageDtos.cs
├── MembershipsController.cs         # route "api/v1/memberships" — sell/payment/renew/cancel
├── MemberMembershipsController.cs   # route "api/v1/members" — /{id}/memberships
├── IMembershipService.cs · MembershipService.cs
├── MembershipDtos.cs                # MembershipResponse, ConfirmPaymentResult
├── PaymentsController.cs            # route "api/v1/payments" — list + summary
├── MemberPaymentsController.cs      # route "api/v1/members" — /{id}/payments
├── IPaymentService.cs · PaymentService.cs
├── PaymentDtos.cs                   # PaymentHistoryResponse, PaymentSummaryResponse
└── (VnPay*.cs — thuộc spec 010)

backend/GymMaster.API/Entities/
├── MembershipPackage.cs             # + SupportsPT BIT
├── Membership.cs · MembershipEnums.cs   # MembershipStatus{PendingPayment,Active,Expired,Cancelled}
└── Payment.cs                       # PaymentMethod{Cash,Transfer,Card}, PaymentStatus{Pending,Paid,Refunded}

backend/GymMaster.API/Common/
└── AppClock.cs                      # TodayVn() — GMT+7

database/
└── 008_package_supports_pt.sql      # thêm cột SupportsPT

tests/GymMaster.Api.Tests/
├── MembershipServiceTests.cs
├── MembershipPackageServiceTests.cs
└── PaymentServiceTests.cs           # 25 test, gồm gom doanh thu theo ngày VN
```

**Structure Decision**: một slice `Billing/` nhưng **5 controller** thay vì 1. Lý do: cùng một nghiệp vụ nhưng lộ ra dưới hai gốc route khác nhau (`/memberships`, `/payments` cho nghiệp vụ; `/members/{id}/…` cho góc nhìn hội viên). ASP.NET Core chỉ cho một `[Route]` gốc mỗi controller, nên phải tách — đây là ràng buộc framework, không phải lựa chọn thiết kế.

## 5. Design Decisions

> **Chi tiết hoá ADR dự án**: [D-13](../../../06-Management/decision-log.md) (trạng thái `PendingPayment`) · [D-17](../../../06-Management/decision-log.md) (SQL Server) · [D-19](../../../06-Management/decision-log.md) (Cloud Run — lý do chọn lazy expire ở D-204).
> **Chưa có ADR dự án tương ứng, đáng cân nhắc nâng lên**: **D-202** (`MembershipLifecycle` — 7 feature phụ thuộc) và **D-209** (ngày nghiệp vụ theo giờ VN — áp cho 5 feature).

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-201 | Trạng thái trung gian `PendingPayment` (D-13) | Tách bán khỏi thu tiền; là điểm cắm sẵn cho VNPay (spec 010) mà không sửa luồng bán | Sinh đơn rác nếu khách bỏ ngang → phải có TTL (D-203) |
| D-202 | `MembershipLifecycle` là **static class dùng chung** | 3 bản sao logic ở 3 service từng lệch nhau, gây bug "gói đã hết hạn vẫn check-in được" | Static khó mock trong unit test; bù lại logic thuần, không I/O |
| D-203 | Đơn Pending quá **30 phút** tự `Cancelled` | Đơn bỏ dở chặn mất bất biến "1 Active/member" và làm bẩn báo cáo | Khách thanh toán chậm > 30 phút phải tạo đơn lại |
| D-204 | **Lazy expire** khi truy vấn, không có background job | Cloud Run scale-to-zero — không có process chạy nền đáng tin | Trạng thái chỉ đúng khi có người đọc; báo cáo trực tiếp trên DB có thể thấy `Active` đã quá hạn |
| D-205 | Gia hạn **kéo dài tại chỗ**, không tạo bản ghi mới | Giữ đúng bất biến 1 Active/member; lịch sử thanh toán vẫn đủ để truy vết | Mất lịch sử "từng mua gói nào" ở tầng membership — phải xem qua `payments` |
| D-206 | Nối hạn: kích hoạt đơn mới khi còn gói Active → cộng dồn `EndDate` + `Cancelled` gói cũ | Khách mua trước khi hết hạn không bị mất ngày còn lại | Gói cũ hiển thị `Cancelled` dễ gây hiểu nhầm là bị huỷ |
| D-207 | Quyền dùng PT **suy ra động** từ `Package.SupportsPT`, không lưu cờ trên member | Hết hạn gói là mất quyền PT ngay, không cần job đồng bộ | Mỗi lần gác quyền PT phải JOIN sang `membership_packages` |
| D-208 | Chặn gia hạn lệch loại PT → 422 `PACKAGE_PT_MISMATCH` | Đổi gói thường ↔ gói PT giữa chừng làm rối phân công PT (spec 005) | Khách muốn nâng cấp phải chờ hết hạn |
| D-209 | Ngày nghiệp vụ theo **giờ VN** qua `AppClock` | `DateTime.UtcNow` khiến gói hết hạn lệch 1 ngày lúc rạng sáng | Mọi so sánh ngày phải nhớ gọi `AppClock`, không dùng `DateTime.Today` |
| D-210 | Bán gói cho member đã có đơn Pending → **tái dùng** đơn đó | Staff bấm nhầm 2 lần không sinh 2 đơn | Nếu đổi ý sang gói khác phải huỷ đơn cũ trước |

## 6. Data Flow

```text
Bán gói (thủ công):
  POST /memberships/sell {memberId, packageId, startDate?}
    → MembershipService.SellAsync
        ├─ MembershipLifecycle.ExpireIfPastDue / ExpireStalePending  (đồng bộ trạng thái trước)
        ├─ đã có Active còn hạn?      → 409 ALREADY_HAS_ACTIVE
        ├─ đã có đơn Pending?         → tái dùng (FR-MS-01)
        ├─ Package.IsActive == false  → 422 PACKAGE_INACTIVE
        └─ INSERT memberships { Status=PendingPayment, EndDate = StartDate + DurationDays }
    → 201

Ghi nhận thanh toán:
  POST /memberships/{id}/payment {amount, method}
    → Membership đã Active?  → 409 DUPLICATE_PAYMENT
    → Amount < Price?        → 422 INSUFFICIENT_AMOUNT
    → INSERT/UPDATE payments (Status=Paid, PaidAt)
    → Membership → Active   (nếu member còn gói Active khác: nối hạn + Cancelled gói cũ — D-206)
    → huỷ các đơn Pending anh em
    → AuditLog "CONFIRM_PAYMENT"

Gia hạn:
  POST /memberships/{id}/renew {packageId, method}
    → SupportsPT lệch gói hiện tại? → 422 PACKAGE_PT_MISMATCH
    → EndDate = (còn hạn ? EndDate : hôm nay VN) + DurationDays   → Active
    → ghi Payment Paid ngay (không qua Pending)

Member xin gia hạn:
  POST /memberships/renewal-request {packageId}  → chỉ tạo đơn PendingPayment (ADR-05)
                                                 → Staff/Admin/VNPay xác nhận mới Active

Gác quyền PT (dùng ở spec 005):
  tồn tại Membership Active AND EndDate ≥ hôm nay(VN) AND Package.SupportsPT ⇒ được dùng PT
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-PKG-01, 02, 03 | `MembershipPackageService.cs` |
| FR-PKG-04 | `MembershipLifecycle.IsActiveOn` + `Package.SupportsPT` (dùng ở spec 005) |
| FR-MS-01 | `MembershipService.SellAsync` |
| FR-MS-02 | `MembershipService` (ConfirmPayment) + `PaymentService.cs` |
| FR-MS-03, 03a | `MembershipService.RenewAsync` |
| FR-MS-04 | `MembershipService.CreateRenewalRequestAsync` |
| FR-MS-07 | **`Features/Billing/MembershipLifecycle.cs`** — `IsActiveOn`, `ExpireIfPastDue`, `ExpireStalePending` |
| FR-MS-08 | `MembershipService.CancelAsync` (kiểm tra ownership cho Member) |
| FR-MS-09 | `MemberMembershipsController.cs` |
| FR-MS-05 | Gác ở spec 004 (`CheckInService` gọi `MembershipLifecycle`) |
| FR-MS-06 | `IAuditService` ở mọi action mutating |
| FR-PAY-01 | `PaymentService` — 409 `DUPLICATE_PAYMENT` |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| 5 controller cho một slice | Ràng buộc `[Route]` một gốc/controller của ASP.NET Core | Gộp 1 controller → phải dùng route tuyệt đối rải rác, khó đọc hơn |
| Lazy expire thay vì job định kỳ (D-204) | Cloud Run scale-to-zero, không có worker thường trú | Hosted service → container phải chạy 24/7, tăng chi phí ngoài phạm vi đồ án |
| Gia hạn không tạo bản ghi membership mới (D-205) | Giữ bất biến 1 Active/member đơn giản hơn nhiều | Mỗi lần gia hạn 1 bản ghi → phải xử lý chồng lấn khoảng ngày, phức tạp gấp bội |
| `MembershipLifecycle` là static, khó mock | Logic thuần không I/O nên test trực tiếp được | Interface + DI → thêm 2 file cho một class 40 dòng |
| `MembershipLifecycle` là static, khó mock | Logic thuần không I/O nên test trực tiếp được | Interface + DI → thêm 2 file cho một class 40 dòng |
