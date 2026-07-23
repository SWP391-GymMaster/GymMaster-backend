---
description: "Task list — Online Payment via VNPay (Sandbox)"
---

# Tasks: Online Payment via VNPay (Sandbox)

**Feature**: `010-online-payment-vnpay`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 25/28 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code, `[ ]` = còn nợ. Các mục `[ ]` cuối cùng là việc cần làm **khi chuyển sang live**, không phải lỗi của bản sandbox.

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 `Options/VnPayOptions.cs` (section `VnPay`) — `TmnCode`, `HashSecret`, `BaseUrl`, `ReturnUrl`, `Version`, `Command`, `CurrCode`, `Locale` + binding trong `Program.cs`
- [X] T002 `TmnCode`/`HashSecret` **chỉ ở server** (User Secrets), không commit → **FR-VNP-09**, **SEC-04**
- [X] T003 Đăng ký tài khoản VNPay **sandbox**, cấu hình Return URL + IPN URL trên portal
- [X] T004 Xác nhận **không đổi schema DB** — tái dùng `payments`/`memberships` của spec 003

## Phase 2: Foundational (lớp mật mã)

**⚠️ Sai ở phase này = lỗ hổng giả mạo callback. Mọi user story đều phụ thuộc.**

- [X] T005 ★ `Infrastructure/VnPayLibrary.cs` — dựng query string **sắp xếp theo tên tham số** + ký/verify **HMAC-SHA512** → **NFR-01**
- [X] T006 Hàm verify dùng chung cho **cả IPN và Return** (một implementation, không copy) → D-1003
- [X] T007 Quy ước `vnp_TxnRef = GM{Payment.Id}T{yyyyMMddHHmmssfff}` (giờ VN) + hàm parse ngược ra `Payment.Id`, chấp nhận **cả dạng số thuần lẫn `GM…T…`** → D-1006
- [X] T008 Đăng ký DI `IVnPayService` → `VnPayService` trong `Program.cs`
- [X] T009 Unit test **signature roundtrip** trong `tests/GymMaster.Api.Tests/VnPayServiceTests.cs`

**Checkpoint**: ký và verify khớp nhau → mới được viết luồng nghiệp vụ.

---

## Phase 3: US1 — Tạo link thanh toán (P1) 🎯 MVP

**Goal**: hội viên bấm "Thanh toán online" và được chuyển sang trang VNPay.
**Independent Test**: membership `PendingPayment` → `create-url` trả `payUrl` chứa `BaseUrl` + `vnp_SecureHash`, tạo `Payment` `Pending` đúng giá gói; membership đã `Active` → 409.

- [X] T010 [US1] `VnPayDtos.cs` — request/response `create-url`
- [X] T011 [US1] `VnPayService.CreatePaymentUrlAsync` — tái dùng `Payment` `Pending` sẵn có, không có thì tạo mới → D-1010
- [X] T012 [US1] ★ `vnp_Amount = Package.Price × 100`, **giá lấy từ server**, không nhận từ client → **FR-VNP-08**, **SEC-07**, D-1005
- [X] T013 [US1] `vnp_CreateDate` / `vnp_ExpireDate` theo **giờ VN**, link hết hạn **15 phút** (ngắn hơn TTL 30 phút của đơn Pending — spec 003) → D-1009
- [X] T014 [US1] Chặn membership không ở `PendingPayment` → 409 `INVALID_MEMBERSHIP_STATE` → **FR-VNP-02**
- [X] T015 [US1] Ownership: Member chỉ trả cho membership của mình → 403; Admin/Staff trả thay bất kỳ
- [X] T016 [US1] Chưa cấu hình VNPay → 500 `VNPAY_NOT_CONFIGURED`
- [X] T017 [US1] `VnPayController.cs` route `api/v1/payments/vnpay`
- [X] T018 [US1] Unit test `create-url` (xUnit + EF Core InMemory)

---

## Phase 4: US2 — IPN tự kích hoạt gói (P1) 🎯 MVP

**Goal**: trả tiền xong là gói tự Active, không cần Staff bấm.
**Independent Test**: gửi IPN hợp lệ `vnp_ResponseCode=00` + `vnp_TransactionStatus=00` → `Payment=Paid`, `Membership=Active`, có AuditLog `VNPAY_PAYMENT`, trả `RspCode 00`; gửi lại lần 2 → `RspCode 02`, không đổi gì.

- [X] T019 [US2] `GET /payments/vnpay/ipn` — `[AllowAnonymous]`, **bảo vệ bằng chữ ký** → **SEC-08**
- [X] T020 [US2] ★ Verify chữ ký sai → `{ RspCode: "97" }`, **KHÔNG kích hoạt** → **FR-VNP-04**
- [X] T021 [US2] Không tìm thấy `Payment` → `{ RspCode: "01" }`
- [X] T022 [US2] ★ **Idempotent** — `Payment` đã `Paid` → `{ RspCode: "02" }`, không kích hoạt lại → **FR-VNP-06**, **NFR-02**, D-1004
- [X] T023 [US2] ★ `vnp_Amount` lệch `Payment.Amount` → `{ RspCode: "04" }`, **KHÔNG kích hoạt** → **FR-VNP-05**
- [X] T024 [US2] Thành công → `Payment=Paid` (+`PaidAt`), `Membership=Active` **qua luật của `MembershipLifecycle`** (spec 003, không viết lại), `{ RspCode: "00" }` → **FR-VNP-03**, **ARCH-03**
- [X] T025 [US2] AuditLog `VNPAY_PAYMENT` với `UserId = null` (hành động của hệ thống — spec 008) → **NFR-04**
- [X] T026 [US2] Mọi lỗi diễn đạt qua `RspCode`, **luôn HTTP 200** (đúng giao thức VNPay) → D-1007
- [X] T027 [US2] Unit test: IPN thành công · idempotent · sai chữ ký · lệch tiền — `VnPayServiceTests.cs`

**Checkpoint**: trọn vòng đời thanh toán online chạy được → demo được yêu cầu của giảng viên.

---

## Phase 5: US3 — Return URL dự phòng (P2)

**Goal**: người dùng thấy kết quả ngay sau khi trả, kể cả khi IPN chưa tới.
**Independent Test**: redirect về `return` với chữ ký hợp lệ → trả trạng thái payment; nếu IPN chưa tới thì finalize luôn, IPN tới sau vẫn `RspCode 02`.

- [X] T028 [US3] `GET /payments/vnpay/return` — `[AllowAnonymous]`, verify chữ ký → 400 `INVALID_SIGNATURE` → **FR-VNP-07**
- [X] T029 [US3] Đối chiếu số tiền → 400 `INVALID_AMOUNT`
- [X] T030 [US3] Finalize **dự phòng idempotent** khi IPN chưa tới; IPN vẫn là **nguồn sự thật** → D-1002
- [X] T031 [US3] Trả `{ paymentId, status, membershipStatus, paidAt }` cho FE

---

## Phase 6: Polish & Cross-cutting

- [X] T032 [P] Giữ nguyên luồng thủ công `POST /memberships/{id}/payment` (spec 003) làm **fallback** → D-1012
- [X] T033 [P] Kiểm chứng đổi sandbox ↔ live **chỉ qua cấu hình**, không sửa code → **NFR-05**
- [X] T034 [P] Ghi chú vận hành: chạy local cần tunnel (ngrok / Cloudflare Tunnel) để VNPay gọi được IPN
- [X] T035 Log quyết định **override ADR-03** vào `docs/06-Management/decision-log.md` → **D-22** (2026-06-26)
- [ ] T036 **Khi làm live** — thêm cột `provider_ref` / `bank_txn_no` nullable vào `payments` (cần team DB) để lưu mã giao dịch VNPay phục vụ đối soát → D-1011
- [ ] T037 **Khi làm live** — cân nhắc thêm `PaymentMethod.Online` để báo cáo `byMethod` (spec 003) tách được tiền online với chuyển khoản tay; hiện đang dùng chung `Transfer` → D-1008

---

## Dependencies & Execution Order

- **Phụ thuộc ngoài**:
  - **spec 003 là điều kiện tiên quyết** — feature này chỉ khả thi vì `PendingPayment` đã tách sẵn "bán" khỏi "thu tiền". Kích hoạt membership **phải** đi qua `MembershipLifecycle`, không được viết lại.
  - spec 001 — ownership Member khi gọi `create-url`;
  - spec 008 — `IAuditService` cho `VNPAY_PAYMENT`.
- **Phase 2 (lớp mật mã) chặn tất cả**: chưa ký/verify đúng thì không có luồng nào an toàn.
- **US1 → US2 → US3**: phải có link mới có callback; Return chỉ là dự phòng cho IPN.
- **Không có feature nào phụ thuộc ngược** vào 010 — gỡ ra thì luồng thủ công của spec 003 vẫn chạy đủ.

```text
[003 PendingPayment + MembershipLifecycle ★ · 001 ownership · 008 audit]
                          ↓
Setup → Foundational(VnPayLibrary ★) → US1 → US2 (IPN) → US3 (Return) → Polish
                                                  ↓
                                    [luồng thủ công 003 vẫn giữ làm fallback]
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01 | T011, T012 | `VnPayServiceTests.cs` (create-url) |
| AC-02 | T024, T025 | `VnPayServiceTests.cs` (IPN thành công) |
| AC-03 | T022 | `VnPayServiceTests.cs` (idempotent) |
| AC-04 | T020 | `VnPayServiceTests.cs` (sai chữ ký) |
| AC-05 | T023 | `VnPayServiceTests.cs` (lệch tiền) |
| AC-06 | T014 | `VnPayServiceTests.cs` |

> 6 unit test hiện có phủ **toàn bộ 6 AC** — đây là feature có độ phủ test tốt nhất trong 10 spec, tương xứng với mức rủi ro bảo mật.
