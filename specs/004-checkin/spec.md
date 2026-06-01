# Feature Specification: Member Check-in

**Feature Branch**: `004-checkin`
**Created**: 2026-05-30
**Status**: Approved
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: 06_FEATURE_SPECS (F2), 03_SRS (UC-09), 04 (FR-CHK), OQ-06

> EARS legend như spec 001.

---

## 1. Context & Goal
Ghi nhận lượt đến phòng tập, xác thực gói còn hạn, làm đầu vào thống kê dashboard. Mục tiêu: check-in nhanh (≤3 click), chặn member hết hạn/chưa thanh toán.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Staff | Check-in tại quầy cho member |
| Member | Tự check-in (tìm theo mã/SĐT) |
| System | Xác thực membership, tạo bản ghi, cập nhật thống kê |

## 3. Functional Requirements (EARS)
- **FR-CHK-01 (Event):** WHEN Member có Membership `Active` còn hạn thực hiện check-in, THE system SHALL tạo CheckIn với timestamp UTC.
- **FR-CHK-02 (Unwanted):** IF Member không có Membership `Active` còn hạn, THEN THE system SHALL từ chối check-in và hiển thị nhắc gia hạn.
- **FR-CHK-03 (Optional):** WHERE cấu hình giới hạn 1 lần/ngày được bật, THE system SHALL chặn check-in thứ 2 trong cùng ngày (mặc định MVP: cho nhiều lần/ngày — OQ-06).
- **FR-CHK-04 (Unwanted):** IF mã/SĐT không khớp Member nào, THEN THE system SHALL từ chối với 404.
- **FR-CHK-05 (State):** WHILE tài khoản Member bị Locked, THE system SHALL từ chối check-in.
- **FR-CHK-06 (Ubiquitous):** THE system SHALL lưu người thực hiện (CreatedBy) — null nếu member tự check-in.

## 4. Non-functional Requirements
- **NFR-01:** Check-in < 300ms (P95), ≤ 3 click.
- **NFR-02:** Timestamp lưu UTC, hiển thị theo giờ địa phương ở UI.
- **NFR-03:** Chịu tải giờ cao điểm (~50 check-in/phút).

## 5. Data Model
- **CheckIns**(Id, MemberId→MemberProfiles, CheckInAt DATETIME2 UTC, CreatedBy→Users nullable)
- Index: (MemberId, CheckInAt). Tham chiếu Memberships để xác thực. Xem `15_DATABASE_SCHEMA.md` §2.6.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/checkins | Staff, Member(self) | {memberId \| memberCode} | 201 {checkInAt} | 403, 404, 422 |
| GET | /api/checkins?date=&memberId= | Admin, Staff | — | 200 (list) | 401, 403 |
| GET | /api/members/{id}/checkins | Admin, Staff, PT(assigned), Member(self) | — | 200 | 403, 404 |

## 7. Error Handling (EARS Unwanted)
- IF Member không tồn tại, THEN 404 `MEMBER_NOT_FOUND`.
- IF không có membership Active còn hạn, THEN 422 `NO_ACTIVE_MEMBERSHIP` kèm message gia hạn.
- IF membership PendingPayment, THEN 422 `PAYMENT_PENDING`.
- IF tài khoản Locked, THEN 403 `ACCOUNT_LOCKED`.
- IF (cấu hình bật) đã check-in trong ngày, THEN 409 `ALREADY_CHECKED_IN_TODAY`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given Member Active còn hạn, When check-in, Then tạo CheckIn và hiện trong dashboard hôm nay.
- [ ] **AC-02:** Given Member hết hạn, When check-in, Then 422 + nhắc gia hạn.
- [ ] **AC-03:** Given Membership PendingPayment, When check-in, Then 422 PAYMENT_PENDING.
- [ ] **AC-04:** Given mã không hợp lệ, When check-in, Then 404.
- [ ] **AC-05:** Given giới hạn 1 lần/ngày bật + đã check-in, When check-in lần 2, Then 409.

## 9. Out of Scope
- Phần cứng quét vân tay/thẻ từ/cổng xoay, check-out (ra về), đếm thời gian lưu lại phòng.
