# GymMaster Constitution

> Bản chuẩn Spec Kit của hiến pháp dự án. Chi tiết 3 lớp đầy đủ ở `CONSTITUTION.md` (root). Khi mâu thuẫn: file này + `CONSTITUTION.md` (đồng bộ) **vượt trên** mọi tài liệu khác.

## Core Principles

### I. Spec-First & EARS (NON-NEGOTIABLE)
Mọi feature phải có `specs/NNN-*/spec.md` được **Approved** trước khi code. Yêu cầu chức năng viết theo **EARS** (Ubiquitous/Event/State/Optional/Unwanted) với ID truy vết `FR-*`. Mỗi spec đủ 9 thành phần: Context & Goal, Actors, Functional (EARS), Non-functional, Data Model, API Spec, Error Handling, Acceptance Criteria (Given-When-Then), Out of Scope. Không có spec rõ → KHÔNG code, phải hỏi. "Sai ở đâu sửa ở Spec đó".

### II. Security by Default
SEC-01 mật khẩu BCrypt cost ≥ 12. SEC-02 mọi endpoint mutating yêu cầu JWT. SEC-03 token policy Access 15' / Refresh 7d. SEC-04 validate input, không nối chuỗi SQL (luôn parameterized/EF). SEC-05 secret chỉ từ env/User-Secrets. `userId`/`role` lấy từ JWT claim, không từ body.

### III. Layered Architecture
ARCH-01: Controller → Service → Repository → DbContext. Không truy vấn DB trong Controller. ARCH-02: response chuẩn `{success, data, error, meta}`; lỗi `{code, message, requestId}` không lộ stack trace. ARCH-03: RBAC 4 role (Admin/Staff/PT/Member). ARCH-04: mọi đổi schema qua EF migration.

### IV. Data Integrity & Audit
DATA-01: soft-delete (IsDeleted) cho dữ liệu nghiệp vụ, không xóa cứng. AUDIT-01: mọi hành động mutating quan trọng ghi AuditLog (ai/hành động/đối tượng/thời gian/metadata), không chứa secret/PII nhạy cảm.

### V. Test & Validation Gate
Coverage ≥ 80% business logic; mỗi endpoint có happy path + ≥1 error case. Trước merge phải qua Validation Gate 4 lớp (Automated → Spec compliance → Constitution → Acceptance) — xem `09_TEST_PLAN.md` §6. Không tin "AI đã xong"; tin khi test xanh + checklist tick.

## Technology Standards
Stack canonical (Layer 3): Frontend Next.js + TypeScript; Backend C#/ASP.NET Core 10 Web API (.NET 10); Database **SQL Server** (Cloud SQL); ORM EF Core 10 Code-First; Auth JWT Bearer + BCrypt + Google ID token; File storage **Cloudinary** (avatar); AI Vision **Google Gemini Vision** (enhancement); Online payment **VNPay**; Deploy **Google Cloud Run** (FE + BE). Một nguồn sự thật duy nhất về stack — đổi stack phải qua Decision Log (D-17 SQL Server; D-18 .NET 10; D-19 Cloud Run; D-20 Cloudinary; D-21 Gemini; D-22 VNPay).

## Development Workflow
Luồng Spec Kit: `/speckit-constitution` → `/speckit-specify` → (`/speckit-clarify`) → `/speckit-plan` → (`/speckit-checklist`/`/speckit-analyze`) → `/speckit-tasks` → `/speckit-implement`. Mỗi feature 1 nhánh `NNN-*`. PR cần ≥1 approval + CI xanh + cập nhật spec/Decision Log trong cùng PR khi đổi business rule. Out of Scope = không làm.

## Governance
Hiến pháp này vượt trên mọi thực hành khác. Mọi PR/review phải xác minh tuân thủ Core Principles. Vi phạm Hard Rule (SEC-*/DATA-*/AUDIT-*) chặn merge và phải escalate. Sửa đổi hiến pháp: ghi vào `12_DECISION_LOG.md`, tăng version, đồng bộ cả `CONSTITUTION.md` và file này. Hướng dẫn runtime cho agent: `10_AGENTS.md` + `CLAUDE.md`.

**Version**: 1.2.0 | **Ratified**: 2026-05-30 | **Last Amended**: 2026-06-01
