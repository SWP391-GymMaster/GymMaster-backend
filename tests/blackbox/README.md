# GymMaster connection, API và performance tests

Bộ script này chạy theo thứ tự thầy đề xuất: connection -> API black-box -> performance. Unit/white-box chạy bằng xUnit riêng. Script chỉ đọc dữ liệu nghiệp vụ; không tạo meal, food hay payment và không gọi Gemini thật.

## 1. Chuẩn bị database và chạy backend

Database phải được cập nhật đủ các script trong `database/` theo thứ tự. Sau đó mở terminal thứ nhất:

```powershell
dotnet run --project backend\GymMaster.API\GymMaster.API.csproj --urls http://127.0.0.1:5042
```

Nếu backend báo `Invalid column name 'AvatarUrl'`, database local chưa chạy `database/012_users_avatarurl.sql`.

## 2. Connection tests

```powershell
.\tests\blackbox\Connection.Tests.ps1 `
  -ReportPath .\tests\blackbox\artifacts\connection.json
```

`CONN-02` login vào tài khoản demo, vì vậy nó đồng thời chứng minh API kết nối được SQL Server và schema đủ mới để query bảng `users`.

## 3. API black-box tests

```powershell
.\tests\blackbox\Api.BlackBox.Tests.ps1 `
  -ReportPath .\tests\blackbox\artifacts\api.json
```

Nếu mật khẩu demo đã được đổi, truyền `-AdminEmail`, `-AdminPassword`, `-MemberEmail`, `-MemberPassword`. Không ghi mật khẩu vào report hay commit file chứa mật khẩu thật.

## 4. Performance baseline

Mặc định chạy 200 request vào Dashboard với 50 request đồng thời. Điều kiện pass dựa trên NFR của dự án: p95 <= 2 giây và tỷ lệ lỗi <= 1%.

```powershell
.\tests\blackbox\Performance.Tests.ps1 `
  -RequestCount 200 `
  -ConcurrentUsers 50 `
  -P95LimitMs 2000 `
  -ReportPath .\tests\blackbox\artifacts\performance.json
```

Đây là baseline trên máy local, không thay thế load test ở môi trường staging có cấu hình gần production. Không dùng endpoint Gemini để tránh quota và chi phí ngoài ý muốn.

## 5. Unit tests và coverage

```powershell
dotnet test tests\GymMaster.Api.Tests\GymMaster.Api.Tests.csproj
dotnet test tests\GymMaster.Api.Tests\GymMaster.Api.Tests.csproj --collect:"XPlat Code Coverage"
```

Chi tiết mapping black-box/white-box nằm trong `TEST_MATRIX.md`.
