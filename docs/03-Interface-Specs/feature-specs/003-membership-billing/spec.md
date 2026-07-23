# Feature Specification: Membership Packages, Sell, Renew & Payment

**Feature Branch**: `003-membership-billing`
**Created**: 2026-05-30
**Status**: Implemented (spec đồng bộ theo code 2026-07-15)
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: archive/06_FEATURE_SPECS (F1), srs-use-cases (UC-06/07/08), requirements (FR-PKG/MS/PAY), ADR-03/05, D-13

> EARS legend như spec 001. Mọi path dưới `/api/v1`. Thanh toán **online VNPay** ở spec 010 (mở rộng, không thay thế luồng thủ công). Quy tắc vòng đời membership gom ở `MembershipLifecycle` (dùng chung mọi service).

---

## 1. Context & Goal
Quản lý gói tập mẫu, bán/gia hạn gói cho hội viên và ghi nhận thanh toán **thủ công** (ADR-03) + online (spec 010). Đây là nguồn doanh thu chính và đầu vào dashboard. Mục tiêu: tính EndDate chính xác, tách bán gói khỏi thanh toán bằng trạng thái `PendingPayment` (D-13), và **một quy tắc vòng đời duy nhất** cho mọi đường đọc/ghi.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Admin | CRUD gói; bán/gia hạn; ghi payment; huỷ đơn bất kỳ |
| Staff | Bán/gia hạn; ghi payment; huỷ đơn bất kỳ |
| Member | Gửi *yêu cầu* gia hạn (ADR-05); huỷ đơn Pending/gói Active của mình; không tự kích hoạt |
| System | Tính EndDate, set trạng thái, auto-expire, ghi AuditLog |

## 3. Functional Requirements (EARS)
- **FR-PKG-01 (Event):** WHEN Admin tạo/sửa gói với DurationDays > 0 và Price ≥ 0 (tên không trùng), THE system SHALL lưu MembershipPackage.
- **FR-PKG-02 (State):** WHILE gói `IsActive=false`, THE system SHALL từ chối bán/gia hạn gói đó (`422 PACKAGE_INACTIVE`).
- **FR-PKG-03 (Event):** WHEN Admin tạo/sửa gói, THE system SHALL cho đánh dấu gói **có hỗ trợ PT hay không** qua `SupportsPT` (mặc định `false`).
- **FR-PKG-04 (Ubiquitous):** THE system SHALL coi quyền dùng PT là **suy ra động từ gói đang hiệu lực** — hội viên được dùng PT ⇔ tồn tại Membership `Status=Active` AND `EndDate ≥ hôm nay (giờ VN)` AND `Package.SupportsPT=true`. (Quy tắc gác PT ở spec 005.)
- **FR-MS-01 (Event):** WHEN Staff/Admin bán gói hợp lệ cho Member (chưa có gói Active còn hạn), THE system SHALL tạo Membership `PendingPayment` với `EndDate = StartDate + DurationDays`. WHERE member đã có đơn `PendingPayment`, THE system SHALL **tái dùng đơn đó** thay vì tạo trùng.
- **FR-MS-02 (Event):** WHEN Staff/Admin ghi nhận payment `Paid` cho membership `PendingPayment` (Amount ≥ giá gói), THE system SHALL chuyển Membership → `Active`, tái dùng bản `Payment` Pending nếu có, huỷ các đơn Pending anh em, ghi AuditLog `CONFIRM_PAYMENT`.
- **FR-MS-03 (Event):** WHEN gia hạn (`/renew`, request `{packageId, method}`), THE system SHALL **kéo dài tại chỗ**: `EndDate = (EndDate hiện tại nếu còn hạn, ngược lại hôm nay) + DurationDays`, đặt `Active`, ghi nhận Payment `Paid` ngay. `method` nhận chuỗi `"cash"/"transfer"/"card"` (không phân biệt hoa thường).
- **FR-MS-03a (Unwanted):** IF gói gia hạn có `SupportsPT` khác gói Active hiện tại, THEN 422 `PACKAGE_PT_MISMATCH` (phải đổi loại gói khi hết hạn, không đổi giữa chừng).
- **FR-MS-04 (Optional):** WHERE người gọi là Member, THE system SHALL chỉ cho tạo *yêu cầu gia hạn* (`/renewal-request` → tạo đơn `PendingPayment`), Admin/Staff/VNPay xác nhận thanh toán mới Active.
- **FR-MS-07 (Ubiquitous — MembershipLifecycle):** THE system SHALL áp dụng vòng đời chung mọi nơi: (a) đơn `PendingPayment` quá **30 phút** chưa thanh toán → tự `Cancelled`; (b) `Active` đã quá `EndDate` → `Expired` (lazy, khi có truy vấn); (c) "goi hiệu lực" = `Active` AND `EndDate ≥ hôm nay`.
- **FR-MS-08 (Event):** WHEN huỷ membership (`/cancel`), THE system SHALL chuyển `PendingPayment`/`Active` → `Cancelled`. Member chỉ huỷ được đơn của chính mình; Staff/Admin huỷ bất kỳ. Không hoàn tiền (ngoài phạm vi).
- **FR-MS-09 (Event):** WHEN xem lịch sử membership của một member (`GET /members/{id}/memberships`), THE system SHALL đồng bộ trạng thái (expire) rồi trả danh sách mới→cũ (ownership: Admin/Staff mọi member, Member chỉ của mình).
- **FR-PAY-01 (Unwanted):** IF ghi payment cho membership đã `Active` (đã thanh toán), THEN 409 `DUPLICATE_PAYMENT`.
- **FR-MS-05 (State):** WHILE Membership chưa `Active`, THE system SHALL từ chối check-in cho Member đó (spec 004, khi bật EnforceMembership).
- **FR-MS-06 (Ubiquitous):** THE system SHALL ghi AuditLog cho mọi hành động bán/gia hạn/payment/huỷ.

> **Nối hạn (renewal window):** khi kích hoạt một đơn mới (qua payment thủ công hoặc VNPay) mà member đang còn một gói Active khác, hệ thống **nối tiếp**: `EndDate mới = EndDate gói Active cũ + DurationDays` và **huỷ (`Cancelled`) gói Active cũ** để vẫn giữ bất biến "tối đa 1 Active/member".

## 4. Non-functional Requirements
- **NFR-01:** Thao tác bán gói < 500ms.
- **NFR-02:** Tính tiền dùng DECIMAL(12,2), không dùng float.
- **NFR-03:** Mọi bước có audit + truy vết người thực hiện (CreatedBy từ JWT).
- **NFR-04:** "Hôm nay" tính theo **giờ VN (GMT+7, `AppClock`)** để không lệch hạn 1 ngày vào rạng sáng.

## 5. Data Model
- **membership_packages**(Id, Name[UNIQUE], Description, DurationDays SMALLINT, Price DECIMAL(12,2), **SupportsPT BIT** {0=thường, 1=có PT}, IsActive, CreatedAt, UpdatedAt)
- **memberships**(Id, MemberId→member_profiles, PackageId→membership_packages, StartDate DATE, EndDate DATE, Status TINYINT{0 PendingPayment,1 Active,2 Expired,3 Cancelled}, CreatedByUserId, CreatedAt, UpdatedAt)
- **payments**(Id, MembershipId→memberships, Amount DECIMAL(12,2), PaymentMethod TINYINT{1 Cash,2 Transfer,3 Card}, Status TINYINT{0 Pending,1 Paid,2 Refunded}, PaidAt, CreatedByUserId, CreatedAt, UpdatedAt)

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/packages | Admin | {name, durationDays, price, description?, supportsPT?} | 201 | 400, 409 |
| PUT | /api/v1/packages/{id} | Admin | {name?, durationDays?, price?, description?, isActive?, supportsPT?} | 200 | 400, 404, 409 |
| GET | /api/v1/packages | tất cả (Member chỉ thấy IsActive) | — | 200 (kèm `supportsPT`) | 401 |
| POST | /api/v1/memberships/sell | Admin, Staff | {memberId, packageId, startDate?} | 201 {membership: PendingPayment} | 400, 404, 409, 422 |
| POST | /api/v1/memberships/{id}/payment | Admin, Staff | {amount, method} | 201 {membership, payment, status} | 404, 409, 422 |
| POST | /api/v1/memberships/{id}/renew | Admin, Staff | {packageId, method} | 200 MembershipResponse | 404, 422 |
| POST | /api/v1/memberships/renewal-request | Member | {packageId} | 201 (đơn PendingPayment) | 404, 422 |
| POST | /api/v1/memberships/{id}/cancel | Member(self), Staff, Admin | — | 200 MembershipResponse | 403, 404, 422 |
| GET | /api/v1/memberships?status=&page= | Admin, Staff | — | 200 (PagedResult MembershipResponse) | 401, 403, 422 |
| GET | /api/v1/members/{id}/memberships | Admin, Staff, Member(self) | — | 200 (mảng MembershipResponse) | 403, 404 |
| GET | /api/v1/members/{id}/payments | Admin, Staff, Member(self) | — | 200 (mảng PaymentHistoryResponse) | 403, 404 |
| GET | /api/v1/payments?from=&to=&status=&memberId=&page=&pageSize= | Admin, Staff | — | 200 (PagedResult PaymentHistoryResponse) | 401, 403 |
| GET | /api/v1/payments/summary?from=&to= | Admin, Staff | — | 200 PaymentSummaryResponse | 401, 403 |

### 6.1. Response contract (JSON camelCase; enum PascalCase)
**MembershipResponse:** `{ id, memberId, packageId, packageName, supportsPT, startDate, endDate, status, daysRemaining, isExpiringSoon, createdAt }` — `isExpiringSoon` = Active và còn 0..7 ngày.
**`/payment` → ConfirmPaymentResult:** `{ membership: MembershipResponse, payment: { id, membershipId, paidAt }, status }`.
**PaymentHistoryResponse:** `{ id, membershipId, memberId, memberName, memberEmail, packageId, packageName, amount, paymentMethod, status, membershipStatus, paymentDate, paidAt, createdAt, createdByUserId, createdByName }`.
**PaymentSummaryResponse:** `{ from, to, totalPayments, paidPayments, pendingPayments, revenue, byMethod:[{paymentMethod,count,amount}], byDay:[{date,count,amount}] }` — gom theo **ngày VN**.

## 7. Error Handling (EARS Unwanted)
- IF gói/Member không tồn tại, THEN 404 `NOT_FOUND`.
- IF bán/gia hạn gói Inactive, THEN 422 `PACKAGE_INACTIVE`.
- IF StartDate ở quá khứ, THEN 422 `INVALID_START_DATE`.
- IF member đã có gói Active còn hạn khi bán, THEN 409 `ALREADY_HAS_ACTIVE`.
- IF ghi payment cho membership đã Active, THEN 409 `DUPLICATE_PAYMENT`.
- IF Amount < giá gói, THEN 422 `INSUFFICIENT_AMOUNT`.
- IF gia hạn gói đã huỷ, THEN 422 `MEMBERSHIP_CANCELLED`; IF gói gia hạn lệch loại PT, THEN 422 `PACKAGE_PT_MISMATCH`.
- IF huỷ membership không ở Pending/Active, THEN 422 `CANNOT_CANCEL`; IF Member huỷ đơn không phải của mình, THEN 403 `FORBIDDEN`.
- IF ghi payment cho đơn đã Cancelled, THEN 409 `MEMBERSHIP_CANCELLED`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given gói 30 ngày, When Staff bán với StartDate=01/06, Then Membership PendingPayment, EndDate=01/07.
- [ ] **AC-02:** Given Membership PendingPayment, When ghi payment Paid (≥ giá gói), Then status=Active + AuditLog `CONFIRM_PAYMENT`.
- [ ] **AC-03:** Given Member đang Active đến 01/07, When gia hạn gói 30 ngày, Then EndDate mới=31/07 (nối tiếp).
- [ ] **AC-04:** Given Membership PendingPayment, When Member check-in (EnforceMembership bật), Then bị từ chối.
- [ ] **AC-05:** Given membership đã Active, When ghi payment lại, Then 409 `DUPLICATE_PAYMENT`.
- [ ] **AC-06:** Given Member gửi yêu cầu gia hạn, When chưa xác nhận, Then đơn ở PendingPayment (chưa Active).
- [ ] **AC-07:** Given Admin tạo gói `supportsPT=true`, When lưu, Then `SupportsPT=1`; mặc định = 0.
- [ ] **AC-08:** Given Member gói `SupportsPT=true` Active còn hạn, When kiểm tra quyền PT, Then = true; gói hết hạn/không PT → false.
- [ ] **AC-09:** Given đơn PendingPayment tạo quá 30 phút, When truy vấn lại, Then đơn tự chuyển `Cancelled`.
- [ ] **AC-10:** Given Member có đơn Pending, When huỷ đơn của mình, Then `Cancelled`; huỷ đơn người khác → 403.

## 9. Out of Scope
- Hoá đơn điện tử/VAT, trả góp, hoàn tiền tự động, khuyến mãi/voucher (secondary). Online gateway ở spec 010.
