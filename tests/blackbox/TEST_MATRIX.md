# Test matrix - phần Minh

| ID | Loại | Mục tiêu | Kỳ vọng chính | Liên kết yêu cầu |
|---|---|---|---|---|
| CONN-01 | Connection / black-box | Backend có thể nhận HTTP | `200`, `status=running` | Deployment readiness |
| CONN-02 | Connection / black-box | API kết nối được SQL Server và schema tương thích | Login `200`, có token | FR-AUTH-02 |
| CONN-03 | Connection / black-box | JWT và đọc bảng user hoạt động | `/auth/me` trả `200` | FR-AUTH-02, RBAC |
| CONN-04 | Connection / black-box | Các query Dashboard chạy được trên DB thật | Summary trả `200` | FR-DASH-01..03 |
| API-001..012 | API / black-box | Auth, RBAC, dashboard, audit, food search, meal journal, calorie summary | Status code và response contract đúng | FR-AUD-02, FR-DASH-01..03, FR-FOOD-01, FR-MEAL-01, FR-CAL-01 |
| `DashboardServiceTests` | Unit / white-box | Validation, zero state, aggregate, timezone, filter, sort, pagination/clamp | Tất cả nhánh đã chọn đúng kết quả | FR-DASH-01..03, FR-AUD-02 |
| `FoodScanServiceTests` | Unit / white-box | Membership gate, validation ảnh, lỗi Gemini, empty result, validation confirm | `403/422/502/200/400` đúng nhánh | FR-AI-01..04 |
| `NutritionServiceTests` | Unit / white-box | Macro, target mới nhất, không có target | Phép tính và error code đúng | FR-MEAL-01, FR-CAL-01 |
| PERF-01 | Performance / black-box | Dashboard dưới 50 request đồng thời | p95 <= 2 giây, error <= 1% | NFR dashboard <2s, khoảng 50 concurrent users |

## Phân vùng black-box đang dùng

- Lớp tương đương hợp lệ: tài khoản Admin/Member đúng; khoảng ngày hợp lệ; trang và page size hợp lệ.
- Lớp tương đương không hợp lệ: không token; role Member gọi API Admin; tài khoản không tồn tại; `from > to`.
- Giá trị biên: `page=1`, `pageSize=5`; unit test kiểm tra clamp `page=0`, `pageSize=999`; ảnh vượt đúng giới hạn cấu hình.

White-box ở đây nghĩa là test biết cấu trúc nhánh trong service và cố ý đi qua từng nhánh quan trọng. Black-box chỉ quan sát request/response công khai, không gọi trực tiếp service hay DbContext.
