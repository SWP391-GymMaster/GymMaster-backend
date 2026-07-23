# GymMaster — Feature Specs Index (Spec Kit / SDD + ADD)

Mỗi feature có **3 tài liệu**:

| File | Tầng | Trả lời câu hỏi | Cấu trúc |
|---|---|---|---|
| `spec.md` | SDD | **Làm cái gì** và tại sao | 9 thành phần (Context & Goal · Actors · Functional Requirements *(EARS)* · Non-functional Requirements · Data Model · API Spec · Error Handling · Acceptance Criteria *(Given-When-Then)* · Out of Scope) |
| `plan.md` | SDD/ADD | **Làm bằng cách nào** — kiến trúc, quyết định thiết kế, đánh đổi | Summary · Technical Context · Constitution Check · Project Structure · Design Decisions · Data Flow · Traceability (FR→code) · Complexity Tracking |
| `tasks.md` | ADD | **Chia việc ra sao** — trạng thái từng đầu việc | Phase (Setup → Foundational → User Story → Polish) · Dependencies · Truy vết AC |

Yêu cầu chức năng viết theo **EARS** (Ubiquitous/Event/State/Optional/Unwanted).

## Vị trí trong mô hình Hybrid SDD + ADD

Thư mục `docs/03-Interface-Specs/feature-specs/` là **tầng SDD**. Tầng **ADD** nằm ở `docs/` và `.claude/skills/`. Hai tầng nối với nhau như sau:

| Pha (theo [`ai-workflow.md`](../../06-Management/ai-workflow.md) §1) | Người làm | AI làm | Tài liệu chi phối |
|---|---|---|---|
| **B1 Spec** | duyệt spec | soạn nháp EARS, gợi error case | `docs/03-Interface-Specs/feature-specs/*/spec.md` · [`04_REQUIREMENTS`](../../01-SRS-Requirements/requirements.md) |
| **B2 Plan** | duyệt plan | đề xuất task + file ảnh hưởng | `docs/03-Interface-Specs/feature-specs/*/plan.md` · [`CONSTITUTION.md`](../../../CONSTITUTION.md) (mục Constitution Check) |
| **B3 Implement** | review từng bước | sinh code theo spec + convention | [`docs/03-Interface-Specs/feature-specs/BACKLOG.md`](BACKLOG.md) ← **đây mới là input chạy được cho agent** |
| **B4 Validate** | tick Validation Gate | chạy test, tự rà spec | [`test-plan.md`](../../04-Test-Specs/test-plan.md) §6 · mục "Truy vết AC" cuối mỗi `tasks.md` |

**Quy tắc vàng** (`ai-workflow.md` §1): *"Sai ở đâu, sửa ở Spec đó"* — code lệch acceptance criteria thì sửa `spec.md` trước, rồi mới regenerate.

**Tài liệu ADD liên quan**: [`agents.md`](../../06-Management/agents.md) (persona + rule) · [`decision-log.md`](../../06-Management/decision-log.md) (ADR — có bản đồ ADR ↔ Design Decisions cấp feature) · [`team-workflow.md`](../../06-Management/team-workflow.md) (git flow, PR) · [`prompt-library.md`](../../06-Management/prompt-library.md) (prompt mẫu).

> **Lưu ý về `tasks.md`**: vì bộ tài liệu này viết **sau** khi code chạy, `tasks.md` là *bản ghi* (hầu hết `[X]`), không phải *lệnh cho agent* — không chạy `/speckit-implement` được. Phần còn nợ đã tách ra [`BACKLOG.md`](BACKLOG.md) để dùng cho pha B3. Feature **mới** thì làm đúng chiều xuôi: `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`.

| # | Feature | Phạm vi | Tài liệu | Nguồn gốc |
|---|---|---|---|---|
| 001 | Authentication & RBAC | Login, JWT, refresh, 4 role, chống brute-force | [spec](001-auth-rbac/spec.md) · [plan](001-auth-rbac/plan.md) · [tasks](001-auth-rbac/tasks.md) | UC-01/02, FR-AUTH/RBAC |
| 002 | User/Staff/PT/Member Management | CRUD + tìm kiếm hồ sơ, soft-delete | [spec](002-member-management/spec.md) · [plan](002-member-management/plan.md) · [tasks](002-member-management/tasks.md) | UC-03/03A/04/05, FR-MEM |
| 003 | Membership, Sell, Renew & Payment | Gói tập, bán/gia hạn, payment thủ công | [spec](003-membership-billing/spec.md) · [plan](003-membership-billing/plan.md) · [tasks](003-membership-billing/tasks.md) | F1, UC-06/07/08 |
| 004 | Check-in | Ghi lượt đến, xác thực gói còn hạn | [spec](004-checkin/spec.md) · [plan](004-checkin/plan.md) · [tasks](004-checkin/tasks.md) | F2, UC-09 |
| 005 | PT Assignment, Workout & Notes | Phân công PT 1-1, giáo án, ghi chú | [spec](005-pt-training/spec.md) · [plan](005-pt-training/plan.md) · [tasks](005-pt-training/tasks.md) | F3, UC-10..13 |
| 006 | Progress Tracking & 360° | Tiến độ + hồ sơ 360 | [spec](006-progress-tracking/spec.md) · [plan](006-progress-tracking/plan.md) · [tasks](006-progress-tracking/tasks.md) | UC-14/15 |
| 007 | Meal Journal & Calorie Summary | Nhật ký ăn, tính calo ngày | [spec](007-nutrition-calorie/spec.md) · [plan](007-nutrition-calorie/plan.md) · [tasks](007-nutrition-calorie/tasks.md) | F4, UC-16..21 |
| 008 | Dashboard & Audit Log | Dashboard vận hành + audit | [spec](008-dashboard-audit/spec.md) · [plan](008-dashboard-audit/plan.md) · [tasks](008-dashboard-audit/tasks.md) | F5, UC-22/23 |
| 009 | Image Food Recognition Assist | **Enhancement** — gợi ý tên món từ ảnh (Gemini) | [spec](009-image-food-recognition/spec.md) · [plan](009-image-food-recognition/plan.md) · [tasks](009-image-food-recognition/tasks.md) | UC-26, ENH-01 |
| 010 | Online Payment via VNPay (Sandbox) | Thanh toán online VNPay + IPN auto-activate (mở rộng 003) | [spec](010-online-payment-vnpay/spec.md) · [plan](010-online-payment-vnpay/plan.md) · [tasks](010-online-payment-vnpay/tasks.md) | Yêu cầu giảng viên, override ADR-03 |

## Bản đồ phụ thuộc giữa các feature

```text
001 Auth ──→ 002 Members ──→ 003 Billing ──┬──→ 004 Check-in ──┐
             (hồ sơ)         (MembershipLifecycle ★)           │
                                            ├──→ 005 PT ───────┤
                                            ├──→ 007 Nutrition ┤
                                            └──→ 010 VNPay     │
                                                               ↓
                                                     006 Member 360°
                                                     (gom 5 nguồn)

008 Dashboard + AuditService ← 002 · 003 · 005 · 009 · 010 đều ghi audit vào đây
009 Food AI → tạo FoodItem cho 007
```

**Hai điểm chạm quan trọng nhất — sửa là ảnh hưởng nhiều feature:**
- `Features/Billing/MembershipLifecycle.cs` — luật vòng đời membership, được 003 · 004 · 005 · 006 · 007 · 009 · 010 dùng chung.
- `Features/Dashboard/IAuditService.cs` — 5 slice inject vào để ghi audit.

## Secondary (chưa làm vội)
Các feature secondary (In-app Notification, PT Booking, Group Classes, Combo Packages, PT KPI, Room Booking) được theo dõi ở [SECONDARY_BACKLOG.md](SECONDARY_BACKLOG.md) — trạng thái **Deferred**, chưa viết spec chi tiết cho tới khi core 001–008 ổn định.

## Quy ước
- ID yêu cầu (FR-*) là **nguồn truy vết**: code phải gắn tag `// FR-...`, test map theo (xem `docs/04-Test-Specs/test-plan.md`).
- Stack canonical: ASP.NET Core 10 (.NET 10) + **SQL Server** + EF Core 10 + Next.js (xem `CONSTITUTION.md` Layer 3).
- Luồng Spec Kit: `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`.
- **Status hiện tại (2026-07-15): tất cả 001–010 đã `Implemented`** — spec kit đã được **đồng bộ ngược từ code thật** (backend .NET 10 + FE Next.js đang chạy trên Cloud Run). Mọi path là `/api/v1/...`; "hôm nay" tính theo giờ VN (GMT+7, `AppClock`). Đây là bản spec phản ánh đúng hành vi code hiện tại, không phải bản thiết kế ban đầu.

## Quy ước "as-built" cho `plan.md` / `tasks.md` (2026-07-23)

Vì code đã chạy production trước khi bộ `plan.md`/`tasks.md` được viết, hai loại tài liệu này là **as-built** — ghi lại kiến trúc và công việc **đã thực sự làm**, không phải bản thiết kế dự kiến:

- Mọi đường dẫn file trong `plan.md` là file **có thật** trong repo, kiểm chứng được.
- Task `[X]` = đã có trong code. Task `[ ]` = **còn nợ**, phát hiện khi đối chiếu spec ↔ code — đây là backlog kỹ thuật thật, không phải placeholder.
- Mục **Complexity Tracking** trong `plan.md` ghi các chỗ **cố ý lệch** `CONSTITUTION.md` kèm lý do — đọc mục này trước khi định "sửa cho đúng chuẩn".

### Tổng hợp việc còn nợ

> Bản đầy đủ, có ưu tiên và điều kiện hoàn thành: **[BACKLOG.md](BACKLOG.md)** (18 mục còn mở, P1→P5). Bảng dưới chỉ để tra nhanh theo feature.

| Feature | Còn nợ | Loại |
|---|---|---|
| 001 | `AuthServiceTests.cs` (T020) · ghi lý do chọn HS256 vào D-05 (T038) | test · tài liệu |
| 002 | `MemberServiceTests.cs` (T042) · đo NFR-01 tìm kiếm (T043) | test · đo lường |
| 003 | test `ExpireStalePending` (T043) — *`PaymentServiceTests.cs` đã xong, PR #10* | test |
| 004 | đo NFR-01/NFR-03 check-in (T025) | đo lường |
| 005 | test WorkoutPlan/TrainerNote (T038, T039) | test |
| 006 | đo NFR-01 360° (T028) · test `currentMembership = null` (T029) | đo lường · test |
| 007 | **snapshot macro** — cần team DB thêm 3 cột (T031) · test tier free (T032) | nợ kỹ thuật · test |
| 008 | đo NFR-01 dashboard (T035) · cân nhắc chuyển `IAuditService` (T036) | đo lường · kiến trúc |
| 009 | test fallback khi Gemini timeout (T026) | test |
| 010 | `provider_ref` + `PaymentMethod.Online` khi làm live (T036, T037) | khi live |

Mục đáng ưu tiên nhất: **007 T031** — macro lịch sử hiện **sẽ đổi** nếu Admin sửa món (lệch [`SAFE-03`](../../01-SRS-Requirements/constraints/safety.md)), và bị chặn bởi team DB nên phải đặt yêu cầu sớm.

**Đã đóng 2026-07-23:** *005 T040 / 008 T014* — audit cho giáo án + ghi chú **đã có sẵn trong code** (34 action / 13 service), bản tài liệu đầu tiên ghi nhầm là thiếu. *B-19* — `CONSTITUTION.md` ARCH-01/ARCH-04 đã sửa cho khớp code (v1.3.0, ADR D-24).
