# Feature Specification: Operations Dashboard & Audit Log

**Feature Branch**: `008-dashboard-audit`
**Created**: 2026-05-30
**Status**: Approved
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 06_FEATURE_SPECS (F5), 03_SRS (UC-22, UC-23), 04 (FR-DASH/AUD), CONSTITUTION AUDIT-01

> EARS legend như spec 001.

---

## 1. Context & Goal
Admin xem dashboard vận hành (doanh thu, membership active/expired, check-in) từ **dữ liệu thật** (không mock), và tra cứu audit log mọi hành động mutating quan trọng. Mục tiêu: ra quyết định vận hành + truy vết.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Admin | Xem dashboard + audit log |
| System | Tổng hợp aggregate, ghi audit log từ các feature khác |

## 3. Functional Requirements (EARS)
- **FR-DASH-01 (Event):** WHEN Admin mở dashboard, THE system SHALL trả số liệu tính từ dữ liệu thật: tổng doanh thu kỳ, số membership Active, số Expired, số check-in theo ngày.
- **FR-DASH-02 (Event):** WHEN Admin lọc theo khoảng ngày, THE system SHALL tính lại các chỉ số trong khoảng đó.
- **FR-DASH-03 (Unwanted):** IF không có dữ liệu trong kỳ, THEN THE system SHALL trả giá trị 0, KHÔNG báo lỗi.
- **FR-DASH-04 (Unwanted):** IF người gọi không phải Admin, THEN THE system SHALL trả 403.
- **FR-AUD-01 (Ubiquitous):** THE system SHALL ghi AuditLog cho mọi hành động mutating quan trọng (bán/gia hạn/payment, phân công PT, tạo/sửa/xóa tài khoản, đổi trạng thái) gồm: ai, hành động, đối tượng, thời gian, metadata trước/sau.
- **FR-AUD-02 (Event):** WHEN Admin tra cứu audit log với bộ lọc (người dùng, hành động, khoảng ngày), THE system SHALL trả danh sách phân trang theo thời gian giảm dần.
- **FR-AUD-03 (Ubiquitous):** THE system SHALL KHÔNG ghi dữ liệu nhạy cảm (mật khẩu, token, PII đầy đủ) vào metadata audit.

## 4. Non-functional Requirements
- **NFR-01:** Dashboard load < 2s với ~1000 hội viên.
- **NFR-02:** Aggregate query có index phù hợp (Payments.CreatedAt, Memberships.Status, CheckIns.CheckInAt).
- **NFR-03:** Audit log chỉ đọc (append-only); không cho sửa/xóa qua API.

## 5. Data Model
- **AuditLogs**(Id, UserId→Users, Action NVARCHAR(100), Entity NVARCHAR(60), EntityId, Metadata NVARCHAR(MAX) JSON, CreatedAt DATETIME2)
- Dashboard là read-model aggregate từ Payments, Memberships, CheckIns. Xem `15_DATABASE_SCHEMA.md` §2.8.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| GET | /api/dashboard/summary?from=&to= | Admin | — | 200 {revenue, activeCount, expiredCount, checkinsByDay[]} | 401, 403 |
| GET | /api/audit-logs?userId=&action=&from=&to=&page= | Admin | — | 200 (paged) | 401, 403 |

## 7. Error Handling (EARS Unwanted)
- IF không phải Admin, THEN 403 `FORBIDDEN`.
- IF khoảng ngày không hợp lệ (from > to), THEN 422 `INVALID_RANGE`.
- IF không có dữ liệu kỳ, THEN 200 với các chỉ số = 0 (không lỗi).

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given có payment/membership/check-in thật, When Admin mở dashboard, Then số liệu khớp DB.
- [ ] **AC-02:** Given chọn khoảng ngày, When áp dụng, Then chỉ số tính đúng trong khoảng.
- [ ] **AC-03:** Given Member/PT, When gọi dashboard, Then 403.
- [ ] **AC-04:** Given kỳ trống, When mở dashboard, Then trả 0, không lỗi.
- [ ] **AC-05:** Given Staff bán gói, When tra audit, Then có bản ghi `SELL_MEMBERSHIP` với người thực hiện + thời gian.
- [ ] **AC-06:** Given audit log, When kiểm tra metadata, Then không chứa mật khẩu/token.

## 9. Out of Scope
- Dashboard realtime/websocket, export báo cáo PDF/Excel nâng cao (secondary), biểu đồ dự báo, gửi báo cáo định kỳ qua email.
