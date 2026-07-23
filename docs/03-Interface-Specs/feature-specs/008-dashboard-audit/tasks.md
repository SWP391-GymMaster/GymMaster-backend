---
description: "Task list — Operations Dashboard & Audit Log"
---

# Tasks: Operations Dashboard & Audit Log

**Feature**: `008-dashboard-audit` | **Ngày lập**: 2026-07-23
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 24/27 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code, `[ ]` = còn nợ.

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 Tạo slice `backend/GymMaster.API/Features/Dashboard/`

## Phase 2: Foundational (hạ tầng audit — chặn 7 slice khác)

**⚠️ Đây là phase được nhiều feature phụ thuộc nhất: 002, 003, 005, 009, 010 đều inject `IAuditService`.**

- [X] T002 `Entities/AuditLog.cs` — `UserId` **nullable**, `Action NVARCHAR(100)`, `Entity NVARCHAR(60)`, `EntityId`, `Metadata NVARCHAR(MAX)` JSON, `CreatedAt`
- [X] T003 Index `(Entity, EntityId)` trong `Data/GymMasterDbContext.cs` → **NFR-02**
- [X] T004 `IAuditService.cs` — **chỉ có hàm ghi**, không có hàm sửa/xoá (append-only) → **NFR-03**, D-809
- [X] T005 `AuditService.cs` — lấy `UserId` từ JWT claim qua `IHttpContextAccessor`, service gọi **không phải truyền vào** → D-806
- [X] T006 Đăng ký DI `IAuditService` + `AddHttpContextAccessor()` trong `Program.cs`
- [X] T007 Kiểm tra các index phục vụ aggregate: `payments.MembershipId`, `memberships.MemberId`, `check_ins (MemberId, CheckInAt)` → **NFR-02**

**Checkpoint**: mọi feature khác ghi được audit → dashboard mới có dữ liệu truy vết.

---

## Phase 3: US1 — Ghi nhật ký hành động (P1) 🎯 MVP

**Goal**: mọi hành động quan trọng đều truy được ai làm, lúc nào, trên đối tượng nào.
**Independent Test**: Staff bán gói → tra `audit_logs` thấy bản ghi `SELL_MEMBERSHIP` kèm `UserId` + thời gian; kiểm metadata không chứa mật khẩu/token.

- [X] T008 [US1] Cắm `IAuditService` vào `UserService` / `MemberService` (spec 002) — `CREATE_USER`, `UPDATE_MEMBER`, `DELETE_MEMBER`, `UPDATE_AVATAR`
- [X] T009 [US1] Cắm vào `MembershipService` / `PaymentService` (spec 003) — `SELL_MEMBERSHIP`, `CONFIRM_PAYMENT`, huỷ, gia hạn
- [X] T010 [US1] Cắm vào `AssignmentService` (spec 005) — `ASSIGN_PT`
- [X] T011 [US1] Cắm vào `FoodScanService` (spec 009) và `VnPayService` (spec 010)
- [X] T012 [US1] Quy ước dựng metadata: **chỉ id + trường nghiệp vụ**, không mật khẩu/token/PII đầy đủ → **FR-AUD-03**
- [X] T013 [US1] `UserId = null` cho hành động do hệ thống thực hiện (VNPay IPN, member tự check-in) → D-807
- [X] T014 [US1] Cắm audit cho **giáo án + ghi chú** (spec 005) — `WorkoutPlanService` và `TrainerNoteService` đều inject `IAuditService`, ghi đủ 6 action `CREATE/UPDATE/DELETE`. AUDIT-01 phủ đủ **34 action / 13 service** (xem `plan.md` §7.1)

---

## Phase 4: US2 — Dashboard vận hành (P1)

**Goal**: Admin nhìn một màn hình là biết tình hình phòng gym.
**Independent Test**: tạo payment/membership/check-in thật → mở dashboard thấy số khớp DB; chọn khoảng ngày trống → mọi chỉ số = 0, không lỗi; gọi bằng token Staff → 403.

### Chỉ số cơ bản

- [X] T015 [US2] `DashboardDtos.cs` — `DashboardSummaryResponse` (15 field)
- [X] T016 [US2] `DashboardService.GetSummaryAsync` — `revenue` = Σ `payments` Status=Paid trong kỳ → **FR-DASH-01**
- [X] T017 [US2] `activeCount` / `expiredCount` từ `memberships` (đã lazy-expire — spec 003)
- [X] T018 [US2] `checkinsByDay` gom theo **ngày VN** (spec 004)
- [X] T019 [US2] `pendingPaymentAmount` / `pendingPaymentCount`
- [X] T020 [US2] Lọc `from`/`to`, mặc định = **tháng này theo giờ VN**; `from > to` → 422 `INVALID_RANGE` → **FR-DASH-02**

### Chỉ số phái sinh → **FR-DASH-03**

- [X] T021 [US2] `revenueByMonth` — 6 tháng gần nhất, gom theo tháng VN, nhãn dạng `"T6"` → D-810
- [X] T022 [US2] `recentlyExpired` — top 10 (`initials`, `memberName`, `packageName`, `expiredDate`)
- [X] T023 [US2] `facilityLoadPercent` = check-in hôm nay / **sức chứa 50** → D-804
- [X] T024 [US2] `ptSessionPercent` / `generalAreaPercent` từ `trainer_assignments` (spec 005)
- [X] T025 [US2] `previousMonthRevenue` · `newMembershipsThisMonth`
- [X] T026 [US2] `peakHourStart` / `peakHourEnd` — tính trên **30 ngày gần nhất**, gom theo giờ VN → D-805

### Ràng buộc chung

- [X] T027 [US2] Kỳ trống → trả **0**, không báo lỗi → **FR-DASH-04**
- [X] T028 [US2] `DashboardController.cs` — `[Authorize(Roles="admin")]`, role khác → 403 → **FR-DASH-05**
- [X] T029 [US2] Unit test `tests/GymMaster.Api.Tests/DashboardServiceTests.cs`

---

## Phase 5: US3 — Tra cứu audit log (P2)

**Goal**: Admin truy vết được một sự việc cụ thể.
**Independent Test**: `GET /audit-logs?action=SELL_MEMBERSHIP&from=&to=` → danh sách giảm dần theo thời gian, có `userDisplayName`.

- [X] T030 [US3] `AuditLogResponse { id, userId, userDisplayName, action, entityType, entityId, metadata, createdAt }`
- [X] T031 [US3] `AuditLogsController.cs` — lọc `userId` / `action` / `from` / `to` / `search`, phân trang, sắp xếp **giảm dần** theo thời gian → **FR-AUD-02**
- [X] T032 [US3] JOIN `users` lấy `userDisplayName` (xử lý `UserId = null` → hiển thị "Hệ thống")
- [X] T033 [US3] Chỉ Admin (`[Authorize(Roles="admin")]`); **không có** endpoint sửa/xoá → **NFR-03**

---

## Phase 6: Polish & Cross-cutting

- [X] T034 [P] Đồng bộ ngược `spec.md` theo code thật (2026-07-15)
- [ ] T035 **Còn nợ** — đo NFR-01 (dashboard < 2s với ~1000 hội viên). Đây là endpoint nặng nhất hệ thống (15 chỉ số, không cache — D-801) nhưng chưa có số đo thực tế
- [ ] T036 **Còn nợ** — cân nhắc chuyển `IAuditService` sang `Infrastructure/` nếu xuất hiện nơi tiêu thụ audit thứ hai (hiện 7 slice phải `using Features.Dashboard` chỉ để ghi log)

---

## Dependencies & Execution Order

- **Phase 2 (T004–T006) chặn ngược 5 spec khác**: 002, 003, 005, 009, 010 đều inject `IAuditService`. Đây là lý do `AddScoped<IAuditService, AuditService>()` được đăng ký **đầu tiên** trong `Program.cs`.
- **US1 → US3**: không có bản ghi thì không có gì để tra.
- **US2 phụ thuộc dữ liệu của 003, 004, 005** — nhưng chỉ ở tầng đọc, không sửa gì.
- **Không có feature nào phụ thuộc ngược vào Dashboard** — đây là điểm cuối của chuỗi đọc.

```text
Setup → Foundational(IAuditService ★) ──→ [002·003·005·009·010 ghi audit]
                                                      ↓
                                          US1 → US3 (tra cứu)
        [003·004·005 cung cấp dữ liệu] ──→ US2 (dashboard) → Polish
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01 | T016–T019 | `DashboardServiceTests.cs` |
| AC-02 | T020 | `DashboardServiceTests.cs` |
| AC-03 | T028 | black-box (token Staff → 403) |
| AC-04 | T027 | `DashboardServiceTests.cs` |
| AC-05 | T009 | black-box (bán gói rồi tra `audit_logs`) |
| AC-06 | T012 | rà soát thủ công metadata trong DB |
| AC-07 | T026 | `DashboardServiceTests.cs` |
