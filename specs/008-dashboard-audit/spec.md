# Feature Specification: Operations Dashboard & Audit Log

**Feature Branch**: `008-dashboard-audit`
**Created**: 2026-05-30
**Status**: Implemented (spec đồng bộ theo code 2026-07-15)
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 06_FEATURE_SPECS (F5), 03_SRS (UC-22, UC-23), 04 (FR-DASH/AUD), CONSTITUTION AUDIT-01

> EARS legend như spec 001. Mọi path dưới `/api/v1`. Mốc ngày/tháng tính theo giờ VN (`AppClock`).

---

## 1. Context & Goal
Admin xem dashboard vận hành (doanh thu, membership active/expired, check-in, tải cơ sở, giờ cao điểm) từ **dữ liệu thật** (không mock), và tra cứu audit log mọi hành động mutating quan trọng. Mục tiêu: ra quyết định vận hành + truy vết.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Admin | Xem dashboard + audit log |
| System | Tổng hợp aggregate, ghi audit log từ mọi feature |

## 3. Functional Requirements (EARS)
- **FR-DASH-01 (Event):** WHEN Admin mở dashboard, THE system SHALL trả số liệu tính từ dữ liệu thật: doanh thu kỳ (Paid), số membership Active, số Expired, check-in hôm nay, thanh toán chờ xử lý (số tiền + số đơn).
- **FR-DASH-02 (Event):** WHEN Admin lọc theo khoảng ngày (`from`/`to`), THE system SHALL tính lại doanh thu kỳ trong khoảng đó (mặc định = tháng này theo giờ VN).
- **FR-DASH-03 (Ubiquitous — chỉ số phái sinh):** THE system SHALL trả kèm: doanh thu 6 tháng gần nhất (`revenueByMonth`, gom theo tháng VN), hội viên hết hạn gần đây (top 10, `recentlyExpired`), tải cơ sở (`facilityLoadPercent` = check-in hôm nay / sức chứa 50), `ptSessionPercent`/`generalAreaPercent`, doanh thu tháng trước (`previousMonthRevenue`), số membership mới trong tháng (`newMembershipsThisMonth`), giờ cao điểm (`peakHourStart/End`, tính 30 ngày gần nhất theo giờ VN).
- **FR-DASH-04 (Unwanted):** IF không có dữ liệu trong kỳ, THEN trả 0, KHÔNG báo lỗi.
- **FR-DASH-05 (Unwanted):** IF người gọi không phải Admin, THEN 403.
- **FR-AUD-01 (Ubiquitous):** THE system SHALL ghi AuditLog cho mọi hành động mutating quan trọng (bán/gia hạn/payment/huỷ, phân công PT, tạo/sửa/xoá tài khoản, đổi trạng thái, check-in, giáo án/ghi chú, food AI…) gồm: ai (`UserId` từ JWT), hành động, đối tượng (`Entity`/`EntityId`), thời gian, metadata JSON.
- **FR-AUD-02 (Event):** WHEN Admin tra cứu audit log với bộ lọc (userId, action, from, to, search), THE system SHALL trả `PagedResult` theo thời gian giảm dần, kèm `userDisplayName`.
- **FR-AUD-03 (Ubiquitous):** THE system SHALL KHÔNG ghi dữ liệu nhạy cảm (mật khẩu, token, PII đầy đủ) vào metadata.

## 4. Non-functional Requirements
- **NFR-01:** Dashboard load < 2s với ~1000 hội viên.
- **NFR-02:** Aggregate query có index (payments.MembershipId, memberships.MemberId, check_ins (MemberId,CheckInAt), audit_logs (Entity,EntityId)).
- **NFR-03:** Audit log append-only; không sửa/xoá qua API.

## 5. Data Model
- **audit_logs**(Id, UserId→users nullable, Action NVARCHAR(100), Entity NVARCHAR(60), EntityId, Metadata NVARCHAR(MAX) JSON, CreatedAt DATETIME2) — index (Entity, EntityId).
- Dashboard là read-model aggregate từ payments, memberships, check_ins, trainer_assignments.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| GET | /api/v1/dashboard/summary?from=&to= | Admin | — | 200 DashboardSummaryResponse | 401, 403, 422 |
| GET | /api/v1/audit-logs?userId=&action=&from=&to=&search=&page=&pageSize= | Admin | — | 200 (PagedResult AuditLogResponse) | 401, 403 |

**DashboardSummaryResponse:** `{ revenue, activeCount, expiredCount, checkinsByDay:[{date,count}], pendingPaymentAmount, pendingPaymentCount, revenueByMonth:[{month:"T6",revenue}], recentlyExpired:[{initials,memberName,packageName,expiredDate}], facilityLoadPercent, ptSessionPercent, generalAreaPercent, previousMonthRevenue, newMembershipsThisMonth, peakHourStart, peakHourEnd }`.
**AuditLogResponse:** `{ id, userId, userDisplayName, action, entityType, entityId, metadata, createdAt }`.

## 7. Error Handling (EARS Unwanted)
- IF không phải Admin, THEN 403.
- IF khoảng ngày không hợp lệ (from > to), THEN 422 `INVALID_RANGE`.
- IF không có dữ liệu kỳ, THEN 200 với các chỉ số = 0 (không lỗi).

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given có payment/membership/check-in thật, When Admin mở dashboard, Then số liệu khớp DB.
- [ ] **AC-02:** Given chọn khoảng ngày, When áp dụng, Then doanh thu kỳ tính đúng trong khoảng.
- [ ] **AC-03:** Given Member/PT/Staff, When gọi dashboard, Then 403.
- [ ] **AC-04:** Given kỳ trống, When mở dashboard, Then trả 0, không lỗi.
- [ ] **AC-05:** Given Staff bán gói, When tra audit, Then có bản ghi `SELL_MEMBERSHIP` với người thực hiện + thời gian.
- [ ] **AC-06:** Given audit log, When kiểm tra metadata, Then không chứa mật khẩu/token.
- [ ] **AC-07:** Given có check-in 30 ngày qua, When mở dashboard, Then `peakHourStart/End` phản ánh giờ đông nhất (giờ VN).

## 9. Out of Scope
- Dashboard realtime/websocket, export PDF/Excel nâng cao, biểu đồ dự báo, gửi báo cáo định kỳ qua email.
