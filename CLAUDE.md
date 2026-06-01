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

## CURRENT STATE
- Phase: **Phase 0–1 (Context Discovery + Specification)**. Đang hoàn thiện bộ 15 spec docs.
- Chưa scaffold code. Bước tiếp: chốt spec → PLAN → TASKS → implement theo sprint (xem `07_ROADMAP_RELEASES.md`).

## LESSONS LEARNED
- [2026-05-30] DB từng được chốt MySQL, sau đó team **đổi sang SQL Server** (D-17). Đã cập nhật toàn bộ docs + kiểu dữ liệu (IDENTITY/NVARCHAR/DATETIME2/BIT, không dùng ENUM). Nguồn sự thật stack: `CONSTITUTION.md` Layer 3. Bài học: chốt DB sớm trước khi viết schema chi tiết.

## AUTO MEMORY (Claude Code tự append phía dưới)
<!-- entries tự động sẽ xuất hiện ở đây; review & dọn mỗi tuần -->

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
