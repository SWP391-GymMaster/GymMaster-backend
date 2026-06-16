# CLAUDE.md — GymMaster Project Memory

> Đọc `10_AGENTS.md` (persona + rules) và `CONSTITUTION.md` (luật) trước.
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

## QUYẾT ĐỊNH KIẾN TRÚC (tóm tắt — chi tiết ở 12_DECISION_LOG.md)
- **ADR-01:** 4 roles (Admin/Staff/PT/Member) thay vì 3 — cần tách lễ tân khỏi chủ phòng.
- **ADR-02:** ~~MySQL~~ → **SQL Server** + EF Core Code First (đổi 2026-05-30, xem D-17) — team quen toolset Microsoft (LocalDB/SSMS), tích hợp Azure SQL.
- **ADR-06:** ~~.NET 8~~ → **.NET 10** (ASP.NET Core 10 + EF Core 10) (đổi 2026-06-01, xem D-18) — code đã build trên `net10.0`; docs đồng bộ theo code.
- **ADR-03:** Payment ghi nhận **thủ công** trong MVP — không tích hợp payment gateway (giảm rủi ro).
- **ADR-04:** Meal journal nhập tay từ food database; Image Food Recognition là enhancement sau secondary.
- **ADR-05:** Member gửi *yêu cầu gia hạn*, Admin/Staff *xác nhận thanh toán* mới active.

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
- Code thật ở `backend/GymMaster.API/` — **1 project**, sắp xếp theo lớp (Controllers/Services/DTOs/Entities/Data/Options). KHÁC sơ đồ `/src` 4-project ở trên (chưa tách, để vậy được).
- **DB:** dùng DB ngoài **`GymMasterDb`** trên SQL Server `BanhMiChao` (team DB cung cấp, snake_case khớp code). Backend **KHÔNG tự tạo schema** (đã bỏ `EnsureCreated`). Seeder chỉ tạo roles + 4 tài khoản demo.
- **Secret** (connection string + JWT key) trong **User Secrets** (không commit). Đổi DB: `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`.
- **Repo:** đã tách 2 — `GymMaster-backend` (C#, private) + `GymMaster-frontend` (Next.js, public, repo riêng). File SQL tạo DB ở `database/`.
- **4 tài khoản test:** `admin@gymmaster.local`/`Admin123!` · `staff@`/`Staff123!` · `pt@`/`Pt123!` · `member@gymmaster.local`/`Member123!`.

## CÒN DANG DỞ / CHÚ Ý (cho session sau)
- ⬜ **Unit test** (DoD ≥80%) chưa viết.
- ⬜ **Spec 003–008** chưa làm. Frontend đã có UI nutrition/progress/dashboard → gọi backend chưa có → lỗi/mock = **BÌNH THƯỜNG**.
- ⚠️ **Lệch path** với frontend: FE gọi `/api/members`, `/api/trainers` (không `v1`) còn backend dùng `/api/v1/...` → cần đồng bộ với bạn frontend (auth + users đã khớp `/api/v1/`).
- ⚠️ Seeder thêm 4 tài khoản demo (`EnsureUserAsync`) — thay đổi này **CHƯA commit**.
- 📄 Chi tiết thay đổi: `CHANGELOG_vs_old_spec.md` · So sánh DB cũ/mới: `DB_DIFF_FOR_DBTEAM.md` · Schema chuẩn: `15_DATABASE_SCHEMA.md`.
- ▶️ Chạy: backend `dotnet run` ở `backend/GymMaster.API` (cổng 5042) TRƯỚC → frontend `npm run dev` (cổng 3000). Login: `localhost:3000/login`.

## LESSONS LEARNED
- [2026-05-30] DB từng được chốt MySQL, sau đó team **đổi sang SQL Server** (D-17). Đã cập nhật toàn bộ docs + kiểu dữ liệu (IDENTITY/NVARCHAR/DATETIME2/BIT, không dùng ENUM). Nguồn sự thật stack: `CONSTITUTION.md` Layer 3. Bài học: chốt DB sớm trước khi viết schema chi tiết.

## AUTO MEMORY (Claude Code tự append phía dưới)
<!-- entries tự động sẽ xuất hiện ở đây; review & dọn mỗi tuần -->

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
