# Global Technical Constraints — GymMaster

**Phiên bản:** 1.1 · **Áp dụng cho:** backend .NET 10 (`backend/GymMaster.API/`)

> **Quan hệ với [`CONSTITUTION.md`](../../../CONSTITUTION.md).** File đó là **luật gốc**, sửa cần đồng thuận toàn team. File này **không lặp lại**, mà ghi các ràng buộc kỹ thuật **đang có hiệu lực trong code** nhưng chưa được viết thành luật.
>
> **Thứ tự ưu tiên khi xung đột:** `CONSTITUTION.md` → file này → Design Decisions trong `plan.md`.

---

## Tech stack

| Tầng | Công nghệ | Ghi chú |
|---|---|---|
| Backend | C# 13 / ASP.NET Core 10 (`net10.0`) | |
| Database | SQL Server 2022 + EF Core 10 | schema do team DB sở hữu (GBL-08) |
| Frontend | Next.js + TypeScript | repo riêng `GymMaster-frontend` |
| Auth | JWT Bearer HS256 + BCrypt cost 12 | |
| Deploy | Google Cloud Run + Cloud SQL | `asia-southeast1` |
| Ảnh đại diện | Cloudinary | chỉ lưu URL |
| AI Vision | Gemini `gemini-2.5-flash` | enhancement, spec 009 |
| Thanh toán online | VNPay sandbox | spec 010 |

Chi tiết phiên bản package → [`CONSTITUTION.md`](../../../CONSTITUTION.md) Layer 3.

---

## GBL — Bảng tra nhanh

| ID | Ràng buộc | Thi hành ở | Áp cho spec | Trạng thái |
|---|---|---|---|---|
| **GBL-01** | Ngày nghiệp vụ theo **giờ VN** | `Common/AppClock.cs` | 003·004·006·007·008 | ✅ |
| **GBL-02** | Luật **vòng đời membership** = một nguồn | `Features/Billing/MembershipLifecycle.cs` | 003·004·005·006·007·009·010 | ✅ |
| **GBL-03** | Validate dữ liệu người ở một chỗ | `Common/PersonValidation.cs` | 002 | ✅ |
| **GBL-04** | Kiểm quyền ở **Service**, không ở Controller | từng `*Service.cs` | mọi spec | ✅ |
| **GBL-05** | Identity **chỉ** từ JWT claim | `Common/ApiControllerBase.cs` | mọi spec | ✅ |
| **GBL-06** | Ngưỡng vận hành đưa ra **config** | `Options/*.cs` | 004·009·010 | ✅ |
| **GBL-07** | **Vertical slice**, không có Repository | `Features/<Tên>/` | mọi spec | ✅ |
| **GBL-08** | Schema DB do **team DB** sở hữu | `database/*.sql` | mọi spec | ✅ |
| **GBL-09** | Read-model tính lúc gọi, **không cache** | `DashboardService` · `ProgressService` | 006·008 | ✅ |
| **GBL-10** | **Không** tiến trình chạy nền | — | 003·004 | ✅ |

**Kết quả rà code: 10/10 đạt** — chi tiết ở [bảng kiểm chứng](#kiểm-chứng-bằng-code).

---

## Chi tiết từng ràng buộc

### GBL-01 · Ngày nghiệp vụ tính theo giờ VN

| | |
|---|---|
| **Quy định** | Mọi so sánh "hôm nay", "tháng này", khung ngày, giờ cao điểm **SHALL** đi qua `Common/AppClock.cs` (GMT+7). **KHÔNG** dùng `DateTime.Today` / `DateTime.Now`. |
| **Vì sao** | Container Cloud Run chạy UTC. Dùng giờ UTC thì gói hết hạn lệch 1 ngày vào rạng sáng, doanh thu rơi sai tháng ở đầu/cuối tháng. |
| **Ngoại lệ** | `DateTime.UtcNow` cho **dấu thời gian** (`CreatedAt`, `PaidAt`, `CheckInAt`) là đúng — chỉ **ngày nghiệp vụ** mới cần giờ VN. |
| **Kiểm chứng** | `AppClock.` xuất hiện **16 lần**; `DateTime.Today\|Now` xuất hiện **0 lần** |

### GBL-02 · Luật vòng đời membership = một nguồn duy nhất

| | |
|---|---|
| **Quy định** | Định nghĩa **"gói còn hiệu lực"** và các quy tắc hết hạn (`IsActiveOn`, `ExpireIfPastDue`, `ExpireStalePending`) **SHALL** chỉ tồn tại ở `Features/Billing/MembershipLifecycle.cs`. Service khác **KHÔNG** được viết lại. |
| **Vì sao** | Đã từng có **3 bản sao** ở `MembershipService`/`ProgressService`/`VnPayService` lệch nhau, gây lỗi *"gói hết hạn vẫn check-in được"*. |
| **Phạm vi** | Luật **vòng đời** (trạng thái gói). Các helper quanh việc kích hoạt/nối hạn nằm ngoài phạm vi luật này — xem ghi chú bên dưới. |
| **Kiểm chứng** | `MembershipLifecycle.` được gọi **12 lần** từ 4 service · **1** định nghĩa duy nhất cho mỗi hàm vòng đời |

> **Nợ kỹ thuật liên quan (không thuộc phạm vi GBL-02).** Ba helper quanh luật **nối hạn** hiện có hai bản giống nhau:
>
> | Hàm | `MembershipService.cs` | `VnPayService.cs` |
> |---|---|---|
> | `ApplyPaidRenewalWindow` | L596 | L292 |
> | `CancelSiblingPendingAsync` | L558 | L244 |
> | `SaveActivationAsync` | L573 | L259 |
>
> Cả hai bản hiện **giống hệt nhau**, nên hành vi của luồng thu tiền tay và luồng VNPay đang **thống nhất**. Rủi ro là ở tương lai: sửa một bên quên bên kia thì hai luồng kích hoạt gói sẽ lệch.
> **Kế hoạch gom về một nguồn:** việc **B-20** trong [BACKLOG](../../03-Interface-Specs/feature-specs/BACKLOG.md).

### GBL-03 · Validate dữ liệu người dùng ở một chỗ

| | |
|---|---|
| **Quy định** | Validate `dob` / `gender` / `phone` / `emergencyContact` **SHALL** dùng `Common/PersonValidation.cs`. |
| **Vì sao** | Bốn slice cùng nhận hồ sơ người; validate riêng lẻ sẽ cho kết quả khác nhau trên cùng dữ liệu. |
| **Kiểm chứng** | Được dùng ở **4/4** service liên quan: `UserService` · `MemberService` · `TrainerService` · `AccountService` (8 lời gọi) |

### GBL-04 · Kiểm quyền ở tầng Service, không ở Controller

| | |
|---|---|
| **Quy định** | Ownership check (Member chỉ dữ liệu của mình; PT chỉ member được phân công) **SHALL** nằm trong Service. Controller chỉ gác role bằng `[Authorize(Roles=…)]`. |
| **Vì sao** | Một nghiệp vụ thường có nhiều đường vào (2 controller khác gốc route) — đặt ở controller sẽ sót đường. |
| **Kiểm chứng** | `Status403Forbidden` trong Service: **39 lần** · trong Controller: **0 lần** |

### GBL-05 · Identity chỉ lấy từ JWT claim

| | |
|---|---|
| **Quy định** | `userId` / `role` **SHALL** đọc qua `ApiControllerBase` (`CurrentUserId`, `CurrentRole`). **KHÔNG BAO GIỜ** đọc từ request body hoặc query. |
| **Vì sao** | Nhận `userId` từ body cho phép bất kỳ ai thao tác trên tài khoản người khác. |
| **Kiểm chứng** | **24/24** controller kế thừa `ApiControllerBase` · **24/24** controller có `[Authorize]`* |

> \* `Program.cs` dùng `AddAuthorization()` **không có fallback policy** — nghĩa là endpoint thiếu `[Authorize]` sẽ **công khai**. Hiện không controller nào thiếu, nhưng **controller mới bắt buộc phải tự thêm**. Cân nhắc đặt `FallbackPolicy = RequireAuthenticatedUser()` để an toàn mặc định.

### GBL-06 · Ngưỡng vận hành đưa ra cấu hình

| | |
|---|---|
| **Quy định** | Ngưỡng có thể đổi theo phòng gym **SHALL** nằm trong `Options/*.cs` bind từ configuration, không hard-code. |
| **Vì sao** | Đổi chính sách không cần build lại; sandbox ↔ live chỉ đổi config. |
| **Kiểm chứng** | **7** lời gọi `Configure<>` trong `Program.cs`: `Jwt` · `GoogleAuth` · `Cloudinary` · `CheckIn` · `VnPay` · `Email` · `Gemini` |
| **Ngoại lệ đã biết** | Sức chứa cơ sở **50** (tính `facilityLoadPercent`, spec 008) đang hard-code — chấp nhận vì đồ án một cơ sở |

### GBL-07 · Vertical slice theo feature

| | |
|---|---|
| **Quy định** | Code tổ chức theo `Features/<Tên>/`, mỗi slice tự chứa controller + service + DTO. Service gọi **thẳng `DbContext`**, **không có tầng Repository**. |
| **Vì sao** | EF Core `DbSet` đã đóng vai repository; thêm tầng nữa chỉ tăng số file mà không tăng khả năng test (test dùng EF InMemory) — xem [`001-auth-rbac/plan.md`](../../03-Interface-Specs/feature-specs/001-auth-rbac/plan.md) D-002. |
| **Kiểm chứng** | **0** thư mục `Repositor*` · **0** interface `I*Repository` |
| **Luật gốc** | Đồng bộ `CONSTITUTION.md` **ARCH-01** (v1.3.0, ADR D-24) |

### GBL-08 · Schema DB do team DB sở hữu (Database First)

| | |
|---|---|
| **Quy định** | Thay đổi schema đi qua **script SQL đánh số** trong `database/`. Backend **KHÔNG** tự tạo/sửa schema; `DatabaseSeeder` chỉ seed 4 role + tài khoản admin. |
| **Kiểm chứng** | **0** thư mục `Migrations/` · **0** lời gọi `Database.Migrate()` / `EnsureCreated()` · **11** file `.sql` trong `database/` |
| **Luật gốc** | Đồng bộ `CONSTITUTION.md` **ARCH-04** (v1.3.0, ADR D-24) |

> ⚠️ **Hệ quả khi lập kế hoạch:** mọi việc cần **cột DB mới** đều bị **chặn bởi team DB** — vd **B-01** (snapshot macro) và **B-17** (`provider_ref`). Đặt yêu cầu sớm.

### GBL-09 · Read-model tính lúc gọi, không cache

| | |
|---|---|
| **Quy định** | Dashboard (008) và Member 360° (006) **SHALL** tính trực tiếp từ DB mỗi lần gọi. |
| **Vì sao** | Yêu cầu "số liệu thật, khớp DB". Cache khiến Admin bán gói xong không thấy doanh thu đổi → mất niềm tin vào số liệu. |
| **Đánh đổi** | Phải giữ trong ngân sách hiệu năng: dashboard < 2s, 360° < 1.5s — **chưa có số đo** (B-11, B-12) |
| **Kiểm chứng** | **0** lời gọi `IMemoryCache` / `IDistributedCache` / `ResponseCache` |

### GBL-10 · Không có tiến trình chạy nền

| | |
|---|---|
| **Quy định** | **KHÔNG** dùng `BackgroundService` / `IHostedService` cho nghiệp vụ. Việc đến hạn xử lý **lazy** khi có truy vấn. |
| **Vì sao** | Cloud Run **scale-to-zero** — không request thì không container nào chạy, job nền không đáng tin. |
| **Hệ quả** | `MembershipLifecycle.ExpireIfPastDue` / `ExpireStalePending` chạy khi có người đọc; truy vấn DB trực tiếp có thể thấy `Active` đã quá hạn |
| **Kiểm chứng** | **0** lời gọi `BackgroundService` / `IHostedService` / `AddHostedService` |

---

## Kiểm chứng bằng code

Chạy từ thư mục gốc repo để tự kiểm tra lại:

| ID | Lệnh kiểm | Kỳ vọng | Thực tế |
|---|---|---|---|
| GBL-01 | `grep -rE "DateTime\.(Today\|Now)\b" backend --include=*.cs` | 0 | **0** ✅ |
| GBL-02 | `grep -rc "MembershipLifecycle\." backend --include=*.cs` | > 0, không có bản sao | **12 lời gọi / 1 định nghĩa** ✅ |
| GBL-03 | `grep -rl "PersonValidation\." backend --include=*.cs` | 4 service | **4** ✅ |
| GBL-04 | `grep -rc "Status403Forbidden" backend/**/​*Controller.cs` | 0 | **0** ✅ |
| GBL-05 | `grep -rc ": ApiControllerBase" backend --include=*.cs` | = số controller | **24/24** ✅ |
| GBL-06 | `grep -c "Services.Configure<" backend/GymMaster.API/Program.cs` | 7 | **7** ✅ |
| GBL-07 | `find backend -type d -name "Repositor*"` | rỗng | **rỗng** ✅ |
| GBL-08 | `grep -rE "Database\.Migrate\(\)\|EnsureCreated\(\)" backend --include=*.cs` | 0 | **0** ✅ |
| GBL-09 | `grep -rE "IMemoryCache\|IDistributedCache" backend --include=*.cs` | 0 | **0** ✅ |
| GBL-10 | `grep -rE "BackgroundService\|IHostedService" backend --include=*.cs` | 0 | **0** ✅ |

> Bảng này nên chạy lại mỗi lần review kiến trúc. **Một luật không kiểm chứng được bằng lệnh là luật sẽ bị vi phạm âm thầm.**
>
> Nợ kỹ thuật đang theo dõi (không phải vi phạm luật hiện hành): `grep -rc "ApplyPaidRenewalWindow"` trả về **2** — ba helper nối hạn còn hai bản, kế hoạch gom ở **B-20**.

## Cách dùng khi review

Mỗi `plan.md` có mục **§3 Constitution Check** đối chiếu feature với: `SEC-*`/`ARCH-*`/`DATA-*`/`AUDIT-*` (luật gốc) + `GBL-*` (file này) + `BIZ-*` ([business.md](business.md)) + `SAFE-*` ([safety.md](safety.md)).

| Trạng thái | Nghĩa |
|---|---|
| ✅ PASS | Đạt, có bằng chứng trong code |
| ⚠️ PARTIAL | Đạt một phần — bắt buộc nêu phần thiếu + việc trong BACKLOG |
| ⚠️ LỆCH CÓ CHỦ Ý | Cố ý không tuân — bắt buộc có lý do ở §8 Complexity Tracking **và** một dòng ADR |
| N/A | Luật không áp dụng cho feature này |
