# Feature Specification: Online Payment via VNPay (Sandbox)

**Feature Branch**: `010-online-payment-vnpay`
**Created**: 2026-06-15
**Status**: Draft
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: Yêu cầu giảng viên (bắt buộc có luồng thanh toán online); mở rộng spec 003 §10 (đã tiên liệu); **override ADR-03** (MVP thủ công → bổ sung cổng online chạy **sandbox**)

> EARS legend như spec 001. Spec này **bổ sung** cho [003 — Membership & Billing](../003-membership-billing/spec.md), không thay thế luồng thủ công (vẫn giữ làm fallback).

---

## 1. Context & Goal
Spec 003 ghi nhận thanh toán **thủ công** (Staff bấm xác nhận — ADR-03). Giảng viên yêu cầu hệ thống phải có **luồng thanh toán online** thực sự: hội viên trả tiền qua cổng, hệ thống **tự xác nhận** và kích hoạt gói.

Mục tiêu: tích hợp **VNPay** ở chế độ **sandbox** để demo trọn vòng đời — tạo yêu cầu thanh toán → chuyển sang trang VNPay → trả bằng thẻ test → VNPay gọi **IPN** (server-to-server) → hệ thống kích hoạt `Membership` tự động. Không tiền thật, không hợp đồng merchant. Chuyển sang live chỉ là đổi cấu hình (BaseUrl + TmnCode + HashSecret), **không đổi logic**.

Sai = sai doanh thu, kích hoạt nhầm gói, hoặc lỗ hổng giả mạo callback → bắt buộc verify chữ ký + đối chiếu số tiền + idempotent.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Member | Tự khởi tạo thanh toán online cho membership *của mình* đang `PendingPayment` |
| Admin / Staff | Khởi tạo thanh toán online thay cho bất kỳ membership `PendingPayment` |
| VNPay (Sandbox) | Xử lý thu tiền; gọi **IPN** + redirect **Return URL** kèm chữ ký |
| System | Ký/verify HMAC-SHA512, đối chiếu số tiền, kích hoạt membership, ghi AuditLog |

## 3. Functional Requirements (EARS)
- **FR-VNP-01 (Event):** WHEN Member (sở hữu) hoặc Admin/Staff yêu cầu thanh toán online cho membership `PendingPayment`, THE system SHALL tạo (hoặc tái dùng) một `Payment` trạng thái `Pending` và trả về **URL thanh toán VNPay đã ký** HMAC-SHA512.
- **FR-VNP-02 (Unwanted):** IF membership không ở trạng thái `PendingPayment`, THEN THE system SHALL từ chối với `409 INVALID_MEMBERSHIP_STATE`.
- **FR-VNP-03 (Event):** WHEN VNPay gọi **IPN** với chữ ký hợp lệ và giao dịch thành công (`vnp_ResponseCode=00` AND `vnp_TransactionStatus=00`), THE system SHALL đặt `Payment=Paid` (+`PaidAt`), chuyển `Membership=Active`, ghi AuditLog `VNPAY_PAYMENT`, và trả `{ RspCode:"00" }`.
- **FR-VNP-04 (Unwanted):** IF chữ ký IPN/Return không hợp lệ, THEN THE system SHALL từ chối (IPN `{ RspCode:"97" }` / Return `400 INVALID_SIGNATURE`) và **KHÔNG** kích hoạt membership.
- **FR-VNP-05 (Unwanted):** IF số tiền callback (`vnp_Amount`) lệch số tiền của `Payment`, THEN THE system SHALL từ chối (IPN `{ RspCode:"04" }` / Return `400 INVALID_AMOUNT`).
- **FR-VNP-06 (State):** WHILE `Payment` đã `Paid`, IF nhận callback lần nữa cho cùng giao dịch, THEN THE system SHALL trả `{ RspCode:"02" }` và **không kích hoạt lại** (idempotent).
- **FR-VNP-07 (Event):** WHEN VNPay redirect trình duyệt về **Return URL**, THE system SHALL verify chữ ký rồi trả trạng thái payment cho FE. IPN là **nguồn sự thật**; Return chỉ finalize **dự phòng** (idempotent) cho trường hợp IPN chưa tới.
- **FR-VNP-08 (Ubiquitous):** THE system SHALL tính `vnp_Amount = Package.Price × 100` (đơn vị VND nhỏ nhất) và **luôn lấy giá từ Package ở server** — không nhận số tiền từ client.
- **FR-VNP-09 (Ubiquitous, security):** THE system SHALL giữ `TmnCode`/`HashSecret` **chỉ ở server** (User Secrets) và KHÔNG nhận `amount`/`method`/trạng thái kích hoạt từ client.

## 4. Non-functional Requirements
- **NFR-01:** Chữ ký dùng **HMAC-SHA512** với `HashSecret`; verify cho **cả** IPN và Return.
- **NFR-02:** Xử lý callback **idempotent** — IPN gửi lại nhiều lần không tạo double-activation.
- **NFR-03:** Tính tiền DECIMAL(12,2); `vnp_Amount` là số nguyên (×100), không float.
- **NFR-04:** Mọi lần kích hoạt có AuditLog + truy vết (`VNPAY_PAYMENT`).
- **NFR-05:** Đổi sandbox ↔ live chỉ qua cấu hình (`BaseUrl`, `TmnCode`, `HashSecret`, `ReturnUrl`) — không sửa code.

## 5. Data Model
Tái dùng bảng của spec 003 — **không đổi schema** (bản tối giản cho demo):
- **Payments**(Id, MembershipId, Amount, Method, Status{Pending,Paid,Refunded}, PaidAt, CreatedBy, CreatedAt, UpdatedAt)
  - Thanh toán online: `Method = Transfer`, vòng đời `Pending → Paid`.
  - **`vnp_TxnRef` = `Payment.Id`** (mã định danh giao dịch gửi sang VNPay).
- **Memberships**: `PendingPayment → Active` khi IPN thành công.
- **Cấu hình** `VnPayOptions` (section `VnPay`): TmnCode, HashSecret *(User Secrets)*, BaseUrl, ReturnUrl, Version, Command, CurrCode, Locale.
- **Tương lai (khi làm live, ngoài demo):** thêm cột `provider_ref`/`bank_txn_no` nullable (cần DB team) để lưu mã giao dịch VNPay; thêm `PaymentMethod.Online`. **Không** làm ở demo.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/payments/vnpay/create-url | Member (sở hữu), Admin, Staff | {membershipId} | 201 {payUrl, paymentId, amount} | 403, 404, 409, 500 `VNPAY_NOT_CONFIGURED` |
| GET | /api/v1/payments/vnpay/ipn | VNPay (AllowAnonymous, bảo vệ bằng chữ ký) | query `vnp_*` | 200 {RspCode, Message} | — (mọi lỗi diễn đạt qua RspCode) |
| GET | /api/v1/payments/vnpay/return | AllowAnonymous (trình duyệt) | query `vnp_*` | 200 {paymentId, status, membershipStatus, paidAt} | 400, 404 |

**IPN RspCode:** `00` Confirm Success · `01` Order not found · `02` Order already confirmed · `04` Invalid amount · `97` Invalid signature.

## 7. Error Handling (EARS Unwanted)
- IF chưa cấu hình VNPay (thiếu TmnCode/HashSecret), THEN `500 VNPAY_NOT_CONFIGURED`.
- IF membership/Payment không tồn tại, THEN `404 NOT_FOUND` (IPN: `RspCode 01`).
- IF membership không `PendingPayment`, THEN `409 INVALID_MEMBERSHIP_STATE`.
- IF người gọi là Member nhưng không sở hữu membership, THEN `403 FORBIDDEN`.
- IF chữ ký không hợp lệ, THEN IPN `RspCode 97` / Return `400 INVALID_SIGNATURE`.
- IF số tiền lệch, THEN IPN `RspCode 04` / Return `400 INVALID_AMOUNT`.
- IF callback trùng (đã Paid), THEN IPN `RspCode 02` (idempotent, không lỗi nghiệp vụ).

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given membership `PendingPayment`, When gọi `create-url`, Then nhận `payUrl` chứa BaseUrl + `vnp_SecureHash` và tạo `Payment` `Pending` đúng số tiền gói.
- [ ] **AC-02:** Given `Payment` `Pending`, When IPN hợp lệ + thành công, Then `Payment=Paid`, `Membership=Active`, AuditLog `VNPAY_PAYMENT`, trả `RspCode 00`.
- [ ] **AC-03:** Given đã `Paid`, When IPN lần 2, Then `RspCode 02`, không thay đổi gì.
- [ ] **AC-04:** Given IPN sai chữ ký, Then `RspCode 97`, Membership vẫn `PendingPayment`.
- [ ] **AC-05:** Given chữ ký đúng nhưng `vnp_Amount` lệch giá gói, Then `RspCode 04`, không kích hoạt.
- [ ] **AC-06:** Given membership đã `Active`/`Cancelled`, When `create-url`, Then `409 INVALID_MEMBERSHIP_STATE`.

## 9. Out of Scope
- Giao dịch **tiền thật** / hợp đồng merchant / quyết toán / đối soát ngân hàng (chỉ sandbox demo).
- **Refund** tự động qua API VNPay; **recurring/subscription** tự gia hạn.
- Lưu mã giao dịch VNPay (`provider_ref`) — để dành khi làm live.
- Đa cổng (MoMo/ZaloPay), ví điện tử, trả góp.

## 10. Ghi chú triển khai (2026-06-15)
- **Override ADR-03**: MVP gốc chốt thủ công; spec này bổ sung cổng online theo yêu cầu giảng viên. Luồng thủ công (`POST /memberships/{id}/payment`) **vẫn giữ** làm fallback. Đề nghị log quyết định ở `12_DECISION_LOG.md`.
- **Bản tối giản (demo)**: không đổi schema DB; `vnp_TxnRef = Payment.Id`; `Method = Transfer`; member tự trả gói của mình.
- **IPN chạy local cần tunnel** (ngrok / Cloudflare Tunnel) để VNPay sandbox gọi vào `localhost`; đăng ký URL tunnel làm Return/IPN trên portal sandbox. Return URL có finalize dự phòng nếu IPN không tới được.
- **Thuộc Part Y** (billing 003) — không đụng module của thành viên khác.
- **Test**: 6 unit test (xUnit + EF Core InMemory) ở `tests/GymMaster.Api.Tests/VnPayServiceTests.cs` — create-url, IPN thành công/idempotent/sai chữ ký/lệch tiền, signature roundtrip.
