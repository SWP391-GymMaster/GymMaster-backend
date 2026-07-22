# Phân công hoàn thiện SDS — GymMaster

- Tài liệu nền: `docs/GymMaster_SDS_v1.1.docx`
- Ngày chốt: 22/07/2026
- Phạm vi: 28 hình, gồm 4 sơ đồ tổng quan, 12 class diagram và 12 sequence diagram
- Trạng thái: Phân công chính thức

## 1. Phân công chính thức

| Nhóm | Người | Phần vẽ được giao | Tổng |
|---|---|---|---:|
| N1 | Như | Class + Sequence: Authentication and Session Management; Password Recovery and Account Profile; Admin Dashboard and Audit Logs | 6 |
| N2 | Quang Anh | Backend Package Diagram; Database Schema/ERD; Class + Sequence: User, Member and Trainer Management; Class: Progress Tracking and Member 360 | 5 |
| N3 | Lộc | Class + Sequence: Membership Packages and Membership Lifecycle; Payment and VnPay Integration | 4 |
| N4 | Đam | Class + Sequence: Member Check-in; PT Assignment; Workout Plans and Trainer Notes; Sequence: Progress Tracking and Member 360 | 7 |
| N5 | Minh | System Architecture; Frontend Package Diagram; Class + Sequence: Nutrition/Meal Journal; Gemini Food Recognition | 6 |

Tổng cộng: **28/28 hình đã có người chịu trách nhiệm**.

## 2. Hai phần giao nhau đã chốt rõ

- **User/Member/Trainer Management:** Quang Anh trực tiếp vẽ; Như review phần Users, trạng thái tài khoản và quản trị mật khẩu.
- **Progress Tracking and Member 360:** Quang Anh vẽ Class Diagram; Đam vẽ Sequence Diagram. Hai bạn chốt chung actor, lifeline `ProgressService` và `GetProfile360Async` trước khi xuất ảnh.
- **Database Schema/ERD:** Mỗi owner gửi danh sách bảng và khóa ngoại của domain cho Quang Anh; Quang Anh hợp nhất và chịu trách nhiệm bản ERD cuối.

## 3. Danh sách 28 hình và owner

| No. | Diagram | Owner |
|---:|---|---|
| 01 | System Architecture / Component | Minh |
| 02 | Frontend Package | Minh |
| 03 | Backend Package | Quang Anh |
| 04 | Database Schema / ERD | Quang Anh |
| 05 | Class — Authentication and Session | Như |
| 06 | Sequence — Authentication and Session | Như |
| 07 | Class — Password Recovery and Profile | Như |
| 08 | Sequence — Password Recovery and Profile | Như |
| 09 | Class — User/Member/Trainer | Quang Anh |
| 10 | Sequence — User/Member/Trainer | Quang Anh |
| 11 | Class — Membership Lifecycle | Lộc |
| 12 | Sequence — Membership Lifecycle | Lộc |
| 13 | Class — Payment and VnPay | Lộc |
| 14 | Sequence — Payment and VnPay | Lộc |
| 15 | Class — Member Check-in | Đam |
| 16 | Sequence — Member Check-in | Đam |
| 17 | Class — PT Assignment | Đam |
| 18 | Sequence — PT Assignment | Đam |
| 19 | Class — Workout Plans/Trainer Notes | Đam |
| 20 | Sequence — Workout Plans/Trainer Notes | Đam |
| 21 | Class — Progress Tracking/Member 360 | Quang Anh |
| 22 | Sequence — Progress Tracking/Member 360 | Đam |
| 23 | Class — Nutrition/Meal Journal | Minh |
| 24 | Sequence — Nutrition/Meal Journal | Minh |
| 25 | Class — Gemini Food Recognition | Minh |
| 26 | Sequence — Gemini Food Recognition | Minh |
| 27 | Class — Admin Dashboard/Audit | Như |
| 28 | Sequence — Admin Dashboard/Audit | Như |

## 4. Checklist kỹ thuật theo người

### Như

- Dùng `AuthService` cho login, token helper và password operations; không vẽ `JwtTokenService` hoặc `PasswordService` thành lớp riêng.
- Dùng `useAuthSessionStore` cho session phía frontend.
- `AuditService` ghi log; `DashboardService` đọc và phân trang audit log.

### Quang Anh

- ERD dùng đủ 23 bảng và đúng khóa ngoại, tên cột trong `GymMasterDbContext` và schema cuối.
- Member 360 nằm trong `ProgressService.GetProfile360Async`; không tạo `Member360Service`.
- Class User/Member/Trainer phải khớp controller/service thật; soft-delete chỉ dùng `IsDeleted` và `UpdatedAt`.

### Lộc

- `MembershipStatus`: `PendingPayment=0`, `Active=1`, `Expired=2`, `Cancelled=3`.
- `PaymentStatus`: `Pending=0`, `Paid=1`, `Refunded=2`.
- `PaymentMethod`: `Cash=1`, `Transfer=2`, `Card=3`.
- VnPay dùng `Payment.Id` làm `vnp_TxnRef`; bảng `payments` không có `ExternalTransactionId`.

### Đam

- `check_ins` dùng cột `CreatedBy`.
- `trainer_assignments` dùng trạng thái `Active=1`, `Ended=2` và ngày `StartDate`/`EndDate`.
- `workout_exercises` dùng `WeightKg`; progress dùng `MeasuredAt` và một bản ghi mỗi ngày.
- Sequence Member 360 phải thể hiện access scope, lifecycle sync và các truy vấn tổng hợp.

### Minh

- Nutrition dùng `FoodItemsController`, `MealLogsController`, `MemberNutritionController` và `NutritionService`.
- Macro được tổng hợp từ `FoodItem`; `meal_log_items` chỉ lưu `Quantity` và `Calories`.
- AI dùng `FoodScanController`, `FoodScanService` và `GeminiService`.
- `food_items` không có `NormalizedName` hoặc trạng thái `AIConfirmed`; giá trị nguồn AI là `Source="AI"`.
- Sau khi nhận ảnh: chèn đúng placeholder, cập nhật field và kiểm tra toàn bộ trang trước khi xuất PDF.

## 5. Quy trình nộp hình

1. Đọc mục tương ứng trong `GymMaster_SDS_v1.1.docx` và xác nhận tên lớp, method, bảng và cột.
2. Vẽ hình; sequence diagram phải có nhánh lỗi/alternative và không để đường nối chồng chữ.
3. Tự kiểm tra rồi gửi PNG hoặc SVG rõ nét theo tên `Fig_<No>_<ShortName>.<ext>`.
4. Thực hiện review chéo tại hai phần giao nhau; owner sửa và chốt bản cuối.
5. Minh chèn ảnh, nhấn `Ctrl+A` → `F9` để cập nhật caption/mục lục, render PDF và kiểm tra lần cuối.

## 6. Tiêu chí nghiệm thu

- Đủ 28 hình, đúng owner và đúng vị trí caption trong SDS.
- Class diagram chỉ dùng lớp/interface có thật; frontend store hoặc private helper phải được ghi rõ.
- Sequence diagram khớp endpoint, service và các nhánh lỗi đã mô tả.
- ERD và SQL dùng đúng tên bảng, cột, kiểu enum và quan hệ cuối.
- Ảnh đọc rõ ở mức 100%, không bị cắt, đè chữ hoặc tách caption khỏi hình.
- Mục lục, số hình và số trang đã cập nhật trước khi xuất PDF.
