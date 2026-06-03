# Database — GymMaster

File SQL tạo cơ sở dữ liệu cho backend GymMaster (SQL Server).

## File
- `GymMaster_SQLServer_Final.sql` — script tạo database **`GymMasterDb`** với đầy đủ bảng (khớp với code backend: snake_case, `user_roles`, `password_reset_tokens`...).

## Cách dùng (clone về chạy)
1. Mở **SSMS**, kết nối tới SQL Server của bạn.
2. Mở `GymMaster_SQLServer_Final.sql` → **Execute (F5)** → tạo database `GymMasterDb`.
3. Trỏ connection string cho backend (User Secrets) — trong thư mục `backend/GymMaster.API`:
   ```
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<SERVER>;Database=GymMasterDb;User Id=<USER>;Password=<PASS>;TrustServerCertificate=True"
   dotnet user-secrets set "Jwt:SecretKey" "<chuoi-ngau-nhien-it-nhat-32-ky-tu>"
   ```
4. Chạy backend: `dotnet run` → seeder tự tạo tài khoản admin để đăng nhập:
   - `admin@gymmaster.local` / `Admin123!`

## Ghi chú
- Backend **không tự tạo schema** — DB phải được tạo từ file này (hoặc do team DB cung cấp) trước khi chạy.
- Schema chi tiết từng cột: xem `../15_DATABASE_SCHEMA.md`.
- So sánh thay đổi schema (nếu sửa DB): xem `../DB_DIFF_FOR_DBTEAM.md`.
