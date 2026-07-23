# Global Technical Constraints — GymMaster

**Phiên bản:** 1.0 · **Áp dụng cho:** backend .NET 10

> **Quan hệ với `CONSTITUTION.md`.** File đó là **luật gốc** (Layer 1 Hard Rules · Layer 2 Architectural · Layer 3 Engineering) — sửa cần đồng thuận toàn team. File này **không lặp lại** luật đó, mà bổ sung các **ràng buộc kỹ thuật đang thực sự có hiệu lực trong code** nhưng chưa được viết thành luật, cộng với việc ghi nhận **2 chỗ luật và code đang mâu thuẫn**.
>
> Thứ tự ưu tiên khi xung đột: `CONSTITUTION.md` > file này > quyết định cấp feature trong `plan.md`.

---

## Tech stack (nguồn: `CONSTITUTION.md` Layer 3 — không lặp chi tiết ở đây)

Backend **C# / ASP.NET Core 10 (.NET 10)** · **SQL Server** + **EF Core 10** · Frontend **Next.js + TypeScript** · Auth **JWT Bearer + BCrypt** · Deploy **Google Cloud Run + Cloud SQL** (`asia-southeast1`) · Avatar **Cloudinary** · AI Vision **Gemini `gemini-2.5-flash`** · Payment **VNPay sandbox**.

---

## GBL — Ràng buộc đang có hiệu lực trong code

### GBL-01 · Ngày nghiệp vụ tính theo giờ VN
Mọi so sánh "hôm nay", "tháng này", khung ngày, giờ cao điểm **SHALL** đi qua `backend/GymMaster.API/Common/AppClock.cs` (GMT+7). **KHÔNG** dùng `DateTime.Today` / `DateTime.Now`.
*Vì sao:* container chạy UTC → gói hết hạn lệch 1 ngày vào rạng sáng, doanh thu rơi sai tháng.
*Áp cho:* spec 003, 004, 006, 007, 008.

### GBL-02 · Một luật nghiệp vụ = một nguồn duy nhất
Luật vòng đời membership **SHALL** chỉ tồn tại ở `Features/Billing/MembershipLifecycle.cs`. Service khác **KHÔNG** được tự viết lại định nghĩa "gói còn hiệu lực".
*Vì sao:* đã từng có **3 bản sao** ở `MembershipService` / `ProgressService` / `VnPayService` lệch nhau, gây lỗi "gói hết hạn vẫn check-in được".
*Áp cho:* 7 feature (003, 004, 005, 006, 007, 009, 010).

### GBL-03 · Validate dữ liệu người dùng ở một chỗ
Validate `dob` / `gender` / `phone` / `emergencyContact` **SHALL** dùng `Common/PersonValidation.cs`, không viết lại trong từng slice.

### GBL-04 · Kiểm quyền ở tầng Service, không ở Controller
Ownership check (Member chỉ dữ liệu của mình; PT chỉ member được phân công) **SHALL** nằm trong Service. Controller chỉ gác bằng `[Authorize(Roles=…)]`.
*Vì sao:* một nghiệp vụ có nhiều đường vào (2 controller khác gốc route) — đặt ở controller sẽ sót đường.

### GBL-05 · Identity chỉ lấy từ JWT claim
`userId` / `role` **SHALL** đọc qua `Common/ApiControllerBase.cs` (`CurrentUserId`, `CurrentRole`). **KHÔNG BAO GIỜ** đọc từ request body hoặc query.

### GBL-06 · Quy tắc vận hành đưa ra cấu hình
Ngưỡng vận hành có thể đổi theo phòng gym **SHALL** nằm trong `Options/*.cs` bind từ configuration, không hard-code (`CheckInOptions`, `GeminiOptions`, `VnPayOptions`).
*Vì sao:* đổi chính sách không cần build lại; sandbox ↔ live chỉ đổi config.

### GBL-07 · Vertical slice theo feature
Code tổ chức theo **`Features/<Tên>/`**, mỗi slice tự chứa controller + service + DTO. Service gọi **thẳng `DbContext`**, **không có tầng Repository**.
*Lý do:* EF Core `DbSet` đã đóng vai trò repository; thêm tầng nữa chỉ tăng số file mà không tăng khả năng test (test dùng EF InMemory) — xem [`001-auth-rbac/plan.md`](../../03-Interface-Specs/feature-specs/001-auth-rbac/plan.md) D-002.

> Đồng bộ với `CONSTITUTION.md` **ARCH-01** (v1.3.0, ADR **D-24**).

### GBL-08 · Schema DB do team DB sở hữu (Database First)
Thay đổi schema đi qua file SQL đánh số trong `database/` (`004_checkin.sql` … `013_*.sql`). Backend **KHÔNG** tự tạo/sửa schema — `Program.cs` ghi rõ điều này; `DatabaseSeeder` chỉ seed 4 role + tài khoản admin.

> Đồng bộ với `CONSTITUTION.md` **ARCH-04** (v1.3.0, ADR **D-24**).
>
> ⚠️ **Hệ quả cần nhớ khi lập kế hoạch:** mọi việc cần **cột DB mới** đều **bị chặn bởi team DB**, không tự làm được ở tầng backend — vd **B-01** (snapshot macro) và **B-17** (`provider_ref`) trong [BACKLOG](../../03-Interface-Specs/feature-specs/BACKLOG.md). Đặt yêu cầu với team DB sớm.

### GBL-09 · Read-model tính lúc gọi, không cache
Dashboard (spec 008) và Member 360° (spec 006) **SHALL** tính trực tiếp từ DB mỗi lần gọi.
*Vì sao:* yêu cầu "số liệu thật, khớp DB"; cache khiến Admin bán gói xong không thấy doanh thu đổi.
*Đánh đổi:* phải giữ trong ngân sách hiệu năng — xem `04-Test-Specs/test-plan.md`.

### GBL-10 · Không có tiến trình chạy nền
Cloud Run scale-to-zero → **KHÔNG** dùng `BackgroundService`/hosted service cho nghiệp vụ. Việc đến hạn xử lý theo kiểu **lazy** khi có truy vấn (`MembershipLifecycle.ExpireIfPastDue`).

---

## Kiểm tra tuân thủ

Mỗi `plan.md` có mục **§3 Constitution Check** đối chiếu feature đó với: luật gốc (`SEC-*`, `ARCH-*`, `DATA-*`, `AUDIT-*` từ `CONSTITUTION.md`) + `GBL-*` (file này) + `BIZ-*` ([business.md](business.md)) + `SAFE-*` ([safety.md](safety.md)).

Trạng thái dùng: ✅ PASS · ⚠️ PARTIAL · ⚠️ LỆCH CÓ CHỦ Ý (bắt buộc kèm lý do ở mục §8 Complexity Tracking) · N/A.
