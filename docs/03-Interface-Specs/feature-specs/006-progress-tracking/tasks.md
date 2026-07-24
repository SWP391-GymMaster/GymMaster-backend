---
description: "Task list — Progress Tracking & Member 360° Profile"
---

# Tasks: Progress Tracking & Member 360° Profile

**Feature**: `006-progress-tracking`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 26/28 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code, `[ ]` = còn nợ.

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 Đặt feature trong slice sẵn có `backend/GymMaster.API/Features/Training/` (dùng chung cửa quyền PT của spec 005)

## Phase 2: Foundational

- [X] T002 `Entities/ProgressLog.cs` — `WeightKg`, `BodyFatPercent`, `ChestCm`, `WaistCm`, `HipCm` kiểu `DECIMAL(5,2)`, `Note NVARCHAR(500)`, `CreatedByUserId`
- [X] T003 Index `(MemberId, MeasuredAt)` trong `Data/GymMasterDbContext.cs`
- [X] T004 Đăng ký DI `IProgressService` → `ProgressService` trong `Program.cs`

---

## Phase 3: US1 — Ghi & xem tiến độ (P1) 🎯 MVP

**Goal**: hội viên/PT ghi số đo theo thời gian và xem được biểu đồ tiến bộ.
**Independent Test**: `POST /members/{id}/progress {weightKg: 70}` → 201; ghi lại cùng ngày `{weightKg: 71}` → 200 và timeline vẫn chỉ có 1 điểm cho ngày đó; ghi `weightKg: 5` → 422.

- [X] T005 [US1] `ProgressDtos.cs` — `ProgressLogResponse`
- [X] T006 [US1] `ProgressService.CreateAsync` — lưu `ProgressLog` → **FR-PROG-01**
- [X] T007 [US1] Validate khoảng: Weight 20–300kg · BodyFat 0–70% · Chest/Waist/Hip 30–200cm → 422 `INVALID_MEASUREMENT`
- [X] T008 [US1] Từ chối khi **không có chỉ số nào** → 422 `INVALID_MEASUREMENT`
- [X] T009 [US1] Chặn `measuredAt` ở tương lai (so theo **giờ VN**, `AppClock`) → 422
- [X] T010 [US1] Giới hạn `note` ≤ 500 ký tự → **NFR-03**
- [X] T011 [US1] ★ Quy tắc **1 ngày = 1 bản ghi**: đã có bản ghi cùng ngày thì **UPDATE (200)**, chưa có thì **INSERT (201)** → **FR-PROG-01**, D-601
- [X] T012 [US1] Ownership: Member(self) · PT(assigned — **dùng lại** cửa quyền spec 005) · Admin/Staff → 403 → **FR-PROG-02**
- [X] T013 [US1] `GET /members/{id}/progress` — sắp xếp **tăng dần** theo `measuredAt` để FE vẽ biểu đồ → **FR-PROG-03**
- [X] T014 [US1] `MemberProgressController.cs` route `api/v1/members`
- [X] T015 [US1] Unit test `tests/GymMaster.Api.Tests/ProgressServiceTests.cs`

**Checkpoint**: có dữ liệu tiến độ → 360° mới có `progressTimeline` để hiển thị.

---

## Phase 4: US2 — Member 360° (P1)

**Goal**: một màn hình gom toàn bộ thông tin hội viên cho Staff/PT ra quyết định.
**Independent Test**: gọi `GET /members/{id}/profile-360` cho member có đủ dữ liệu → thấy membership + lịch sử + check-in + PT + tiến độ + dinh dưỡng; member chỉ còn đơn Cancelled → `currentMembership = null`.

- [X] T016 [US2] `ProgressDtos.cs` — `Profile360Response`, `Membership360` (contract camelCase, enum PascalCase)
- [X] T017 [US2] ★ `ProgressService.GetProfile360Async` — **một implementation duy nhất** cho cả 3 route → D-605
- [X] T018 [US2] Kiểm quyền: self | assigned PT | Admin | Staff → 403 → **FR-360-02**
- [X] T019 [US2] Gọi `MembershipLifecycle.ExpireIfPastDue` **trước** khi tổng hợp (spec 003) → **NFR-01**, D-606
- [X] T020 [US2] Quy tắc suy `currentMembership`: Active còn hạn (EndDate lớn nhất) → Pending mới nhất → **null**; không bao giờ bọc đơn `Cancelled`/`Expired` → **FR-360-03**
- [X] T021 [US2] `paymentStatus` suy từ bảng `payments` (Paid/Pending) — spec 003
- [X] T022 [US2] `membershipHistory` toàn bộ, sắp xếp mới→cũ
- [X] T023 [US2] `recentCheckIns` **tối đa 5**, ép `Kind=Utc` (spec 004) → **NFR-02**
- [X] T024 [US2] `assignedPT` — assignment `Active`, `id` = TrainerId, `assignedAt` ép `Kind=Utc` (spec 005), có thể null
- [X] T025 [US2] `nutritionSummary` — tính sẵn ở backend (spec 007), có thể null → D-608
- [X] T026 [US2] 3 route cùng gọi T017: `/members/{id}/profile-360` (canonical) · `/members/{id}/360` (alias) · `/members/me/profile-360`

---

## Phase 5: Polish & Cross-cutting

- [ ] T028 **Còn nợ** — đo NFR-01 (360° < 1.5s) khi member có nhiều dữ liệu; nếu vượt thì cân nhắc gộp truy vấn (hiện là ~6 query/lần gọi, chưa có số đo thực tế)
- [X] T029 Unit test `Profile360Tests.cs` — 15 test cho ma trận quyền self/assigned PT/Admin/Staff + quy tắc suy `currentMembership` (Active → Pending → null) + lazy expire trước khi trả

---

## Dependencies & Execution Order

- **US1 → US2**: 360° đọc chính bảng `progress_logs` do US1 tạo.
- **US2 phụ thuộc 5 spec khác**: 002 (hồ sơ), 003 (`MembershipLifecycle`, membership + payment), 004 (check-in), 005 (assignment PT), 007 (nutrition summary). Đây là điểm tích hợp lớn nhất hệ thống — **sửa contract ở bất kỳ spec nào trong 5 cái đó đều có khả năng làm hỏng 360°**.
- **Không có feature nào phụ thuộc ngược** vào 006 — đây là điểm cuối của chuỗi đọc.

```text
[002·003·004·005·007] ──────────────┐
                                     ↓
   Setup → Foundational → US1 → US2 (360°) → Polish
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01 | T006, T013 | `ProgressServiceTests.cs` |
| AC-02 | T012 | `ProgressServiceTests.cs` |
| AC-03 | T011 | `ProgressServiceTests.cs` |
| AC-04 | T017, T020–T025 | black-box (cần member có đủ dữ liệu 5 nguồn) |
| AC-05 | T007, T009 | `ProgressServiceTests.cs` |
| AC-06 | T020 | **chưa có unit test — T029** |
