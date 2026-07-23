# Feature Specification: Member Check-in

**Feature Branch**: `004-checkin`
**Created**: 2026-05-30
**Status**: Implemented (spec đồng bộ theo code 2026-07-15)
**Spec style**: SDD + Spec Kit — 9 components, EARS notation
**Source**: archive/06_FEATURE_SPECS (F2), srs-use-cases (UC-09), requirements (FR-CHK), OQ-06

> EARS legend như spec 001. Mọi path dưới `/api/v1`. "Hôm nay" tính theo **giờ VN (GMT+7, `AppClock`)**.

---

## 1. Context & Goal
Ghi nhận lượt đến phòng tập, (tuỳ cấu hình) xác thực gói còn hạn, làm đầu vào thống kê dashboard. Staff check-in tại quầy, Member tự check-in, PT check-in cho hội viên được phân công. Mục tiêu: check-in nhanh (≤3 click), chống check-in hộ, giới hạn số lần/ngày.

## 2. Actors
| Actor | Vai trò |
|---|---|
| Staff/Admin | Check-in tại quầy cho member (theo MemberId/SĐT) |
| Member | Tự check-in cho chính mình |
| PT | Check-in cho hội viên đang được phân công active cho mình |
| System | Xác thực (tài khoản/membership/giới hạn ngày), tạo bản ghi |

## 3. Functional Requirements (EARS)
- **FR-CHK-01 (Event):** WHEN check-in hợp lệ, THE system SHALL tạo CheckIn với timestamp UTC.
- **FR-CHK-02 (Unwanted, cấu hình):** WHERE `CheckIn:EnforceMembership=true` (mặc định `false`), IF Member không có Membership `Active` còn hạn, THEN THE system SHALL từ chối: còn đơn `PendingPayment` → 422 `PAYMENT_PENDING`; ngược lại 422 `NO_ACTIVE_MEMBERSHIP` (nhắc gia hạn).
- **FR-CHK-03 (Optional):** WHERE cấu hình giới hạn `CheckIn:MaxPerDay` (mặc định **2**; `OncePerDay=true` ⇒ 1; ≤0 ⇒ không giới hạn), THE system SHALL chặn khi đã đủ lượt trong ngày (theo giờ VN) với 409 `DAILY_LIMIT_REACHED`.
- **FR-CHK-04 (Unwanted):** IF MemberId/SĐT không khớp Member nào, THEN 404 `MEMBER_NOT_FOUND`.
- **FR-CHK-05 (State):** WHILE tài khoản Member bị Locked (status `locked` hoặc `LockedUntil` còn hiệu lực), THE system SHALL từ chối check-in (403 `ACCOUNT_LOCKED`).
- **FR-CHK-06 (Ubiquitous):** THE system SHALL lưu người thực hiện (`CreatedBy`): **null** khi member tự check-in (source `"member"`); = userId của Staff/PT khi tác nghiệp hộ (source `"front-desk"`).
- **FR-CHK-07 (Optional):** WHERE người gọi là Member (không phải staff/admin), THE system SHALL chỉ cho tự check-in cho chính mình (`profile.UserId == actor`), ngược lại 403.
- **FR-CHK-08 (Optional):** WHERE người gọi là PT (`/pt/members/{id}/checkins`), THE system SHALL chỉ cho check-in hội viên có assignment `Active` với PT đó (403 nếu không).

## 4. Non-functional Requirements
- **NFR-01:** Check-in < 300ms (P95), ≤ 3 click.
- **NFR-02:** Timestamp lưu UTC (đọc ra ép `Kind=Utc` để FE hiển thị đúng giờ địa phương). Khung "ngày" reset lúc nửa đêm **giờ VN**.
- **NFR-03:** Chịu tải giờ cao điểm (~50 check-in/phút).

## 5. Data Model
- **check_ins**(Id, MemberId→member_profiles, CheckInAt DATETIME2 UTC, CreatedBy→users nullable)
- Index: (MemberId, CheckInAt). Không có mã hội viên riêng → tra cứu theo SĐT (`users.Phone`) khi cần.

## 6. API Spec
| Method | Path | Role | Request | Success | Lỗi |
|---|---|---|---|---|---|
| POST | /api/v1/checkins | Admin, Staff, Member(self) | {memberId? \| memberCode?(=SĐT), source?} | 201 CheckInResponse | 403, 404, 409, 422 |
| GET | /api/v1/checkins?date=&memberId= | Admin, Staff | — | 200 (list, kèm memberName) | 401, 403 |
| GET | /api/v1/members/{id}/checkins | Admin, Staff, PT, Member(self) | — | 200 | 403, 404 |
| POST | /api/v1/pt/members/{memberId}/checkins | PT(assigned) | — | 201 | 403, 404, 409, 422 |
| GET | /api/v1/pt/checkins/today | PT | — | 200 (check-in hôm nay của member được phân công) | 401, 404 |

> **CheckInResponse:** `{ id, memberId, checkInAt (UTC 'Z'), source: "member"|"front-desk", memberName? }`. `memberName` chỉ điền ở các endpoint LIST.

## 7. Error Handling (EARS Unwanted)
- IF Member không tồn tại, THEN 404 `MEMBER_NOT_FOUND`.
- IF (EnforceMembership) không có gói Active còn hạn, THEN 422 `NO_ACTIVE_MEMBERSHIP`.
- IF (EnforceMembership) còn đơn PendingPayment, THEN 422 `PAYMENT_PENDING`.
- IF tài khoản Locked, THEN 403 `ACCOUNT_LOCKED`.
- IF đã đủ lượt trong ngày, THEN 409 `DAILY_LIMIT_REACHED`.
- IF Member tự check-in hộ người khác, THEN 403 `FORBIDDEN`.
- IF PT check-in member không được phân công, THEN 403 `FORBIDDEN`; IF PT chưa có hồ sơ, THEN 404 `TRAINER_NOT_FOUND`.

## 8. Acceptance Criteria (Given-When-Then)
- [ ] **AC-01:** Given Member (giới hạn 2/ngày), When check-in, Then tạo CheckIn và hiện trong dashboard hôm nay.
- [ ] **AC-02:** Given EnforceMembership bật + Member hết hạn, When check-in, Then 422 `NO_ACTIVE_MEMBERSHIP`.
- [ ] **AC-03:** Given EnforceMembership bật + Membership PendingPayment, When check-in, Then 422 `PAYMENT_PENDING`.
- [ ] **AC-04:** Given SĐT không hợp lệ, When check-in, Then 404.
- [ ] **AC-05:** Given đã check-in đủ MaxPerDay hôm nay, When check-in lần nữa, Then 409 `DAILY_LIMIT_REACHED`.
- [ ] **AC-06:** Given Member tự check-in, When tạo bản ghi, Then `CreatedBy=null`, source=`"member"`.
- [ ] **AC-07:** Given PT, When check-in cho member được phân công, Then 201; cho member người khác → 403.

## 9. Out of Scope
- Phần cứng quét vân tay/thẻ từ/cổng xoay, check-out (ra về), đếm thời gian lưu lại phòng.
