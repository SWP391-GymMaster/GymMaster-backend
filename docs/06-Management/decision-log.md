# 12 — Decision Log (ADR)

> Ghi lại quyết định quan trọng + lý do. Tránh "Context Amnesia" (sách Ch.8.5). Mỗi quyết định: bối cảnh → lựa chọn → lý do → hệ quả.

| ID | Ngày | Quyết định | Lý do | Hệ quả |
|---|---|---|---|---|
| D-01 | 2026-05 | Dùng **4 role**: Admin, Staff, PT, Member | Phản ánh vận hành gym thực tế; Staff tách khỏi Admin | RBAC 4 nhóm; thêm UC-03A quản lý Staff |
| D-02 | 2026-05 | Backend **C#/ASP.NET Core 8 Web API** | Phù hợp môn học SWP391, team quen | Layered architecture |
| D-03 | 2026-05 | Database **MySQL** | ~~Free, phổ biến~~ | **Superseded by D-17** |
| D-04 | 2026-05 | ORM **EF Core 8 Code-First** | Migration versioned, đồng bộ team | Không sửa DB bằng tay |
| D-05 | 2026-05 | Auth **JWT Bearer + BCrypt**; ký bằng **HS256** (đối xứng), không dùng RS256 | Stateless, chuẩn ngành. Chọn HS256 vì chỉ có **một** service phát hành **và** verify token — không có bên thứ ba nào cần verify độc lập, nên không cần tách khoá ký/khoá công khai của RS256. HS256 cũng nhẹ hơn và bớt một việc vận hành (quản lý cặp khoá) | Access 15', Refresh 7d; bảng refresh_tokens. **Hệ quả:** `Jwt:SecretKey` là bí mật chung — đổi khoá phải deploy lại; nếu sau này có service khác cần verify token thì phải chuyển sang RS256 |
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
| D-19 | 2026-06-25→28 | **Deploy Google Cloud Run + Cloud SQL** (cả FE + BE) — thay Vercel + Azure App Service (superseded D-06 phần deploy) | Gom full-stack về một nền GCP để demo; $300 credit dùng thử; Cloud SQL for SQL Server | FE `gymmaster-os`, BE `gymmaster-api`, region `asia-southeast1`; Cloud SQL `gymmaster-sql-sg`; env qua Cloud Run env vars |
| D-20 | 2026-06 | **Avatar → Cloudinary** (thay Azure Blob, superseded D-07) | Không cần Azure; Cloudinary free tier đủ demo | `IAvatarStorage`/`CloudinaryAvatarStorage`; URL lưu `users.AvatarUrl` |
| D-21 | 2026-06-26 | **AI quét ảnh món ăn → Google Gemini Vision** (`gemini-2.5-flash`, thay Google Cloud Vision — OQ-09) | Gemini nhận nhiều món + ước lượng dinh dưỡng/gram trong 1 call, phù hợp hơn Vision (chỉ nhãn) | `GeminiService`/`IFoodImageAnalyzer`; spec 009 |
| D-22 | 2026-06-26 | **Thanh toán online VNPay sandbox** (override ADR-03 thủ công) | Yêu cầu giảng viên phải có luồng online thực | HMAC-SHA512 + IPN auto-activate; spec 010 |
| D-23 | 2026-06 | **Reset mật khẩu bằng OTP 6 số qua email** (SMTP Gmail) — thay token link dài | UX quen thuộc + gửi email thật | `password_reset_tokens.AttemptCount`; spec 001 |
| D-25 | 2026-07 | **Xoá cứng giáo án + ghi chú PT** — ngoại lệ có chủ ý của **DATA-01** (soft delete). Áp cho `DELETE /workout-plans/{id}` (cascade `workout_exercises`) và `DELETE /trainer-notes/{id}` | DATA-01 bảo vệ dữ liệu **nghiệp vụ/chứng từ** (Member, Membership, Payment). Giáo án và ghi chú là **bản nháp huấn luyện** — PT sửa/xoá liên tục trong quá trình kèm; soft delete sẽ buộc mọi truy vấn lọc thêm `IsDeleted` mà gần như không mang lại giá trị truy vết | Bù bằng **audit log**: `DELETE_WORKOUT_PLAN`, `DELETE_TRAINER_NOTE` ghi ai xoá, lúc nào, bản ghi nào — xem `constraints/safety.md` SAFE-08. Đây là **2 chỗ xoá cứng duy nhất** trong hệ thống; thêm chỗ mới phải có ADR riêng |
| D-26 | 2026-07 | **Gom luật nối hạn membership về một nguồn** — `ApplyPaidRenewalWindow` → `MembershipLifecycle.cs`; `CancelSiblingPendingAsync` + `SaveActivationAsync` → `MembershipActivation.cs` | `MembershipService` và `VnPayService` mỗi bên giữ một bản sao giống hệt nhau của 3 hàm này. Sửa một bên quên bên kia → luồng thu tiền tay và luồng VNPay kích hoạt gói khác nhau (sai hạn gói, sai doanh thu). Đợt gom `MembershipLifecycle` trước đó đã bỏ sót 3 hàm này | `grep ApplyPaidRenewalWindow` còn **1** định nghĩa. Thêm `InternalsVisibleTo` cho project test + 18 unit test cho `MembershipLifecycle`. Thi hành [`GBL-02`](../01-SRS-Requirements/constraints/global.md) |
| D-24 | 2026-07-23 | **Sửa CONSTITUTION ARCH-01 + ARCH-04 cho khớp code** (v1.2.0 → **v1.3.0**). ARCH-01: "Layered + Repository" → **Vertical Slice**, service gọi thẳng `DbContext`. ARCH-04: "EF Core Migration (Code First)" → **Database First**, schema do team DB sở hữu qua script SQL. | Rà `CONSTITUTION.md` ↔ code phát hiện 2 điều luật **chưa bao giờ khớp code**: không có tầng Repository nào tồn tại, và `Program.cs` ghi rõ backend không tạo/sửa schema. Luật mô tả sai thực tế thì không ai kiểm tra được, làm hỏng giá trị của Constitution Check trong 10 `plan.md`. **Sửa luật cho khớp code** (code đang chạy production, cả hai lựa chọn đều có lý do thiết kế đã ghi). | Đồng bộ `.specify/memory/constitution.md`; chi tiết ở `constraints/global.md` GBL-07/GBL-08. **Hệ quả cần nhớ khi lập kế hoạch:** việc cần cột DB mới bị chặn bởi team DB (B-01 snapshot macro, B-17 `provider_ref`). |
| D-27 | 2026-07-24 | **Khôi phục MemberProfile đã soft-delete tại chỗ** trong flow relink và `/members/me`, không chèn profile thứ hai | `member_profiles.UserId` có unique index không filter. FR-MEM-04 giữ row khi xóa mềm, còn FR-MEM-06 tự tạo khi không thấy profile active; nếu luôn INSERT, SQL Server trả unique violation và làm mất liên kết lịch sử nếu đổi ID. Restore cùng row là cách duy nhất thỏa cả hai rule mà không đổi schema/API | Giữ nguyên `MemberId` cùng membership/payment/progress/assignment; set `IsDeleted=0`, cập nhật `UpdatedAt`, ghi `RESTORE_MEMBER`. Profile chỉ được restore khi User role Member còn tồn tại và chưa soft-delete |

> Khi đổi một quyết định: thêm dòng mới với ID mới, đánh dấu dòng cũ "Superseded by D-xx", KHÔNG xóa lịch sử. (D-06 phần deploy Vercel & D-07 Azure Blob → superseded bởi D-19/D-20.)

---

# Bản đồ ADR → quyết định cấp feature

Mỗi `docs/03-Interface-Specs/feature-specs/*/plan.md` có bảng **Design Decisions** riêng, đánh số theo feature (`D-001…D-1012`). Đó là **chi tiết hoá** của các ADR cấp dự án bên trên, **không phải hệ đánh số thay thế**. Đọc từ trên xuống: ADR nói *chọn cái gì*, plan nói *cài đặt ra sao và đánh đổi gì*.

| ADR dự án | Chi tiết hoá ở | Quyết định cấp feature liên quan |
|---|---|---|
| **D-01** 4 role | [001/plan.md](../03-Interface-Specs/feature-specs/001-auth-rbac/plan.md) | RBAC qua `[Authorize(Roles=…)]`, claim từ JWT |
| **D-05** JWT Bearer + BCrypt | [001/plan.md](../03-Interface-Specs/feature-specs/001-auth-rbac/plan.md) | D-001 HS256 · D-003 rotate refresh · D-004 hash cả token · D-008 ClockSkew=0 |
| **D-23** OTP 6 số reset mật khẩu | [001/plan.md](../03-Interface-Specs/feature-specs/001-auth-rbac/plan.md) | D-005 OTP thay link · giới hạn `AttemptCount` 3 lần |
| **D-11** API contract `{success,data,error,meta}` | mọi plan §3 | Constitution Check ARCH-01 |
| **D-09** Soft delete | [002/plan.md](../03-Interface-Specs/feature-specs/002-member-management/plan.md) | D-108 unique index có filter `IsDeleted=0` · **ngoại lệ**: [005/plan.md](../03-Interface-Specs/feature-specs/005-pt-training/plan.md) D-509 xoá cứng giáo án/ghi chú |
| **D-20** Avatar → Cloudinary | [002/plan.md](../03-Interface-Specs/feature-specs/002-member-management/plan.md) | D-107 DB chỉ lưu URL |
| **D-13** Membership `PendingPayment` | [003/plan.md](../03-Interface-Specs/feature-specs/003-membership-billing/plan.md) | **D-201** tách bán/thu tiền · D-203 TTL 30 phút · D-210 tái dùng đơn Pending |
| **D-17** SQL Server (canonical) | [003](../03-Interface-Specs/feature-specs/003-membership-billing/plan.md) · [007](../03-Interface-Specs/feature-specs/007-nutrition-calorie/plan.md) | D-209 `AppClock` giờ VN · D-704 collation `Latin1_General_100_CI_AI` |
| **D-19** Cloud Run + Cloud SQL | [003/plan.md](../03-Interface-Specs/feature-specs/003-membership-billing/plan.md) | **D-204** lazy expire thay background job (do scale-to-zero) |
| **D-12** Tối đa 1 PT active | [005/plan.md](../03-Interface-Specs/feature-specs/005-pt-training/plan.md) | **D-501** mô hình 1-1 · D-502 tự đóng assignment cũ · D-503 quyền PT suy từ gói |
| **D-10** Audit log cho action mutating | [008/plan.md](../03-Interface-Specs/feature-specs/008-dashboard-audit/plan.md) | D-806 `UserId` từ HttpContext · D-807 nullable cho hành động hệ thống · D-809 append-only |
| **D-08** AI Vision = enhancement only | [009/plan.md](../03-Interface-Specs/feature-specs/009-image-food-recognition/plan.md) | **D-903** không tự tạo `FoodItem`/`MealLog` · D-902 stateless |
| **D-21** Gemini Vision thay Cloud Vision | [009/plan.md](../03-Interface-Specs/feature-specs/009-image-food-recognition/plan.md) | **D-901** cổng `IFoodImageAnalyzer` — nhờ nó việc đổi nhà cung cấp chỉ sửa 1 file |
| **D-22** VNPay sandbox (override ADR-03) | [010/plan.md](../03-Interface-Specs/feature-specs/010-online-payment-vnpay/plan.md) | D-1001 không đổi schema · D-1002 IPN là nguồn sự thật · D-1004 idempotent · D-1005 giá lấy từ server |
| **D-14** Coverage ≥ 80% business logic | [docs/03-Interface-Specs/feature-specs/BACKLOG.md](../03-Interface-Specs/feature-specs/BACKLOG.md) | **chưa đạt** — 5 service thiếu unit test (B-03…B-07) |

**Quyết định cấp feature chưa có ADR dự án tương ứng** — cân nhắc nâng lên bảng trên nếu ảnh hưởng vượt khỏi một feature:

| Quyết định | Ở đâu | Vì sao đáng cân nhắc |
|---|---|---|
| Gom luật vòng đời vào `MembershipLifecycle` | [003](../03-Interface-Specs/feature-specs/003-membership-billing/plan.md) D-202 | **7 feature** phụ thuộc; từng có 3 bản sao lệch nhau |
| Ngày nghiệp vụ theo giờ VN (`AppClock`) | [003](../03-Interface-Specs/feature-specs/003-membership-billing/plan.md) D-209 | áp cho 003·004·006·007·008 |
| Quy tắc "1 ngày = 1 bản ghi" tiến độ | [006](../03-Interface-Specs/feature-specs/006-progress-tracking/plan.md) D-601 | ảnh hưởng cách FE vẽ biểu đồ |
| Tier free 20 món | [007](../03-Interface-Specs/feature-specs/007-nutrition-calorie/plan.md) D-705 | là **ràng buộc thương mại**, không phải kỹ thuật |
| Chọn HS256 thay RS256 | [001](../03-Interface-Specs/feature-specs/001-auth-rbac/plan.md) D-001 | → việc B-15 trong BACKLOG |
