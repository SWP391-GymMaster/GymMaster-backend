# 12 — Decision Log (ADR)

> Ghi lại quyết định quan trọng + lý do. Tránh "Context Amnesia" (sách Ch.8.5). Mỗi quyết định: bối cảnh → lựa chọn → lý do → hệ quả.

| ID | Ngày | Quyết định | Lý do | Hệ quả |
|---|---|---|---|---|
| D-01 | 2026-05 | Dùng **4 role**: Admin, Staff, PT, Member | Phản ánh vận hành gym thực tế; Staff tách khỏi Admin | RBAC 4 nhóm; thêm UC-03A quản lý Staff |
| D-02 | 2026-05 | Backend **C#/ASP.NET Core 8 Web API** | Phù hợp môn học SWP391, team quen | Layered architecture |
| D-03 | 2026-05 | Database **MySQL** | ~~Free, phổ biến~~ | **Superseded by D-17** |
| D-04 | 2026-05 | ORM **EF Core 8 Code-First** | Migration versioned, đồng bộ team | Không sửa DB bằng tay |
| D-05 | 2026-05 | Auth **JWT Bearer + BCrypt** | Stateless, chuẩn ngành | Access 15', Refresh 7d; bảng refresh_tokens |
| D-06 | 2026-05 | Frontend **Next.js + TypeScript** | SSR, type-safe, deploy Vercel dễ | — |
| D-07 | 2026-05 | File storage **Azure Blob** | Lưu ảnh tiến độ/meal ngoài DB | — |
| D-08 | 2026-05 | **AI Vision = enhancement only** | Tránh scope creep; độ chính xác chưa đủ tin | UC-26 ngoài MVP, người xác nhận |
| D-09 | 2026-05 | **Soft delete** cho dữ liệu nghiệp vụ | Bảo toàn lịch sử + audit | Cột IsDeleted/DeletedAt |
| D-10 | 2026-05 | **Audit log** cho action mutating | Truy vết, yêu cầu vận hành | Bảng AuditLogs |
| D-11 | 2026-05 | API contract `{success,data,error,meta}` | Nhất quán FE-BE | Middleware chuẩn hóa response |
| D-12 | 2026-05 | Member có **tối đa 1 PT active** | Mô hình huấn luyện 1-1 | Assign mới đóng assignment cũ |
| D-13 | 2026-05 | Membership có trạng thái **PendingPayment** | Tách bán gói khỏi thanh toán | Chưa trả → không check-in |
| D-14 | 2026-05 | Coverage tối thiểu **80%** business logic | Chất lượng demo + an toàn refactor | CI gate |
| D-15 | 2026-05 | **Monorepo** trên GitHub | Dễ quản lý cho team nhỏ | FE + BE chung repo |
| D-16 | 2026-05 | Áp dụng **CONSTITUTION + AGENTS + CLAUDE** | Theo playbook SDD+ADD | Thêm 3 file nền tảng |
| D-17 | 2026-05-30 | Database **SQL Server** (CANONICAL) — thay MySQL | Team chốt dùng SQL Server (quen toolset MS, LocalDB/SSMS, tích hợp Azure SQL) | EF Core `Microsoft.EntityFrameworkCore.SqlServer`; kiểu dữ liệu IDENTITY/NVARCHAR/DATETIME2/BIT; không có ENUM → bảng lookup/CHECK |
| D-18 | 2026-06-01 | Runtime **.NET 10** (ASP.NET Core 10 + EF Core 10) — thay .NET 8 | Code thực tế đã build trên `net10.0`; chốt đồng bộ tài liệu theo code thay vì hạ cấp | `TargetFramework=net10.0`; package `Microsoft.*` 10.0.x; cập nhật toàn bộ docs + CONSTITUTION v1.2.0; superseded version trong D-02/D-04 |

> Khi đổi một quyết định: thêm dòng mới với ID mới, đánh dấu dòng cũ "Superseded by D-xx", KHÔNG xóa lịch sử.
