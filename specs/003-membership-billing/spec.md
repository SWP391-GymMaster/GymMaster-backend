# Feature Specification: Membership Packages, Sell, Renew & Payment

**Feature Branch**: `003-membership-billing`
**Created**: 2026-05-30
**Status**: Approved
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 06_FEATURE_SPECS (F1), 03_SRS (UC-06/07/08), 04 (FR-PKG/MS/PAY), ADR-03/05, D-13

> EARS legend như spec 001.

---

## 1. Context & Goal
Quản lý gói tập mẫu, bán/gia hạn gói cho hội viên và ghi nhận thanh toán **thủ công** (ADR-03). Đây là nguồn doanh thu chính và đầu vào dashboard. Sai = sai doanh thu + tranh chấp. Mục tiêu: tính EndDate chính xác, tách bán gói khỏi thanh toán bằng trạng thái PendingPayment (D-13).

## 2. Actors
| Actor | Vai trò |
|---|---|
| Admin | CRUD gói; bán/gia hạn; ghi payment |
| Staff | Bán/gia hạn; ghi payment |
| Member | Gửi *yêu cầu* gia hạn (ADR-05); không tự kích hoạt |
| System | Tính EndDate, set trạng thái, ghi AuditLog |

## 3. Functional Requirements (EARS)
- **FR-PKG-01 (Event):** WHEN Admin tạo/sửa gói với DurationDays > 0 và Price ≥ 0, THE system SHALL lưu MembershipPackage.
- **FR-PKG-02 (State):** WHILE gói ở trạng thái Inactive, THE system SHALL KHÔNG cho bán gói đó.
- **FR-MS-01 (Event):** WHEN Staff bán gói hợp lệ cho Member, THE system SHALL tạo Membership trạng thái `PendingPayment` với `EndDate = StartDate + Package.DurationDays`.
- **FR-MS-02 (Event):** WHEN payment cho membership được ghi nhận `Paid`, THE system SHALL chuyển Membership → `Active` và ghi AuditLog `CONFIRM_PAYMENT`.
- **FR-MS-03 (Event):** WHEN gia hạn cho Member đang có gói `Active`, THE system SHALL đặt `StartDate = EndDate hiện tại` để nối tiếp, không trùng ngày.
- **FR-MS-04 (Optional):** WHERE người gọi là Member, THE system SHALL chỉ cho tạo *yêu cầu gia hạn* (RenewalRequest), Admin/Staff xác nhận thanh toán mới active.
- **FR-PAY-01 (Unwanted):** IF ghi payment trùng kỳ cho cùng membership, THEN THE system SHALL từ chối với 409.
- **FR-MS-05 (State):** WHILE Membership chưa `Active` (PendingPayment), THE system SHALL từ chối check-in cho Member đó (liên kết spec 004).
- **FR-MS-06 (Ubiquitous):** THE system SHALL ghi AuditLog cho mọi hành động bán/gia hạn/ghi payment.

## 4. Non-functional Requirements
- **NFR-01:** Thao tác bán gói < 500ms.
- **NFR-02:** Tính tiền dùng DECIMAL(12,2), không dùng float.
- **NFR-03:** Mọi bước có audit + truy vết người thực hiện (CreatedBy từ JWT).

## 5. Data Model
- **MembershipPackages**(Id, Name, DurationDays, Price DECIMAL(12,2), Status{Active,Inactive}, IsDeleted, CreatedAt, UpdatedAt)
- **Memberships**(Id, MemberId→MemberProfiles, PackageId→MembershipPackages, StartDate, EndDate, Status{PendingPayment,Active,Expired,Cancelled}, CreatedAt, UpdatedAt)
- **Payments**(Id, MembershipId→Memberships, Amount, Method{Cash,Transfer,Card}, Status{Pending,Paid,Refunded}, PaidAt, CreatedBy→Users, CreatedAt)
- Trạng thái map TINYINT+CHECK (SQL Server). Xem `15_DATABASE_SCHEMA.md` §2.4–2.5.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/packages | Admin | {name, durationDays, price} | 201 | 400 |
| PUT | /api/v1/packages/{id} | Admin | {…, status} | 200 | 400, 404 |
| GET | /api/v1/packages | Admin, Staff | — | 200 | 401 |
| POST | /api/v1/memberships/sell | Admin, Staff | {memberId, packageId, startDate} | 201 {membership: PendingPayment} | 400, 404, 422 |
| POST | /api/v1/memberships/{id}/payment | Admin, Staff | {amount, method} | 201 {status: Active} | 404, 409, 422 |
| POST | /api/v1/memberships/{id}/renew | Admin, Staff | {packageId, method} | 201 | 404, 422 |
| POST | /api/v1/memberships/renewal-request | Member | {packageId} | 201 (request) | 422 |
| GET | /api/v1/memberships | Admin, Staff | query: status?, page? | 200 (paged MembershipResponse) | 401, 403 |
| GET | /api/v1/payments | Admin, Staff | query: from?, to?, status?, memberId?, page=1, pageSize=50 | 200 (paged PaymentHistoryResponse) | 401, 403 |
| GET | /api/v1/payments/summary | Admin, Staff | query: from?, to? | 200 (PaymentSummaryResponse) | 401, 403 |

### 6.1. Response contract cho FE (đúng field code trả — JSON camelCase)

> Casing enum (`status`, `method`, `paymentMethod`) trả **PascalCase** (`Active`/`Paid`/`Cash`/`Transfer`/`Card`) đồng bộ toàn hệ thống — FE tự map nếu cần.

**MembershipResponse** (dùng ở `sell`, `payment`, `renew`, `GET /memberships`):
```json
{ "id", "memberId", "packageId", "packageName", "startDate", "endDate",
  "status", "daysRemaining", "isExpiringSoon", "createdAt" }
```

**`POST /memberships/{id}/payment` → ConfirmPaymentResult:**
```json
{ "membership": { …MembershipResponse }, "payment": { "id", "membershipId", "paidAt" }, "status" }
```

**`GET /payments` → mảng PaymentHistoryResponse:**
```json
{ "id", "membershipId", "memberId", "memberName", "memberEmail",
  "packageId", "packageName", "amount", "paymentMethod", "status",
  "paymentDate", "paidAt", "createdAt", "createdByUserId", "createdByName" }
```

**`GET /payments/summary` → PaymentSummaryResponse (báo cáo doanh thu):**
```json
{ "from", "to", "totalPayments", "paidPayments", "pendingPayments", "revenue",
  "byMethod": [ { "paymentMethod", "count", "amount" } ],
  "byDay":    [ { "date", "count", "amount" } ] }
```

## 7. Error Handling (EARS Unwanted)
- IF gói/Member không tồn tại, THEN 404 `NOT_FOUND`.
- IF bán gói Inactive, THEN 422 `PACKAGE_INACTIVE`.
- IF StartDate không hợp lệ (quá khứ xa), THEN 422 `INVALID_START_DATE`.
- IF ghi payment trùng kỳ, THEN 409 `DUPLICATE_PAYMENT`.
- IF Amount < giá gói (theo policy), THEN 422 `INSUFFICIENT_AMOUNT`.
- IF Member cố tự kích hoạt membership, THEN 403 `FORBIDDEN`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given gói 30 ngày, When Staff bán với StartDate=01/06, Then Membership PendingPayment, EndDate=01/07.
- [ ] **AC-02:** Given Membership PendingPayment, When ghi payment Paid, Then status=Active + AuditLog `CONFIRM_PAYMENT`.
- [ ] **AC-03:** Given Member đang Active đến 01/07, When gia hạn gói 30 ngày, Then EndDate mới=31/07 (nối tiếp, không trùng).
- [ ] **AC-04:** Given Membership PendingPayment, When Member check-in, Then bị từ chối.
- [ ] **AC-05:** Given payment đã ghi, When ghi lại trùng kỳ, Then 409.
- [ ] **AC-06:** Given Member gửi yêu cầu gia hạn, When chưa Admin/Staff xác nhận, Then chưa Active.

## 9. Out of Scope
- Payment gateway tự động, hóa đơn điện tử/VAT, trả góp, hoàn tiền tự động, khuyến mãi/voucher (secondary).

## 10. Cập nhật triển khai (2026-06-12)
> Addendum ghi nhận quyết định khi code (BE-first, bám spec). KHÔNG sửa thân spec đã duyệt ở trên.

- **Gia hạn (FR-MS-03) = kéo dài tại chỗ.** Vì ràng buộc "1 Active/member", `renew` KHÔNG tạo membership PendingPayment riêng mà **kéo dài `EndDate` của membership hiện tại** (nối tiếp từ EndDate cũ nếu còn hạn, từ hôm nay nếu đã hết) và **ghi nhận thanh toán ngay** với phương thức từ request. Request đổi `{packageId}` → **`{packageId, method}`**.
- **`method` nhận tên enum dạng chuỗi** (`"cash"`/`"transfer"`/`"card"`), parse không phân biệt hoa thường — áp dụng cho cả `confirm payment` lẫn `renew`. Đã **bỏ hardcode `Cash`** ở gia hạn.
- **Chặn gia hạn gói đã hủy**: nếu membership ở trạng thái `Cancelled`, `renew` trả `422 MEMBERSHIP_CANCELLED` (không hồi sinh gói đã hủy).
- **Mã lỗi §7 đã khớp code**: `INVALID_START_DATE` (StartDate < hôm nay), `INSUFFICIENT_AMOUNT` (Amount < giá gói), `DUPLICATE_PAYMENT` (ghi thanh toán cho membership đã Active).
- **Response §6**: `sell` → `{ membership }`, `payment` → `{ membership, payment, status }`. Trạng thái serialize theo **tên enum (PascalCase)**; FE tự map casing nếu cần (không hạ chuẩn BE theo FE).
- **Online banking / payment gateway = tương lai** (ADR-03 vẫn thủ công ở MVP). Giữ code mở rộng: thêm `PaymentMethod.Online` + webhook (file mới) + cột `provider_ref` nullable (DB team) khi làm thật — KHÔNG over-engineer trước.
- **`GET /api/v1/memberships` (roster, ngoài §6)**: đổi từ list phẳng → **`PagedResult`** `{ items, page, pageSize, totalItems, totalPages }`, `pageSize = 20` (đồng bộ với `members`/`users`). **FE phải đọc `.items`** thay vì coi response là mảng.
- **Test**: 8 unit test (xUnit + EF Core InMemory) ở `tests/GymMaster.Api.Tests` — sell/payment/renew, INVALID_START_DATE/INSUFFICIENT_AMOUNT/DUPLICATE_PAYMENT, MEMBERSHIP_CANCELLED, PagedResult.
