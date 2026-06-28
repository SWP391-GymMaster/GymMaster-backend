# Database — GymMaster

File SQL tạo cơ sở dữ liệu cho backend GymMaster (SQL Server).

## File
- `GymMaster_SQLServer_Final.sql` — script tạo database **`GymMasterDb`** với đầy đủ bảng (khớp với code backend: snake_case, `user_roles`, `password_reset_tokens`..., **đã gồm `membership_packages.SupportsPT`**).
- `008_package_supports_pt.sql` — **patch** thêm cột `membership_packages.SupportsPT` cho **DB đã tạo từ trước** (idempotent — chạy lại nhiều lần không lỗi). DB tạo mới từ Final thì KHÔNG cần chạy file này.
- `011_fix_check_ins_createdby_column.sql` — **patch** đổi tên cột `check_ins.CreatedByUserId` → `CreatedBy` cho **DB cũ bị lệch tên cột**. Lệch cột này làm EF báo `Invalid column name 'CreatedBy'` → **mọi luồng check-in (Staff/Member/PT) trả 500**. Idempotent: DB đã đúng chuẩn (`CreatedBy`) sẽ tự bỏ qua.

> Các file đánh số `004`–`011` là **patch tăng dần** cho DB đang có dữ liệu (không cần tạo lại DB). Sau khi `git pull`, chạy lần lượt các patch mới trên `GymMasterDb` (mỗi file idempotent). DB tạo mới hoàn toàn từ `GymMaster_SQLServer_Final.sql` đã gồm cột chuẩn nên thường chỉ cần các patch ra đời **sau** bản Final.

## Cách dùng (clone về chạy)

**Bước 1 — Tạo / cập nhật DB** (chọn đúng trường hợp của bạn):
- **A. Máy/DB mới (lần đầu):** mở **SSMS** → mở `GymMaster_SQLServer_Final.sql` → **Execute (F5)** → tạo `GymMasterDb` (đã có sẵn `SupportsPT`).
- **B. DB đã tạo từ bản Final cũ** (trước khi có `SupportsPT`): chỉ chạy thêm `008_package_supports_pt.sql` một lần (an toàn, không xoá/sửa dữ liệu cũ — không phải tạo lại DB).

**Bước 2 — Trỏ connection string** cho backend (User Secrets), trong thư mục `backend/GymMaster.API`:
```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<SERVER>;Database=GymMasterDb;User Id=<USER>;Password=<PASS>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SecretKey" "<chuoi-ngau-nhien-it-nhat-32-ky-tu>"
```

**Bước 3 — Chạy backend:** `dotnet run` → seeder tự tạo tài khoản admin:
- `admin@gymmaster.local` / `Admin123!`

## Ghi chú
- Backend **không tự tạo schema** — DB phải được tạo từ file này (hoặc do team DB cung cấp) trước khi chạy.
- Schema chi tiết từng cột: xem `../15_DATABASE_SCHEMA.md`.
- So sánh thay đổi schema (nếu sửa DB): xem `../DB_DIFF_FOR_DBTEAM.md`.
