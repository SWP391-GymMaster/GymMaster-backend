# Implementation Plan: Online Payment via VNPay (Sandbox)

**Feature Branch**: `010-online-payment-vnpay` | **Spec**: [spec.md](spec.md)
**Status**: `Implemented`
**Input**: `docs/03-Interface-Specs/feature-specs/010-online-payment-vnpay/spec.md`

> **Bổ sung** cho [spec 003](../003-membership-billing/plan.md), **không thay thế** luồng thanh toán thủ công (vẫn giữ làm fallback). Override ADR-03.

---

## 1. Summary

Tích hợp cổng VNPay chế độ **sandbox** để demo trọn vòng đời: tạo URL thanh toán đã ký → member trả bằng thẻ test → VNPay gọi **IPN** (server-to-server) → hệ thống tự kích hoạt `Membership`.

Feature này khả thi ở mức **thêm 3 endpoint, không đổi schema DB** — nhờ quyết định kiến trúc từ spec 003: trạng thái trung gian `PendingPayment` đã tách sẵn "bán" khỏi "thu tiền", nên VNPay chỉ cần cắm vào đúng bước xác nhận.

Đây là feature nhạy cảm nhất về bảo mật — sai là sai doanh thu hoặc thủng lỗ hổng giả mạo callback. Ba lớp phòng vệ bắt buộc, áp cho **cả IPN lẫn Return**:

1. **Verify chữ ký HMAC-SHA512** — callback không ký đúng thì bỏ, không kích hoạt gì.
2. **Đối chiếu số tiền** — `vnp_Amount` phải khớp `Payment.Amount`; giá **luôn lấy từ `Package` ở server**, không bao giờ nhận từ client.
3. **Idempotent** — IPN gửi lại nhiều lần không được double-activation.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | Không thêm package — `Infrastructure/VnPayLibrary.cs` tự ký/verify bằng `System.Security.Cryptography` |
| **Chữ ký** | **HMAC-SHA512** với `HashSecret` |
| **Storage** | **Không đổi schema** — tái dùng `payments` / `memberships` của spec 003 |
| **Cấu hình** | `Options/VnPayOptions.cs` (section `VnPay`): `TmnCode`, `HashSecret` *(User Secrets)*, `BaseUrl`, `ReturnUrl`, `Version`, `Command`, `CurrCode`, `Locale` |
| **Đồng hồ** | `vnp_CreateDate`/`vnp_ExpireDate` theo **giờ VN (GMT+7)**, link hết hạn **15 phút** |
| **Testing** | xUnit + EF Core InMemory — `tests/GymMaster.Api.Tests/VnPayServiceTests.cs` (6 test) |
| **Target Platform** | Cloud Run (IPN cần URL công khai; chạy local phải dùng tunnel) |
| **Constraints** | Sandbox — không tiền thật, không hợp đồng merchant |
| **Scale/Scope** | 3 endpoint, 0 bảng mới |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| SEC-05 — secret chỉ ở server, không hard-code | ✅ PASS | `TmnCode`/`HashSecret` từ User Secrets (FR-VNP-09) |
| SAFE-04 — không tin dữ liệu từ client cho quyết định tiền bạc | ✅ PASS | `vnp_Amount = Package.Price × 100`, giá lấy từ server (FR-VNP-08) |
| SAFE-05 — endpoint public phải có cơ chế xác thực thay thế | ✅ PASS | `ipn`/`return` là `AllowAnonymous` nhưng **bảo vệ bằng chữ ký HMAC-SHA512** |
| BIZ-05 — tiền dùng DECIMAL, không float | ✅ PASS | `Payment.Amount` DECIMAL(12,2); `vnp_Amount` là số nguyên ×100 (NFR-03) |
| AUDIT-01 — kích hoạt có audit | ✅ PASS | AuditLog `VNPAY_PAYMENT` (NFR-04) |
| GBL-02 — luật vòng đời membership một nguồn | ✅ PASS | kích hoạt membership dùng lại `MembershipLifecycle` (spec 003). *(Helper nối hạn còn 2 bản giống hệt nhau, kế hoạch gom ở B-20.)* |
| GBL-06 — đổi môi trường không cần sửa code | ✅ PASS | sandbox ↔ live chỉ đổi `BaseUrl`/`TmnCode`/`HashSecret`/`ReturnUrl` (NFR-05) |

## 4. Project Structure

```text
backend/GymMaster.API/
├── Features/Billing/
│   ├── VnPayController.cs      # route "api/v1/payments/vnpay" — create-url, ipn, return
│   ├── IVnPayService.cs · VnPayService.cs   # ★ ký, verify, đối chiếu tiền, kích hoạt
│   ├── VnPayDtos.cs
│   └── MembershipLifecycle.cs  # dùng chung với spec 003 — KHÔNG viết lại luật kích hoạt
├── Infrastructure/
│   └── VnPayLibrary.cs         # dựng query string sắp xếp + HMAC-SHA512 sign/verify
├── Options/VnPayOptions.cs
└── Entities/Payment.cs · Membership.cs      # spec 003, không đổi schema

tests/GymMaster.Api.Tests/VnPayServiceTests.cs   # 6 test: create-url · IPN thành công /
                                                  # idempotent / sai chữ ký / lệch tiền · signature roundtrip
```

**Structure Decision**: đặt trong slice `Features/Billing/` cùng spec 003 chứ **không** tạo slice riêng. Lý do: VNPay là *một phương thức thanh toán khác* của cùng nghiệp vụ, thao tác trên cùng `payments`/`memberships` và phải dùng **đúng** luật kích hoạt của `MembershipLifecycle`. Tách slice sẽ tạo áp lực copy logic vòng đời — chính là lỗi đã từng xảy ra và phải refactor gom lại.

## 5. Design Decisions

> **Chi tiết hoá ADR dự án**: [D-22](../../../06-Management/decision-log.md) (VNPay sandbox, override ADR-03) → toàn bộ bảng dưới · [D-13](../../../06-Management/decision-log.md) (`PendingPayment` — **điều kiện tiên quyết**: không có trạng thái trung gian này thì feature không thể "cắm vào" mà không đổi schema).

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-1001 | Cắm vào `PendingPayment` sẵn có, **không đổi schema** | Spec 003 đã tách bán/thu tiền bằng trạng thái trung gian — VNPay chỉ thay người bấm xác nhận | Không lưu được mã giao dịch VNPay (`provider_ref`) → đối soát live phải thêm cột sau |
| D-1002 | **IPN là nguồn sự thật**; Return chỉ finalize **dự phòng** | IPN là server-to-server, không phụ thuộc việc người dùng có đóng trình duyệt hay không | Hai đường cùng có thể kích hoạt → **bắt buộc** idempotent (D-1004) |
| D-1003 | Verify chữ ký cho **cả** IPN và Return | Return cũng finalize được, nên cũng là bề mặt tấn công | Logic verify phải chạy 2 nơi — gom vào `VnPayLibrary` để không lệch |
| D-1004 | **Idempotent** — đã `Paid` thì trả `RspCode 02`, không kích hoạt lại | VNPay gửi lại IPN nhiều lần theo thiết kế; double-activation = cộng nhầm hạn gói | Phải kiểm trạng thái trước mọi lần ghi |
| D-1005 | `vnp_Amount = Package.Price × 100` **lấy từ server** | Nhận số tiền từ client là lỗ hổng trả 1.000đ cho gói 1.000.000đ | Client không tự chọn được số tiền (đúng như mong muốn) |
| D-1006 | `vnp_TxnRef = GM{Payment.Id}T{yyyyMMddHHmmssfff}` | VNPay yêu cầu `TxnRef` **duy nhất mỗi lần tạo URL**; dùng thẳng `Payment.Id` sẽ trùng khi tạo lại link | Phải parse ngược ra `Payment.Id` khi verify (chấp nhận cả dạng số thuần lẫn `GM…T…`) |
| D-1007 | Mọi lỗi IPN diễn đạt qua **`RspCode`**, luôn HTTP 200 | Đúng giao thức VNPay — trả 4xx/5xx sẽ khiến VNPay retry vô ích | Lỗi không hiện trong monitoring theo HTTP status; phải xem log |
| D-1008 | `Method = Transfer`, không thêm `PaymentMethod.Online` | Không đổi enum đang dùng ở báo cáo doanh thu (spec 003) | Báo cáo `byMethod` không tách được tiền online với chuyển khoản tay |
| D-1009 | Link thanh toán hết hạn **15 phút** | Ngắn hơn TTL 30 phút của đơn `PendingPayment` (spec 003) → link luôn chết trước đơn | Khách trả chậm phải tạo lại link |
| D-1010 | Tái dùng `Payment` `Pending` sẵn có thay vì tạo mới mỗi lần | Bấm "thanh toán" 2 lần không sinh 2 bản ghi tiền | Một `Payment` có thể có nhiều `TxnRef` qua thời gian |
| D-1011 | Sandbox: **không lưu** `provider_ref`/`bank_txn_no` | Cần cột mới từ team DB; không cần cho demo | Làm live phải bổ sung migration + logic lưu mã giao dịch |
| D-1012 | Giữ nguyên luồng thủ công làm fallback | VNPay sandbox có thể sập/tunnel đứt khi demo | Hai đường kích hoạt cùng tồn tại — cả hai phải dùng chung luật vòng đời |

## 6. Data Flow

```text
1) Tạo link thanh toán:
  POST /api/v1/payments/vnpay/create-url {membershipId}     [Member(sở hữu) | Staff]
    → VnPayService.CreatePaymentUrlAsync
        ├─ chưa cấu hình TmnCode/HashSecret     → 500 VNPAY_NOT_CONFIGURED
        ├─ membership không tồn tại             → 404
        ├─ Member không sở hữu hoặc role Admin/PT → 403 FORBIDDEN
        ├─ membership không ở PendingPayment    → 409 INVALID_MEMBERSHIP_STATE
        ├─ tái dùng Payment Pending, không có thì tạo mới     (D-1010)
        ├─ amount = Package.Price × 100   ★ lấy từ server     (D-1005)
        ├─ TxnRef = GM{PaymentId}T{yyyyMMddHHmmssfff} (giờ VN)  (D-1006)
        ├─ CreateDate/ExpireDate giờ VN, hạn 15 phút          (D-1009)
        └─ VnPayLibrary: sắp xếp tham số → ký HMAC-SHA512 → vnp_SecureHash
    → 201 { payUrl, paymentId, amount }

2) IPN — nguồn sự thật (server-to-server):
  GET /api/v1/payments/vnpay/ipn?vnp_*      [AllowAnonymous — bảo vệ bằng chữ ký]
    ├─ verify HMAC-SHA512 sai       → { RspCode: "97" }  ★ KHÔNG kích hoạt   (D-1003)
    ├─ parse TxnRef → Payment không tìm thấy → { RspCode: "01" }
    ├─ Payment đã Paid              → { RspCode: "02" }  ★ idempotent        (D-1004)
    ├─ vnp_Amount ≠ Payment.Amount  → { RspCode: "04" }  ★ KHÔNG kích hoạt   (D-1005)
    ├─ ResponseCode=00 AND TransactionStatus=00 →
    │     Payment → Paid (+PaidAt)
    │     Membership → Active   (qua luật của MembershipLifecycle — spec 003)
    │     AuditLog "VNPAY_PAYMENT"  (UserId = null: hệ thống — spec 008)
    └─ → { RspCode: "00" }
    ★ luôn HTTP 200, lỗi diễn đạt bằng RspCode                                (D-1007)

3) Return — dự phòng (trình duyệt redirect về):
  GET /api/v1/payments/vnpay/return?vnp_*   [AllowAnonymous]
    ├─ chữ ký sai      → 400 INVALID_SIGNATURE
    ├─ số tiền lệch    → 400 INVALID_AMOUNT
    └─ finalize idempotent nếu IPN chưa tới      (D-1002)
    → 200 { paymentId, status, membershipStatus, paidAt }
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-VNP-01 | `Features/Billing/VnPayService.cs` — `CreatePaymentUrlAsync` + `Infrastructure/VnPayLibrary.cs` (ký) |
| FR-VNP-02 | `VnPayService` — 409 `INVALID_MEMBERSHIP_STATE` |
| FR-VNP-03 | `VnPayService` — xử lý IPN, kích hoạt qua `MembershipLifecycle` (spec 003), AuditLog `VNPAY_PAYMENT` |
| FR-VNP-04 | `VnPayLibrary` — verify HMAC-SHA512; IPN `RspCode 97` / Return 400 |
| FR-VNP-05 | `VnPayService` — đối chiếu `vnp_Amount`; IPN `RspCode 04` / Return 400 |
| FR-VNP-06 | `VnPayService` — kiểm `Payment.Status == Paid` → `RspCode 02` |
| FR-VNP-07 | `VnPayController.cs` — action `return`, finalize dự phòng idempotent |
| FR-VNP-08 | `VnPayService` — `Package.Price × 100`, không nhận amount từ client |
| FR-VNP-09 | `Options/VnPayOptions.cs` — secret từ User Secrets |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| **2 đường** cùng kích hoạt được membership (IPN + Return) | IPN đáng tin nhưng có thể chậm/không tới khi chạy local qua tunnel; Return bù lại cho trải nghiệm người dùng | Chỉ IPN → demo hỏng khi tunnel đứt; chỉ Return → mất tiền nếu người dùng đóng trình duyệt trước redirect |
| Endpoint `AllowAnonymous` cho callback | VNPay không gửi JWT được; chữ ký HMAC-SHA512 **là** cơ chế xác thực | Yêu cầu token → không tích hợp được với cổng thanh toán |
| Không lưu mã giao dịch VNPay (D-1011) | Sandbox không cần đối soát; cần cột mới từ team DB | Thêm cột ngay → phụ thuộc lịch của team DB cho một nhu cầu chưa phát sinh |
| `Method = Transfer` thay vì `Online` (D-1008) | Thêm giá trị enum sẽ đụng báo cáo doanh thu `byMethod` của spec 003 đang chạy | Thêm `Online` → phải kiểm lại mọi query thống kê, rủi ro cao hơn giá trị |
| Lỗi IPN luôn trả HTTP 200 (D-1007) | Đúng giao thức VNPay; trả 4xx/5xx khiến VNPay retry vô ích | Trả HTTP status thật → phá hợp đồng với cổng thanh toán |
| Override ADR-03 (MVP chốt thủ công) | Yêu cầu giảng viên bắt buộc có luồng online; đã ghi rõ ở spec §10 | Giữ nguyên ADR-03 → không đáp ứng yêu cầu môn học. **Cần log quyết định vào `docs/06-Management/decision-log.md`** — xem tasks.md T-027 |
