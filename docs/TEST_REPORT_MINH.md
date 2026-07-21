# Báo cáo kiểm thử phần Minh - GymMaster

## 1. Thông tin chung

| Thuộc tính | Giá trị |
|---|---|
| Người phụ trách | Minh |
| Nhánh thực hiện | `Minh` |
| Ngày kiểm thử | 21/07/2026 |
| Backend | ASP.NET Core / .NET 10 / EF Core / SQL Server |
| Frontend | Next.js 16 / React 19 |
| Trạng thái cuối | **PASS** |

Báo cáo này ghi lại kết quả connection test, API black-box test, performance test và unit/white-box test cho phạm vi Nutrition, Food Item, Gemini Food Scan, Dashboard và Audit Log do Minh phụ trách.

## 2. Chiến lược kiểm thử

Thứ tự thực hiện theo đề xuất của giảng viên:

1. Connection test: kiểm tra backend, SQL Server, schema, JWT và các query Dashboard.
2. API test theo black-box: chỉ gửi HTTP request và kiểm tra status code/response contract.
3. Performance test: tạo tải đồng thời và kiểm tra p50, p95, p99, throughput, error rate.
4. Unit test theo white-box: gọi trực tiếp service để đi qua các nhánh validation, authorization, aggregate, error handling và boundary.

### 2.1. Black-box testing

Black-box test không truy cập code nội bộ, service hoặc `DbContext`. Test chỉ quan sát đầu vào và đầu ra công khai của API.

Các kỹ thuật đã áp dụng:

- Phân vùng tương đương hợp lệ: tài khoản Admin/Member hợp lệ, khoảng ngày đúng, phân trang đúng.
- Phân vùng tương đương không hợp lệ: thiếu token, role Member truy cập API Admin, tài khoản không tồn tại, `from > to`.
- Giá trị biên: `page=1`, `pageSize=5`, 50 request đồng thời và ngưỡng p95 2 giây.
- Kiểm tra contract: `success`, `data`, `error.code`, token, metric Dashboard và dữ liệu phân trang.

### 2.2. White-box testing

White-box test biết cấu trúc xử lý bên trong service và chủ động đi qua các nhánh quan trọng:

- Nhánh tạo mới/cập nhật dữ liệu.
- Nhánh không tìm thấy dữ liệu.
- Nhánh sai quyền truy cập.
- Nhánh validation và giá trị biên.
- Nhánh dependency Gemini thất bại.
- Nhánh dữ liệu rỗng, aggregate và phân trang.
- Nhánh membership có hiệu lực/hết hiệu lực.

## 3. Phạm vi và file kiểm thử

| File | Phạm vi |
|---|---|
| `tests/GymMaster.Api.Tests/DashboardServiceTests.cs` | Dashboard aggregate, khoảng ngày, zero state, Audit filter/sort/pagination |
| `tests/GymMaster.Api.Tests/FoodScanServiceTests.cs` | Membership gate, file ảnh, giới hạn dung lượng, lỗi Gemini, xác nhận AI |
| `tests/GymMaster.Api.Tests/GeminiServiceTests.cs` | JSON contract Gemini text-only, ước tính dinh dưỡng theo tên và không gửi dữ liệu ảnh |
| `tests/GymMaster.Api.Tests/NutritionServiceTests.cs` | Meal log, calories/macro, calorie target, authorization, history |
| `tests/GymMaster.Api.Tests/FoodItemServiceTests.cs` | Thêm món, trùng tên, validation, free limit, active membership |
| `tests/blackbox/Connection.Tests.ps1` | Kết nối backend, SQL Server, JWT và Dashboard query |
| `tests/blackbox/Api.BlackBox.Tests.ps1` | Auth, RBAC, Dashboard, Audit, Food Item, Meal Journal, Calorie Summary |
| `tests/blackbox/Performance.Tests.ps1` | Tải đồng thời, latency percentile, throughput và error rate |
| `tests/blackbox/TestSupport.ps1` | HTTP client, result model và xuất report dùng chung |
| `tests/blackbox/TEST_MATRIX.md` | Mapping test case với yêu cầu và kỹ thuật test |

Các report JSON sinh khi chạy được đặt trong `tests/blackbox/artifacts/` và bị loại khỏi Git để tránh commit file runtime.

## 4. Kết quả kiểm thử

### 4.1. Tổng hợp

| Test suite | Kết quả | Trạng thái |
|---|---:|---|
| Unit/white-box test toàn backend | 107/107 pass | PASS |
| Connection test | 4/4 pass | PASS |
| API black-box test | 12/12 pass | PASS |
| Performance request | 200/200 thành công | PASS |
| Performance error rate | 0% | PASS |
| Minh selected-services line coverage | 85,55% | PASS theo mục tiêu 80% |

So với baseline 71 unit test, đợt này bổ sung **36 test case** và toàn bộ đều pass.

### 4.2. Connection test

| ID | Nội dung | Kết quả |
|---|---|---|
| CONN-01 | Backend nhận HTTP và trả `status=running` | PASS |
| CONN-02 | Login truy vấn được SQL Server và trả access token | PASS |
| CONN-03 | JWT hợp lệ và đọc được user hiện tại | PASS |
| CONN-04 | Các query Dashboard chạy được trên database thật | PASS |

Connection test đã phát hiện database local ban đầu cũ hơn code:

- Thiếu `users.AvatarUrl`.
- Thiếu `trainer_profiles.Address` và `trainer_profiles.EmergencyContact`.

Đã áp dụng các script idempotent có sẵn của dự án:

- `database/012_users_avatarurl.sql`.
- `database/013_staff_profiles_trainer_contact_filtered_unique.sql`.

Sau khi cập nhật schema, backend khởi động và toàn bộ connection test pass.

### 4.3. API black-box test

| ID | Tình huống | Kỳ vọng | Kết quả |
|---|---|---|---|
| API-001 | Gọi Audit không có token | 401 | PASS |
| API-002 | Login bằng tài khoản không tồn tại | 401, `success=false` | PASS |
| API-003 | Admin login | 200, có `data.accessToken` | PASS |
| API-004 | Dashboard summary | 200, đủ metric chính | PASS |
| API-005 | Dashboard có `from > to` | 422, `INVALID_RANGE` | PASS |
| API-006 | Audit search và pagination | 200, `pageSize=5` | PASS |
| API-007 | Member login | 200, có token | PASS |
| API-008 | Member truy cập Admin Dashboard | 403 | PASS |
| API-009 | Đọc hồ sơ Member hiện tại | 200, có member ID | PASS |
| API-010 | Tìm Food Item | 200 | PASS |
| API-011 | Đọc Meal Journal theo ngày | 200 | PASS |
| API-012 | Đọc Calorie Summary | 200, có `consumed` | PASS |

Bộ API test chỉ đọc dữ liệu nghiệp vụ, không tự tạo meal, food, payment và không gọi Gemini thật.

### 4.4. Performance test

Endpoint kiểm tra: `GET /api/v1/dashboard/summary`.

| Chỉ số | Kết quả | Ngưỡng |
|---|---:|---:|
| Tổng request | 200 | 200 |
| Concurrent users | 50 | Khoảng 50 theo NFR |
| Success | 200 | 200 |
| Error rate | 0% | <= 1% |
| p50 | 35,68 ms | Tham khảo |
| p95 | 219,46 ms | <= 2.000 ms |
| p99 | 252,08 ms | Tham khảo |
| Throughput | 696,57 request/giây | Tham khảo local |

Kết luận: Dashboard đạt performance baseline trên môi trường local. Kết quả này không thay thế load test trên staging/production có cấu hình máy chủ và network thực tế.

### 4.5. Code coverage

| Phạm vi | Line coverage |
|---|---:|
| DashboardService | 99,44% |
| NutritionService | 85,40% |
| FoodItemService | 92,31% |
| FoodScanService | 64,74% |
| GeminiService | 47,57% |
| Tổng 4 service chính của Minh | **85,55%** |
| Toàn bộ backend | 39,42% |

Coverage toàn backend thấp hơn vì report bao gồm các module của thành viên khác. Chỉ số dùng để đánh giá phần Minh là tổng của bốn service chính ở trên.

`FoodScanService` chưa đạt 80% riêng lẻ vì các nhánh match/tạo Food Item dùng SQL Server collation và tích hợp Gemini thật. Không ép test bằng EF InMemory vì sẽ tạo kết quả sai khác provider; các nhánh này nên được bổ sung bằng integration test SQL Server và Gemini sandbox/mock server.

## 5. Lệnh chạy lại

### 5.1. Chạy backend

```powershell
dotnet run --project backend\GymMaster.API\GymMaster.API.csproj --urls http://127.0.0.1:5042
```

### 5.2. Connection test

```powershell
.\tests\blackbox\Connection.Tests.ps1 `
  -ReportPath .\tests\blackbox\artifacts\connection.json
```

### 5.3. API black-box test

```powershell
.\tests\blackbox\Api.BlackBox.Tests.ps1 `
  -ReportPath .\tests\blackbox\artifacts\api.json
```

### 5.4. Performance test

```powershell
.\tests\blackbox\Performance.Tests.ps1 `
  -RequestCount 200 `
  -ConcurrentUsers 50 `
  -P95LimitMs 2000 `
  -ReportPath .\tests\blackbox\artifacts\performance.json
```

### 5.5. Unit test và coverage

```powershell
dotnet test tests\GymMaster.Api.Tests\GymMaster.Api.Tests.csproj
dotnet test tests\GymMaster.Api.Tests\GymMaster.Api.Tests.csproj --collect:"XPlat Code Coverage"
```

## 6. Hạn chế và đề xuất tiếp theo

- Chưa gọi Gemini thật trong test tự động để tránh quota, chi phí và test không ổn định.
- Performance hiện là baseline local; nên chạy lại trên staging với database gần production.
- Nên bổ sung integration test dùng SQL Server container/test database cho nhánh collation của Food Scan.
- Frontend có 261/261 unit/component test pass; 2/2 Chromium E2E mới cho focus ô khối lượng và AI ước tính theo tên cũng pass.
- Khi merge, cần giữ các commit test tách biệt và không commit file secrets, runtime logs hoặc report JSON sinh tự động.

## 7. Kết luận

Các luồng connection, API chính, RBAC, Nutrition, Dashboard, Audit và performance trong phạm vi Minh đều đạt kết quả mong đợi. Bộ test có thể chạy lại độc lập, trả exit code khác 0 khi có lỗi và phù hợp để dùng làm bằng chứng kiểm thử khi review hoặc chấm bài.
