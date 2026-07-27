# CLAUDE.md — GymMaster Project Memory

> Đọc `docs/06-Management/agents.md` (persona + rules) và `CONSTITUTION.md` (luật) trước.
> File này là **bộ nhớ ngữ cảnh** cho AI agent: kiến trúc, patterns, lessons learned.

## TL;DR (60 giây)
GymMaster là hệ thống web quản lý vòng đời hội viên phòng gym (1 chi nhánh, ~1000 hội viên).
Backend ASP.NET Core 10 Web API (.NET 10) + SQL Server (EF Core 10 Code First). Frontend Next.js + TypeScript.
4 role: **Admin, Staff, PT, Member**. Core flow: Member → Membership → Check-in → PT Assignment → Workout Plan → Progress → Meal Journal → Dashboard → Audit Log.

## KIẾN TRÚC HỆ THỐNG
Layered: **Controller → Service → Repository → DbContext** (xem `CONSTITUTION.md` ARCH-01).
```
/src
  /GymMaster.Api          # Controllers, middleware, DI, auth
  /GymMaster.Application  # Services, DTOs, validators, business logic
  /GymMaster.Domain       # Entities, enums, domain interfaces
  /GymMaster.Infrastructure # EF Core DbContext, Repositories, Migrations
/tests
  /Unit                   # Service logic, no DB
  /Integration            # API + test SQL Server (LocalDB / docker mssql)
/frontend                 # Next.js app
/docs                     # 15 spec files
```

## QUYẾT ĐỊNH KIẾN TRÚC (tóm tắt — chi tiết ở docs/06-Management/decision-log.md)
- **ADR-01:** 4 roles (Admin/Staff/PT/Member) thay vì 3 — cần tách lễ tân khỏi chủ phòng.
- **ADR-02:** ~~MySQL~~ → **SQL Server** + EF Core Code First (đổi 2026-05-30, xem D-17) — team quen toolset Microsoft (LocalDB/SSMS), tích hợp Azure SQL.
- **ADR-06:** ~~.NET 8~~ → **.NET 10** (ASP.NET Core 10 + EF Core 10) (đổi 2026-06-01, xem D-18) — code đã build trên `net10.0`; docs đồng bộ theo code.
- **ADR-03:** Payment ghi nhận **thủ công** trong MVP — không tích hợp payment gateway (giảm rủi ro).
- **ADR-04:** Meal journal nhập tay từ food database; Image Food Recognition là enhancement sau secondary.
- **ADR-05:** Member gửi *yêu cầu gia hạn*; **Staff** xác nhận thanh toán thủ công hoặc VNPay xác nhận online mới active. Admin quản trị/giám sát, không thực hiện giao dịch bán/gia hạn/thu tiền.

## PATTERNS BẮT BUỘC
- DTO cho mọi request/response — không trả entity trực tiếp.
- Service trả `Result<T>` (Ok/Err) thay vì throw cho business error; throw chỉ cho lỗi hệ thống.
- Lấy `userId`/`role` từ JWT claims, KHÔNG nhận từ request body (chống privilege escalation).
- Ownership check: Member↔dữ liệu của mình; PT↔Member được phân công.
- Mọi mutating action quan trọng → ghi `AuditLog`.

## NHỮNG GÌ ĐÃ CHỐT KHÔNG LÀM (Out of Scope MVP)
- Multi-branch, payment gateway tự động, realtime dashboard, booking lịch, mobile app native.

## CURRENT STATE (cập nhật 2026-06-02)
- ✅ **Spec 001 (Auth)** + **Spec 002 (User/Member/PT)** — code XONG, đã merge vào `main`.
- Code thật ở `backend/GymMaster.API/` — **1 project**, sắp xếp **theo feature** (xem mục TRẠNG THÁI 2026-07-16). KHÁC sơ đồ `/src` 4-project ở trên (chưa tách, để vậy được).
- **DB:** dùng DB ngoài **`GymMasterDb`** trên SQL Server `BanhMiChao` (team DB cung cấp, snake_case khớp code). Backend **KHÔNG tự tạo schema** (đã bỏ `EnsureCreated`). Seeder chỉ tạo roles + 4 tài khoản demo.
- **Secret** (connection string + JWT key) trong **User Secrets** (không commit). Đổi DB: `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`.
- **Repo:** đã tách 2 — `GymMaster-backend` (C#, private) + `GymMaster-frontend` (Next.js, public, repo riêng). File SQL tạo DB ở `database/`.
- **4 tài khoản test:** `admin@gymmaster.local`/`Admin123!` · `staff@`/`Staff123!` · `pt@`/`Pt123!` · `member@gymmaster.local`/`Member123!`.

## TRẠNG THÁI (cập nhật 2026-07-15)
- ✅ **Spec 001–010 đã implement hết** và deploy Cloud Run + Cloud SQL. Spec kit (`docs/03-Interface-Specs/feature-specs/` + doc 00–15) đã **đồng bộ ngược từ code** ngày 2026-07-15.
- ✅ Path FE ↔ BE đã **khớp `/api/v1/...`** (FE gọi qua `apiRequest()` base `NEXT_PUBLIC_API_BASE_URL`). Ghi chú lệch path `/api/members` trước đây đã hết hiệu lực.
- ⬜ **Unit test** coverage chưa đạt DoD ≥80% (một số service đã có test ở `tests/`).
- ⚠️ Seeder thêm 4 tài khoản demo (`EnsureUserAsync`) + hồ sơ member/PT demo.
- 📄 Chi tiết thay đổi: `docs/archive/CHANGELOG_vs_old_spec.md` · So sánh DB cũ/mới: `docs/archive/DB_DIFF_FOR_DBTEAM.md` · Schema chuẩn: `docs/02-SDD-Architecture/database-design/database-schema.md`.
- ▶️ Chạy: backend `dotnet run` ở `backend/GymMaster.API` (cổng 5042) TRƯỚC → frontend `npm run dev` (cổng 3000). Login: `localhost:3000/login`.

## TRẠNG THÁI (cập nhật 2026-07-16)
- 🏗️ **Backend đã chuyển sang feature-based** — `Controllers/`, `Services/`, `DTOs/`, `Models/` KHÔNG còn tồn tại:
  - `Features/` — `Auth` · `Account` · `Users` · `Members` · `Trainers` · `Billing` · `CheckIns` · `Training` · `Nutrition` · `Dashboard`. Mỗi feature chứa controller + service + interface + DTO của nó (namespace `GymMaster.API.Features.<Ten>`).
  - `Common/` (`GymMaster.API.Common`) — `ServiceResult<T>`, `ApiResponse<T>`/`ApiError`, `PagedResult<T>`, `ApiControllerBase`, `AppClock`, `PersonValidation`.
  - `Infrastructure/` (`GymMaster.API.Infrastructure`) — Cloudinary, Gemini, EmailSender, VnPayLibrary.
  - `Entities/`, `Data/`, `Options/` giữ nguyên (shared kernel, dùng chung xuyên feature).
- ⚠️ **`AuthServiceResult<T>` đã đổi tên thành `ServiceResult<T>`** (ở `Common/`). Tên cũ nói dối: nó là kiểu trả về của MỌI service, không riêng auth. Mục "PATTERNS BẮT BUỘC" ở trên gọi nó là `Result<T>` — tên thật trong code là `ServiceResult<T>`.
- ⚠️ Feature check-in đặt tên thư mục/namespace là **`CheckIns`** (số nhiều) vì `CheckIn` đụng tên entity `CheckIn` (lỗi `CS0118`).
- 🕸️ **Codebase graph:** `graphify-out/graph.html` (mở bằng browser) · `graphify-out/GRAPH_REPORT.md`. Chạy lại: skill `graphify` ở `.claude/skills/`. Cần `uv` (AST cục bộ, không API key, 0 token). FE cũng có `graphify-out/` riêng.
- ✅ `NU1903` đã vá: `Microsoft.OpenApi` **ghim trực tiếp 2.7.5** trong `.csproj` (CVE-2026-49451, High). **Đừng gỡ dòng ghim này** — `Microsoft.AspNetCore.OpenApi` (kể cả bản 10.0.10 mới nhất) vẫn kéo về 2.0.0 dính lỗ hổng, nâng nó không sửa được. Build hiện **0 warning**.
- 📖 OpenAPI ở `/openapi/v1.json` (`AddOpenApi()` + `MapOpenApi()` của .NET 10). **Không có** `/swagger`.

## LESSONS LEARNED
- [2026-07-16] Refactor feature-based: graphify chỉ ra `AuthServiceResult` là god node degree 185 dùng bởi 38 file ở mọi feature — nếu gom theo tên file thì đã nhét nhầm nó vào `Features/Auth/`. `PagedResult<T>` và `ApiResponse<T>` cũng đang nấp trong `UserDtos.cs`/`AuthDtos.cs`. Bài học: **gom feature theo đồ thị phụ thuộc thật, đừng đoán theo tên**; và build sớm — `CS0118` (namespace đụng tên entity) chỉ lộ ra lúc compile.
- [2026-05-30] DB từng được chốt MySQL, sau đó team **đổi sang SQL Server** (D-17). Đã cập nhật toàn bộ docs + kiểu dữ liệu (IDENTITY/NVARCHAR/DATETIME2/BIT, không dùng ENUM). Nguồn sự thật stack: `CONSTITUTION.md` Layer 3. Bài học: chốt DB sớm trước khi viết schema chi tiết.

## AUTO MEMORY (Claude Code tự append phía dưới)
<!-- entries tự động sẽ xuất hiện ở đây; review & dọn mỗi tuần -->

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
