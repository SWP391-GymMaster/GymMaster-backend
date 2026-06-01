# 01 — CONTEXT.md

# GymMaster — Project Context

**Status:** Updated — Approved 4 Roles & Tech Stack  
**Phase:** Phase 0 — Context Discovery

## 1. Problem Statement

Phòng tập gym quy mô nhỏ đến trung bình thường vận hành bằng Excel, sổ tay, Zalo hoặc nhiều công cụ rời rạc. Điều này gây ra các vấn đề:

- Admin khó tra cứu hội viên khi check-in hoặc gia hạn gói.
- Thông tin gói tập, thanh toán, ngày hết hạn dễ bị sai lệch.
- PT không có nơi tập trung để theo dõi giáo án, ghi chú và tiến độ của hội viên.
- Hội viên khó nhìn lại lịch sử tập luyện, tiến độ cơ thể và dinh dưỡng.
- Admin thiếu dashboard để theo dõi doanh thu, trạng thái thanh toán và dữ liệu vận hành.
- Khi có tranh chấp về gói tập, phân công PT hoặc thanh toán, hệ thống không có audit trail rõ ràng.

**GymMaster** giải quyết các vấn đề trên bằng một hệ thống web quản lý vòng đời hội viên: tạo tài khoản, bán/gia hạn gói, check-in, phân công PT, theo dõi tiến độ, meal journal, dashboard và audit log.

## 2. Product Vision

GymMaster là hệ thống web hỗ trợ phòng tập quản lý các hoạt động vận hành cốt lõi, tập trung vào:

```text
Member lifecycle
→ Membership
→ Check-in
→ PT Assignment
→ Workout Plan
→ Progress Tracking
→ Meal Journal
→ Dashboard
```

## 3. Product Goals

| ID | Mục tiêu |
|---|---|
| PG-01 | Quản lý hội viên, PT và tài khoản theo vai trò. |
| PG-02 | Quản lý gói tập, bán/gia hạn gói và trạng thái thanh toán. |
| PG-03 | Ghi nhận check-in của hội viên. |
| PG-04 | Hỗ trợ Admin phân công PT cho hội viên. |
| PG-05 | Hỗ trợ PT tạo giáo án và ghi chú hằng ngày. |
| PG-06 | Hỗ trợ hội viên xem hồ sơ 360° và tiến độ tập luyện. |
| PG-07 | Hỗ trợ theo dõi calories bằng manual meal journal. |
| PG-08 | Cung cấp dashboard doanh thu, trạng thái thanh toán và check-in. |
| PG-09 | Ghi audit log cho các hành động quan trọng. |

## 4. Strategic Goals

| ID | Mục tiêu chiến lược |
|---|---|
| SG-01 | Có một core business flow đủ rõ để demo xuyên suốt. |
| SG-02 | Giảm rủi ro bằng cách không đưa external API phức tạp vào core. |
| SG-03 | Thiết kế database quan hệ rõ, có PK/FK và map được với use cases. |
| SG-04 | Dễ chia việc cho frontend, backend, database, testing và docs. |
| SG-05 | Có thể mở rộng sau thành booking, notification, barcode lookup, KPI hoặc reports. |
| SG-06 | Tập trung vào tính ổn định của demo và dữ liệu thật từ workflow. |

## 5. Target Scale

| Chỉ số | Giá trị mục tiêu |
|---|---|
| Số phòng tập | 1 |
| Hội viên | khoảng 1.000 |
| PT | 5–15 |
| Admin/Staff | 1–5 |
| Peak concurrent users | khoảng 50 |
| Check-in/ngày | 100–300 |
| Giao dịch/gia hạn/tháng | 100–200 |

## 6. Stakeholders

| Stakeholder | Mô tả | Nhu cầu |
|---|---|---|
| Admin / Chủ phòng tập | Quản lý toàn hệ thống | Dashboard, doanh thu, tài khoản, gói tập, phân công PT |
| Staff / Lễ tân | Hỗ trợ vận hành tại quầy trong mô hình 4 role chính thức | Check-in, bán/gia hạn gói, tìm hội viên |
| PT | Huấn luyện viên | Xem hội viên được phân công, giáo án, note, tiến độ |
| Member | Hội viên | Check-in, xem gói, xem giáo án, meal journal, tiến độ |
| Development Team | Team xây dựng hệ thống | Scope rõ, task rõ, workflow rõ |
| Mentor | Người review dự án | Tài liệu đúng format, demo ổn định, scope hợp lý |

## 7. Core Business Flow

```text
Admin tạo tài khoản/hồ sơ Member
→ Admin/Staff bán hoặc gia hạn gói tập
→ Payment được ghi nhận
→ Membership active
→ Member check-in
→ Admin phân công PT
→ PT xem danh sách Member được phân công
→ PT tạo WorkoutPlan
→ PT ghi DailyTrainerNote
→ Member xem hồ sơ 360°, giáo án, ghi chú và progress
→ Member nhập MealLog
→ Hệ thống tính Daily Calorie Summary
→ Admin xem Dashboard và AuditLog
```

## 8. Domain Glossary

| Thuật ngữ | Định nghĩa |
|---|---|
| User | Tài khoản đăng nhập trong hệ thống. |
| Role | Vai trò của user: Admin, Staff, PT, Member. |
| Member | Hội viên phòng tập. |
| PT / Trainer | Huấn luyện viên cá nhân. |
| MembershipPackage | Gói tập mẫu do Admin tạo. |
| Membership | Gói tập đã được gán cho Member, có ngày bắt đầu, ngày hết hạn và trạng thái. |
| Payment | Giao dịch thanh toán hoặc ghi nhận thanh toán thủ công. |
| Renewal | Lần gia hạn gói tập. |
| CheckIn | Lượt đến phòng tập của Member. |
| TrainerAssignment | Bản ghi phân công PT cho Member. |
| WorkoutPlan | Giáo án tập luyện do PT tạo. |
| WorkoutExercise | Bài tập thuộc giáo án. |
| TrainerNote | Ghi chú luyện tập hằng ngày của PT cho Member. |
| ProgressLog | Dữ liệu tiến độ của Member. |
| FoodItem | Món ăn trong database. |
| MealLog | Bữa ăn mà Member ghi lại. |
| MealLogItem | Từng món ăn trong một MealLog. |
| CalorieTarget | Mục tiêu calories/macros mỗi ngày. |
| AuditLog | Log hệ thống cho hành động quan trọng. |
| Image Food Recognition Assist | Tính năng enhancement dùng AI/vision để đọc ảnh bữa ăn và gợi ý tên món hoặc nguyên liệu. Tính năng này không tự định lượng calories; user vẫn phải xác nhận món và khẩu phần trước khi lưu MealLog. |

## 9. Key Assumptions

| ID | Assumption |
|---|---|
| A-01 | Hệ thống phục vụ 1 phòng tập, chưa hỗ trợ multi-branch. |
| A-02 | Giao dịch thanh toán trong MVP được ghi nhận thủ công, chưa tích hợp payment gateway. |
| A-03 | Meal journal core được nhập thủ công qua food database/custom food. |
| A-04 | Image Food Recognition Assist không thuộc core/secondary; nếu làm sau secondary thì chỉ dùng ảnh để gợi ý tên món/nguyên liệu, calories vẫn do hệ thống tính từ food database và phần user xác nhận. |
| A-05 | Dashboard lấy dữ liệu từ workflow thật, không chỉ là số mock. |
| A-06 | Member có thể có 0 hoặc 1 PT active tại một thời điểm. |
| A-07 | Audit log phải được tạo cho các hành động quan trọng. |

## 10. Open Questions

| ID | Câu hỏi | Trạng thái |
|---|---|---|
| OQ-01 | Hệ thống dùng 3 roles hay 4 roles? | Resolved: 4 roles — Admin, Staff, PT, Member |
| OQ-02 | Frontend ưu tiên React/Next.js, có cần chốt chính thức không? | Resolved: Next.js |
| OQ-03 | Backend dùng ASP.NET Core, Supabase, hay phương án khác? | Resolved: ASP.NET Core 10 Web API (.NET 10) |
| OQ-04 | Database dùng SQL Server, PostgreSQL/Supabase hay khác? | Resolved: SQL Server + EF Core 10 Code First |
| OQ-05 | Member có được tự gia hạn gói không, hay chỉ Admin/Staff? | Resolved: Member được gửi yêu cầu gia hạn, Admin/Staff xác nhận thanh toán |
| OQ-06 | Check-in giới hạn 1 lần/ngày hay cho phép nhiều lần/ngày? | Resolved: MVP cho phép nhiều lần/ngày; có thể cấu hình giới hạn sau |
| OQ-07 | Barcode lookup có nằm trong secondary scope không? | Resolved: Secondary/Optional |
| OQ-09 | Image Food Recognition Assist có được đưa vào enhancement sau secondary không? | Resolved: Enhancement; dùng Google Cloud Vision API nếu còn thời gian |
| OQ-08 | Dashboard cần realtime hay chỉ cập nhật theo request? | Resolved: cập nhật theo request/MVP; realtime là optional |


## 11. Approved Technology Stack

| Layer | Công nghệ |
|---|---|
| Frontend | Next.js |
| Backend | C# / ASP.NET Core 10 Web API (.NET 10) |
| Database | SQL Server |
| ORM | Entity Framework Core 10 - Code First Migrations |
| Authentication | JWT Bearer Token + BCrypt |
| Token Policy | Access Token 15 phút, Refresh Token 7 ngày |
| AI Vision | Google Cloud Vision API |
| Push Notification | Firebase Cloud Messaging |
| File Storage | Azure Blob Storage |
| Frontend Deploy | Vercel |
| Backend Deploy | Azure App Service |
| Version Control | GitHub Monorepo |
| API Testing | Postman / Thunder Client |
