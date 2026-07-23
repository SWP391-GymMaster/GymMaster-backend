---
description: "Task list — Membership Packages, Sell, Renew & Payment"
---

# Tasks: Membership Packages, Sell, Renew & Payment

**Feature**: `003-membership-billing`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 41/43 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code, `[ ]` = còn nợ (phát hiện khi đối chiếu spec ↔ code).

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 Tạo slice `backend/GymMaster.API/Features/Billing/`
- [X] T002 [P] `Common/AppClock.cs` — hàm lấy "hôm nay" theo giờ VN (GMT+7) → **NFR-04**

## Phase 2: Foundational (chặn mọi user story)

- [X] T003 `Entities/MembershipPackage.cs` — `Price DECIMAL(12,2)`, `DurationDays`, `IsActive`
- [X] T004 `Entities/Membership.cs` + `MembershipEnums.cs` — `MembershipStatus {0 PendingPayment, 1 Active, 2 Expired, 3 Cancelled}`
- [X] T005 `Entities/Payment.cs` — `PaymentMethod {1 Cash, 2 Transfer, 3 Card}`, `PaymentStatus {0 Pending, 1 Paid, 2 Refunded}`
- [X] T006 Cấu hình precision `DECIMAL(12,2)` cho `Price`/`Amount` trong `Data/GymMasterDbContext.cs` → **NFR-02**
- [X] T007 ★ `Features/Billing/MembershipLifecycle.cs` — **luật vòng đời dùng chung** → **FR-MS-07**
  - `IsActiveOn(membership, today)` — định nghĩa duy nhất của "gói đang hiệu lực"
  - `ExpireIfPastDue(...)` — lazy expire `Active` quá `EndDate` → `Expired`
  - `ExpireStalePending(...)` — đơn `PendingPayment` quá `PendingPaymentTtl = 30 phút` → `Cancelled`
- [X] T008 **Refactor**: gỡ 3 bản sao logic vòng đời ở `MembershipService` / `ProgressService` / `VnPayService`, trỏ hết về T007 → **ARCH-03**
- [X] T009 Đăng ký DI `IMembershipPackageService`, `IMembershipService`, `IPaymentService` trong `Program.cs`

**Checkpoint**: có entity + một luật vòng đời duy nhất → mọi user story bắt đầu được.

---

## Phase 3: US1 — Quản lý gói tập (P1) 🎯 MVP

**Goal**: Admin định nghĩa danh mục gói để bán.
**Independent Test**: `POST /packages` tạo gói 30 ngày → `GET /packages` thấy gói; tạo trùng tên → 409; đặt `isActive=false` rồi bán → 422.

- [X] T010 [US1] `Features/Billing/PackageDtos.cs` — request/response gói
- [X] T011 [US1] `MembershipPackageService.cs` — `CreateAsync`/`UpdateAsync`, validate `DurationDays > 0`, `Price ≥ 0`, tên không trùng → **FR-PKG-01**
- [X] T012 [US1] Cờ `IsActive` — gói tắt thì từ chối bán/gia hạn (422 `PACKAGE_INACTIVE`) → **FR-PKG-02**
- [X] T013 [US1] Migration `database/008_package_supports_pt.sql` + cột `SupportsPT` (mặc định `false`) → **FR-PKG-03**
- [X] T014 [US1] `PackagesController.cs` — Admin CRUD; Member `GET` chỉ thấy gói `IsActive`

---

## Phase 4: US2 — Bán gói & ghi nhận thanh toán (P1) 🎯 MVP

**Goal**: Staff bán gói tại quầy và xác nhận đã thu tiền.
**Independent Test**: `POST /memberships/sell` → đơn `PendingPayment`, `EndDate = StartDate + DurationDays` → `POST /memberships/{id}/payment` → `Active` + có AuditLog `CONFIRM_PAYMENT`.

- [X] T015 [US2] `MembershipDtos.cs` — `MembershipResponse` (kèm `daysRemaining`, `isExpiringSoon` = Active và còn 0..7 ngày), `ConfirmPaymentResult`
- [X] T016 [US2] `MembershipService.SellAsync` — tạo đơn `PendingPayment`, tính `EndDate` → **FR-MS-01**
- [X] T017 [US2] Tái dùng đơn `PendingPayment` sẵn có thay vì tạo trùng → **FR-MS-01**
- [X] T018 [US2] Chặn bán khi member đã có gói Active còn hạn → 409 `ALREADY_HAS_ACTIVE`
- [X] T019 [US2] Chặn `StartDate` ở quá khứ → 422 `INVALID_START_DATE`
- [X] T020 [US2] `PaymentService.cs` — ghi payment `Paid`, tái dùng bản `Payment` Pending nếu có → **FR-MS-02**
- [X] T021 [US2] Chuyển membership → `Active`, huỷ các đơn Pending anh em → **FR-MS-02**
- [X] T022 [US2] **Nối hạn**: nếu member còn gói Active khác, cộng dồn `EndDate` và `Cancelled` gói cũ (giữ bất biến 1 Active/member)
- [X] T023 [US2] Chặn ghi payment cho membership đã `Active` → 409 `DUPLICATE_PAYMENT` → **FR-PAY-01**
- [X] T024 [US2] Chặn `Amount` < giá gói → 422 `INSUFFICIENT_AMOUNT`; đơn đã `Cancelled` → 409 `MEMBERSHIP_CANCELLED`
- [X] T025 [US2] Ghi AuditLog `SELL_MEMBERSHIP` / `CONFIRM_PAYMENT` → **FR-MS-06**
- [X] T026 [US2] `MembershipsController.cs` — `[Authorize(Roles="admin,staff")]` cho sell/payment
- [X] T027 [US2] Unit test `tests/GymMaster.Api.Tests/MembershipServiceTests.cs`

**Checkpoint**: bán được gói và thu được tiền → MVP doanh thu chạy.

---

## Phase 5: US3 — Gia hạn (P2)

**Goal**: hội viên tiếp tục dùng dịch vụ mà không mất số ngày còn lại.
**Independent Test**: member Active đến 01/07, gia hạn gói 30 ngày → `EndDate` = 31/07 (nối tiếp), không phải 30 ngày kể từ hôm nay.

- [X] T028 [US3] `MembershipService.RenewAsync` — kéo dài **tại chỗ**: `EndDate = (còn hạn ? EndDate : hôm nay VN) + DurationDays` → **FR-MS-03**
- [X] T029 [US3] Nhận `method` dạng chuỗi `"cash"/"transfer"/"card"` không phân biệt hoa thường
- [X] T030 [US3] Ghi Payment `Paid` ngay khi gia hạn (không đi qua `PendingPayment`)
- [X] T031 [US3] Chặn gia hạn lệch loại PT → 422 `PACKAGE_PT_MISMATCH` → **FR-MS-03a**
- [X] T032 [US3] Chặn gia hạn gói đã huỷ → 422 `MEMBERSHIP_CANCELLED`
- [X] T033 [US3] `POST /memberships/renewal-request` cho Member — **chỉ tạo đơn `PendingPayment`**, không tự Active (ADR-05) → **FR-MS-04**

---

## Phase 6: US4 — Huỷ đơn & tra cứu lịch sử (P2)

**Goal**: sửa sai thao tác và tra được lịch sử giao dịch.
**Independent Test**: Member huỷ đơn Pending của mình → `Cancelled`; huỷ đơn người khác → 403; `GET /members/{id}/memberships` trả danh sách mới→cũ đã đồng bộ trạng thái.

- [X] T034 [US4] `MembershipService.CancelAsync` — `PendingPayment`/`Active` → `Cancelled`; trạng thái khác → 422 `CANNOT_CANCEL` → **FR-MS-08**
- [X] T035 [US4] Ownership: Member chỉ huỷ đơn của mình (403), Staff/Admin huỷ bất kỳ
- [X] T036 [US4] `MemberMembershipsController.cs` — `GET /members/{id}/memberships`, đồng bộ trạng thái trước khi trả, sắp xếp mới→cũ → **FR-MS-09**
- [X] T037 [US4] `MemberPaymentsController.cs` — `GET /members/{id}/payments` (ownership như trên)
- [X] T038 [US4] `PaymentsController.cs` — `GET /payments` lọc `from`/`to`/`status`/`memberId`, phân trang
- [X] T039 [US4] `PaymentDtos.cs` + `PaymentSummaryResponse` — `GET /payments/summary` gom `byMethod` và `byDay` **theo ngày VN**

---

## Phase 7: Polish & Cross-cutting

- [X] T040 [P] Kiểm tra mọi so sánh ngày đều qua `AppClock`, không còn `DateTime.Today` → **D-209**
- [X] T042 Unit test `tests/GymMaster.Api.Tests/PaymentServiceTests.cs` — **25 test**, phủ cả `Summary_groups_daily_revenue_by_vietnam_date_not_utc` (đúng chỗ rủi ro nhất về múi giờ). Thêm `MembershipPackageServiceTests.cs`.
- [X] T043 Test cho `MembershipLifecycle` — `MembershipLifecycleTests.cs`, 18 test: `IsActiveOn` · `ExpireIfPastDue` · `ExpireStalePending` (TTL 30 phút, AC-09) · `ApplyPaidRenewalWindow`

---

## Dependencies & Execution Order

- **T007 (`MembershipLifecycle`) chặn tất cả**: mọi service đọc/ghi membership đều gọi vào đây. Sửa file này là sửa hành vi của spec 003, 004, 006 và 010 cùng lúc.
- **US1 → US2**: không có gói thì không bán được.
- **US2 → US3, US4**: gia hạn và huỷ thao tác trên membership do US2 tạo ra.
- **Feature ngoài phụ thuộc**: spec 004 (check-in) gọi `MembershipLifecycle.IsActiveOn`; spec 005 gác quyền PT qua `Package.SupportsPT`; spec 010 cắm vào đúng bước xác nhận thanh toán của US2.

```text
[spec 001/002] → Setup → Foundational(T007 ★) → US1 → US2 ─┬→ US3
                                                            └→ US4 → Polish
                                          ↓
                        [spec 004 · 005 · 006 · 010 đều đọc T007]
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01 | T016 | `MembershipServiceTests.cs` |
| AC-02 | T020, T021, T025 | `MembershipServiceTests.cs` |
| AC-03 | T028 | `MembershipServiceTests.cs` |
| AC-04 | gác ở spec 004 (T-CHK) | black-box |
| AC-05 | T023 | `MembershipServiceTests.cs` |
| AC-06 | T033 | black-box |
| AC-07 | T013 | `MembershipServiceTests.cs` |
| AC-08 | T007 + `Package.SupportsPT` | kiểm chứng qua spec 005 |
| AC-09 | T007 (`ExpireStalePending`) | **chưa có test — T043** |
| AC-10 | T034, T035 | black-box |
