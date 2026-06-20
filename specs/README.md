# GymMaster — Feature Specs Index (Spec Kit / SDD)

Mỗi feature có `spec.md` theo cấu trúc **9 thành phần** (Context & Goal · Actors · Functional Requirements *(EARS)* · Non-functional Requirements · Data Model · API Spec · Error Handling · Acceptance Criteria *(Given-When-Then)* · Out of Scope). Yêu cầu chức năng viết theo **EARS** (Ubiquitous/Event/State/Optional/Unwanted).

| # | Feature | Phạm vi | Nguồn gốc |
|---|---|---|---|
| 001 | [Authentication & RBAC](001-auth-rbac/spec.md) | Login, JWT, refresh, 4 role, chống brute-force | UC-01/02, FR-AUTH/RBAC |
| 002 | [User/Staff/PT/Member Management](002-member-management/spec.md) | CRUD + tìm kiếm hồ sơ, soft-delete | UC-03/03A/04/05, FR-MEM |
| 003 | [Membership, Sell, Renew & Payment](003-membership-billing/spec.md) | Gói tập, bán/gia hạn, payment thủ công | F1, UC-06/07/08 |
| 004 | [Check-in](004-checkin/spec.md) | Ghi lượt đến, xác thực gói còn hạn | F2, UC-09 |
| 005 | [PT Assignment, Workout & Notes](005-pt-training/spec.md) | Phân công PT 1-1, giáo án, ghi chú | F3, UC-10..13 |
| 006 | [Progress Tracking & 360°](006-progress-tracking/spec.md) | Tiến độ + hồ sơ 360 | UC-14/15 |
| 007 | [Meal Journal & Calorie Summary](007-nutrition-calorie/spec.md) | Nhật ký ăn, tính calo ngày | F4, UC-16..21 |
| 008 | [Dashboard & Audit Log](008-dashboard-audit/spec.md) | Dashboard vận hành + audit | F5, UC-22/23 |
| 009 | [Image Food Recognition Assist](009-image-food-recognition/spec.md) | **Enhancement** — gợi ý tên món từ ảnh | UC-26, ENH-01 |
| 010 | [Online Payment via VNPay (Sandbox)](010-online-payment-vnpay/spec.md) | Thanh toán online VNPay + IPN auto-activate (mở rộng 003) | Yêu cầu giảng viên, override ADR-03 |

## Secondary (chưa làm vội)
Các feature secondary (Barcode, In-app Notification, PT Booking, Group Classes, Combo Packages, PT KPI, Room Booking) được theo dõi ở [SECONDARY_BACKLOG.md](SECONDARY_BACKLOG.md) — trạng thái **Deferred**, chưa viết spec chi tiết cho tới khi core 001–008 ổn định.

## Quy ước
- ID yêu cầu (FR-*) là **nguồn truy vết**: code phải gắn tag `// FR-...`, test map theo (xem `09_TEST_PLAN.md`).
- Stack canonical: ASP.NET Core 10 (.NET 10) + **SQL Server** + EF Core 10 + Next.js (xem `CONSTITUTION.md` Layer 3).
- Luồng Spec Kit tiếp theo cho mỗi feature: `/speckit-plan` → `/speckit-tasks` → `/speckit-implement` (chưa thực hiện — giai đoạn này chỉ tạo spec).
- Status hiện tại: tất cả **Approved** ở mức spec; chưa scaffold code.
