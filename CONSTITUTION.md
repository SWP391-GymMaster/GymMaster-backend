# CONSTITUTION.md — GymMaster Project Law

**Ratified:** 2026-05-30 | **Version:** 1.2.0 | **Status:** LOCKED
**Team:** GymMaster Dev Team (Admin/BA, Backend, Frontend, Database, QA)
**RULE:** Mọi thay đổi file này cần đồng thuận toàn team + ghi vào `12_DECISION_LOG.md`.

> Đây là "luật bất biến" của dự án. Mọi Spec, code, và quyết định kỹ thuật đều phải tuân thủ.
> Đọc cùng `10_AGENTS.md` (persona) và `CLAUDE.md` (ngữ cảnh).

---

## ═══ LAYER 1 — HARD RULES (KHÔNG BAO GIỜ VI PHẠM) ═══

### SEC-01: Password Security
THE system SHALL hash password bằng **BCrypt (cost ≥ 12)**. KHÔNG lưu plaintext bất kỳ lúc nào, kể cả trong log.

### SEC-02: Authentication bắt buộc
THE system SHALL yêu cầu **JWT Bearer Token** hợp lệ cho mọi endpoint mutating (POST/PUT/PATCH/DELETE) và mọi endpoint trả dữ liệu cá nhân. Endpoint public phải comment `// PUBLIC ENDPOINT`.

### SEC-03: Token Policy
THE system SHALL cấp Access Token hết hạn sau **15 phút** và Refresh Token **7 ngày**, single-use rotate-on-refresh.

### SEC-04: Input Validation
THE system SHALL validate và sanitize mọi user input ở tầng API (FluentValidation / DataAnnotations) TRƯỚC khi xử lý. KHÔNG ghép chuỗi SQL — chỉ dùng EF Core parameterized queries.

### SEC-05: Secrets
THE system SHALL lưu connection string, JWT secret, API keys trong **User Secrets / biến môi trường**. KHÔNG commit secret vào Git. `.env`, `appsettings.*.json` chứa secret phải nằm trong `.gitignore`.

### DATA-01: Soft Delete
THE system SHALL dùng soft delete (`IsDeleted` / `DeletedAt`) cho entity nghiệp vụ (Member, Membership, Payment...). Hard delete chỉ cho dữ liệu tạm/log > 90 ngày.

### AUDIT-01: Audit Trail
THE system SHALL ghi `AuditLog` cho mọi hành động quan trọng (tạo/sửa Membership, ghi Payment, phân công PT, đổi role) gồm: actor, action, entity, entityId, timestamp (UTC).

---

## ═══ LAYER 2 — ARCHITECTURAL CONSTRAINTS (cần RFC + approval để bypass) ═══

### ARCH-01: Layered Architecture
THE backend SHALL theo: **Controller → Service → Repository → DbContext**.
Controller KHÔNG chứa business logic. Service KHÔNG gọi `DbContext` trực tiếp (đi qua Repository). Repository KHÔNG chứa business logic.

### ARCH-02: API Contract
- REST, base path `/api/v1/[resource]`.
- Response format thống nhất: `{ success, data, error, meta }`.
- HTTP status đúng chuẩn: 200/201/400/401/403/404/409/422/500.
- Error không expose stack trace ra client; chỉ `{ code, message, requestId }`.

### ARCH-03: Authorization theo Role
THE system SHALL kiểm soát quyền theo 4 role **Admin, Staff, PT, Member** bằng `[Authorize(Roles=...)]` + ownership check (Member chỉ xem dữ liệu của chính mình; PT chỉ xem Member được phân công).

### ARCH-04: Database first-class
Mọi thay đổi schema phải qua **EF Core Migration** (Code First). KHÔNG sửa DB thủ công. Migration phải có rollback path.

### ARCH-05: Spec-Driven Development (Spec Kit)
Dự án theo **GitHub Spec Kit / SDD**. THE team SHALL:
- Tạo `specs/NNN-*/spec.md` **Approved** trước khi plan/code mỗi feature; index ở `specs/README.md`.
- Giữ mỗi spec đủ **9 thành phần**: Context & Goal · Actors · Functional (EARS) · Non-functional · Data Model · API Spec · Error Handling · Acceptance (Given-When-Then) · Out of Scope.
- Viết Functional Requirements theo **EARS** (Ubiquitous/Event/State/Optional/Unwanted) với ID `FR-*` làm khóa truy vết; code gắn tag `// FR-...`, test map theo `FR-*`/`AC-*`.
- Theo vòng đời: specify → (clarify) → plan → (checklist/analyze) → tasks → implement. KHÔNG implement khi chưa có spec+plan+tasks.
- Đồng bộ `CONSTITUTION.md` ⇆ `.specify/memory/constitution.md` (sửa một, sửa cả hai).
- Endpoint trong spec là logic; base path chuẩn hóa thực tế theo ARCH-02 (`/api/v1/...`) khi implement.

---

## ═══ LAYER 3 — ENGINEERING STANDARDS (override được nếu có lý do ghi chú) ═══

### TECH STACK (chốt — đổi cần RFC)
| Layer | Công nghệ |
|---|---|
| Frontend | Next.js + TypeScript |
| Backend | C# / ASP.NET Core 10 Web API (.NET 10) |
| Database | **SQL Server** |
| ORM | Entity Framework Core 10 — Code First Migrations |
| Auth | JWT Bearer + BCrypt |
| File Storage | Azure Blob Storage |
| AI Vision (enhancement) | Google Cloud Vision API |
| Deploy | Vercel (FE) + Azure App Service (BE) |
| VCS | GitHub Monorepo |
| API Testing | Postman / Thunder Client |

### CODING STANDARDS
- C#: nullable enabled; async/await cho I/O; DTO cho request/response (không expose entity).
- TypeScript: `strict` mode; **không dùng `any`**.
- Naming: Class/Method `PascalCase`; biến `camelCase`; bảng DB `snake_case` hoặc theo EF convention nhất quán; API route `kebab-case`.
- Không để `Console.WriteLine`/`console.log` trong code merge; dùng `ILogger`.
- Hàm ≤ 40 dòng, file ≤ 300 dòng (refactor nếu vượt). Không để TODO khi merge.

### TESTING REQUIREMENTS
- Unit test coverage ≥ **80%** cho Service/business logic.
- Integration test cho mọi API endpoint (happy path + ≥1 error case).
- Không merge nếu test cũ fail.

### GIT CONVENTIONS
- Branch: `spec/*` (thảo luận spec) | `feat/*` | `fix/*` | `chore/*`.
- Commit: Conventional Commits — `feat(membership): add renewal endpoint`.
- PR: ≥1 approval, mọi CI check pass, max ~400 dòng đổi/PR, không self-approve.

---

## ═══ AI AGENT POLICY ═══

**Được phép:** đọc/ghi `src/`, `tests/`, `docs/`; chạy `dotnet test`, `dotnet build`, lint, `git add/commit`.

**Cấm khi chưa được người xác nhận:** xóa file; sửa `CONSTITUTION.md`; push vào `main`; thêm NuGet/npm dependency mới; chạy migration drop/destructive trên DB có dữ liệu.

**Agent PHẢI:** chạy self-check theo Constitution trước khi submit code; cập nhật `CLAUDE.md` khi có quyết định kiến trúc; báo cáo khi gặp edge case không có trong spec thay vì tự đoán; tuân ARCH-05 (đọc `specs/NNN-*/spec.md` trước, gắn tag `// FR-...`, không code ngoài Out of Scope của spec).

---

**Amendments:**
- v1.1.0 (2026-05-30) — thêm ARCH-05 (Spec-Driven Development / Spec Kit); chốt Database = SQL Server (D-17). Đồng bộ `.specify/memory/constitution.md`.
- v1.2.0 (2026-06-01) — nâng runtime **.NET 8 → .NET 10** (ASP.NET Core 10, EF Core 10) để khớp code thực tế (D-18). Đồng bộ `.specify/memory/constitution.md`.
