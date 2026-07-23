---
description: "Task list — Member Check-in"
---

# Tasks: Member Check-in

**Feature**: `004-checkin`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 22/23 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code, `[ ]` = còn nợ.

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 Tạo slice `backend/GymMaster.API/Features/CheckIns/`
- [X] T002 [P] `Options/CheckInOptions.cs` — `EnforceMembership` (mặc định `false`), `MaxPerDay` (mặc định `2`), `OncePerDay` + binding trong `Program.cs`

## Phase 2: Foundational

- [X] T003 `Entities/CheckIn.cs` — `Id`, `MemberId`, `CheckInAt DATETIME2` (UTC), `CreatedBy` **nullable**
- [X] T004 Index `(MemberId, CheckInAt)` trong `Data/GymMasterDbContext.cs` → phục vụ NFR-03
- [X] T005 Migration `database/011_fix_check_ins_createdby_column.sql`
- [X] T006 Đăng ký DI `ICheckInService` → `CheckInService` trong `Program.cs`

**Checkpoint**: có bảng + cấu hình → các user story bắt đầu được.

---

## Phase 3: US1 — Check-in tại quầy (P1) 🎯 MVP

**Goal**: Staff ghi nhận hội viên đến tập trong ≤ 3 click.
**Independent Test**: `POST /checkins {memberCode: "<SĐT>"}` → 201 với `source="front-desk"`; SĐT không tồn tại → 404.

- [X] T007 [US1] `Features/CheckIns/CheckInDtos.cs` — `CheckInResponse { id, memberId, checkInAt (UTC 'Z'), source, memberName? }`
- [X] T008 [US1] `CheckInService.CreateAsync` — INSERT với `CheckInAt = UtcNow` → **FR-CHK-01**
- [X] T009 [US1] Tra member theo `memberId` **hoặc** `memberCode` (= SĐT trên `users.Phone`) → 404 `MEMBER_NOT_FOUND` → **FR-CHK-04**
- [X] T010 [US1] Ghi `CreatedBy` = userId của Staff/PT khi tác nghiệp hộ, `null` khi member tự check-in → **FR-CHK-06**
- [X] T011 [US1] `CheckInsController.cs` route `api/v1/checkins`, `[Authorize(Roles="admin,staff,member")]`
- [X] T012 [US1] `GET /checkins?date=&memberId=` (Admin/Staff) — trả kèm `memberName`
- [X] T013 [US1] Ép `Kind=Utc` khi đọc ra để FE hiển thị đúng giờ địa phương → **NFR-02**

**Checkpoint**: quầy ghi nhận được lượt đến → dashboard (spec 008) có dữ liệu.

---

## Phase 4: US2 — Các lớp gác cửa (P1)

**Goal**: chống check-in hộ, chống spam, chống người hết gói vẫn vào.
**Independent Test**: bật `EnforceMembership` rồi check-in member hết hạn → 422 `NO_ACTIVE_MEMBERSHIP`; check-in lần thứ 3 trong ngày → 409 `DAILY_LIMIT_REACHED`.

- [X] T014 [US2] Chặn tài khoản Locked (`status = locked` hoặc `LockedUntil` còn hiệu lực) → 403 `ACCOUNT_LOCKED` → **FR-CHK-05**
- [X] T015 [US2] Ownership: Member chỉ tự check-in cho chính mình (`profile.UserId == CurrentUserId`), khác → 403 → **FR-CHK-07**
- [X] T016 [US2] Gác membership khi `EnforceMembership = true`, **dùng lại** `Features/Billing/MembershipLifecycle.IsActiveOn` (không viết lại logic) → **FR-CHK-02**, **ARCH-03**
- [X] T017 [US2] Phân biệt 2 lý do từ chối: còn đơn `PendingPayment` → 422 `PAYMENT_PENDING`; không có gói → 422 `NO_ACTIVE_MEMBERSHIP` → **FR-CHK-02**
- [X] T018 [US2] Giới hạn lượt/ngày đếm theo **giờ VN** (`AppClock`): `MaxPerDay` (`OncePerDay=true` ⇒ 1; `≤0` ⇒ không giới hạn) → 409 `DAILY_LIMIT_REACHED` → **FR-CHK-03**
- [X] T019 [US2] Unit test `tests/GymMaster.Api.Tests/CheckInServiceTests.cs`

---

## Phase 5: US3 — Check-in cho PT (P2)

**Goal**: PT điểm danh hội viên mình đang kèm và biết ai đã đến hôm nay.
**Independent Test**: PT check-in member được phân công → 201; member không được phân công → 403.

- [X] T020 [US3] `POST /pt/members/{memberId}/checkins` trong `Features/Training/PtController.cs` — chỉ cho member có assignment `Active` với PT đó → **FR-CHK-08**
- [X] T021 [US3] PT chưa có hồ sơ `trainer_profiles` → 404 `TRAINER_NOT_FOUND`
- [X] T022 [US3] `GET /pt/checkins/today` — danh sách check-in hôm nay của hội viên được phân công
- [X] T023 [US3] `GET /members/{id}/checkins` — Admin/Staff/PT/Member(self), có kiểm ownership

---

## Phase 6: Polish & Cross-cutting

- [ ] T025 **Còn nợ** — đo NFR-01 (< 300ms P95) và NFR-03 (~50 check-in/phút) bằng `tests/blackbox/Performance.Tests.ps1`, ghi số đo vào `docs/04-Test-Specs/test-plan.md`

---

## Dependencies & Execution Order

- **Phụ thuộc ngoài**: spec 001 (`users.Status`/`LockedUntil`), spec 002 (`member_profiles`, `users.Phone`), spec 003 (`MembershipLifecycle`), spec 005 (bảng phân công PT cho US3).
- **US1 → US2**: các lớp gác cắm vào cùng một `CreateAsync` của US1.
- **US3** cần spec 005 đã có `trainer_assignments`.
- **Feature phụ thuộc ngược**: spec 008 (Dashboard) đọc `check_ins` để đếm lượt đến.

```text
[001·002·003] → Setup → Foundational → US1 → US2 → Polish
[005] ──────────────────────────────────────→ US3
                        ↓
              [008 Dashboard đọc check_ins]
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01 | T008, T012 | `CheckInServiceTests.cs` |
| AC-02 | T016 | `CheckInServiceTests.cs` |
| AC-03 | T017 | `CheckInServiceTests.cs` |
| AC-04 | T009 | `CheckInServiceTests.cs` |
| AC-05 | T018 | `CheckInServiceTests.cs` |
| AC-06 | T010 | `CheckInServiceTests.cs` |
| AC-07 | T020 | black-box (cần token PT + assignment thật) |
