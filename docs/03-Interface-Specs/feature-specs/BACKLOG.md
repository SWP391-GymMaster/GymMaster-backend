# GymMaster — Backlog kỹ thuật (agent-executable)

**Phiên bản**: 1.0 · **Nguồn**: các task `[ ]` trong `feature-specs/*/tasks.md`
**Tổng**: 14 mục còn mở · **Trạng thái code**: đang chạy production, không mục nào là blocker

> **File này dùng để làm gì.** `tasks.md` của mỗi feature là **bản ghi as-built** (hầu hết `[X]`), không chạy `/speckit-implement` được. File này gom **phần còn nợ** — mỗi mục có đủ file path, lý do và điều kiện hoàn thành để giao thẳng cho agent theo vòng B3 Implement trong [`docs/06-Management/ai-workflow.md`](../../06-Management/ai-workflow.md).
>
> **Quy tắc**: làm xong một mục → tick `[X]` **cả ở đây và ở `tasks.md` gốc** → qua Validation Gate ở [`docs/04-Test-Specs/test-plan.md`](../../04-Test-Specs/test-plan.md) §6 → ghi vào AI Interaction Log (`ai-workflow.md` §4) nếu làm bằng AI.

---

## Đã đóng

### ✅ B-19 · `CONSTITUTION.md` ARCH-01 / ARCH-04 mô tả sai code
Hai điều luật Layer 2 không khớp code: ARCH-01 đòi tầng Repository (không tồn tại), ARCH-04 đòi EF Core Migration (dự án dùng SQL script do team DB sở hữu).
**Xử lý theo hướng sửa luật cho khớp code:** `CONSTITUTION.md` → **v1.3.0** (ARCH-01 = Vertical Slice, ARCH-04 = Database First) · ADR **D-24** trong [decision-log.md](../../06-Management/decision-log.md) · đồng bộ `.specify/memory/constitution.md`.
> ⚠️ `CONSTITUTION.md` quy định thay đổi cần **đồng thuận toàn team** — bản sửa này cần được team xác nhận trong buổi họp gần nhất.

### ✅ B-20 · Ba hàm nghiệp vụ bị copy nguyên xi giữa `MembershipService` và `VnPayService` — **xong**
Đã gom về một nguồn (ADR **D-26**): `ApplyPaidRenewalWindow` → `MembershipLifecycle.cs`; `CancelSiblingPendingAsync` + `SaveActivationAsync` → `MembershipActivation.cs` mới. Gỡ hết wrapper, hai service gọi thẳng nguồn chung.
`grep ApplyPaidRenewalWindow` còn **1** định nghĩa · `activeMembership.EndDate.AddDays` còn **1** · `BeginTransactionAsync` còn **1**. Thêm 18 test cho `MembershipLifecycle`. **212/212 test xanh.**

### ✅ B-02 · ADR cho ngoại lệ DATA-01 (xoá cứng giáo án/ghi chú) — **xong**
Thêm **D-25** vào `decision-log.md`: nêu rõ phạm vi DATA-01 (dữ liệu nghiệp vụ/chứng từ), lý do giáo án + ghi chú là bản nháp huấn luyện, và audit log bù (`DELETE_WORKOUT_PLAN`, `DELETE_TRAINER_NOTE`). Ghi rõ đây là 2 chỗ xoá cứng duy nhất; thêm chỗ mới phải có ADR riêng.

### ✅ B-15 · Lý do chọn HS256 — **xong**
Bổ sung vào **D-05**: một service vừa phát hành vừa verify token, không có bên thứ ba cần verify độc lập nên không cần tách khoá của RS256. Kèm hệ quả: `Jwt:SecretKey` là bí mật chung, đổi khoá phải deploy lại.


---

## P1 — Sai lệch so với CONSTITUTION (ưu tiên cao nhất)

Đây là các mục làm hệ thống **lệch luật đã cam kết**, không phải thiếu tiện nghi.

### B-01 · Snapshot macro vào `meal_log_items` — lệch DATA-02
- **Nguồn**: [007 T031](007-nutrition-calorie/tasks.md) · [plan D-706](007-nutrition-calorie/plan.md)
- **Vấn đề**: `meal_log_items` chỉ snapshot `Calories`. Macro (protein/carb/fat) đọc **live** từ `food_items` → Admin sửa món là **số liệu dinh dưỡng lịch sử đổi theo**.
- **Việc cần làm**:
  1. Nhờ team DB thêm 3 cột nullable `ProteinG`, `CarbG`, `FatG` vào `meal_log_items`.
  2. `Features/Nutrition/NutritionService.cs` — ghi snapshot macro lúc INSERT, giống cách đang làm với `Calories`.
  3. Đọc macro từ `meal_log_items` thay vì JOIN `food_items`.
  4. Backfill dữ liệu cũ (chấp nhận dùng giá trị hiện tại của `food_items`).
- **Xong khi**: sửa macro của một `FoodItem` → `GET /members/{id}/calorie-summary` của ngày cũ **không đổi**.
- **Chặn bởi**: team DB (không tự làm được ở tầng backend).


### B-03 · Unit test `AuthService`
- **Nguồn**: [001 T020](001-auth-rbac/tasks.md)
- **Vấn đề**: 14 service khác đều có unit test, riêng `AuthService` — service nhạy cảm nhất — chỉ được phủ black-box.
- **Việc cần làm**: tạo `tests/GymMaster.Api.Tests/AuthServiceTests.cs` (xUnit + EF Core InMemory, theo mẫu `VnPayServiceTests.cs`), phủ tối thiểu:
  - khoá tạm sau 5 lần sai trong 15 phút (`LockedUntil`) → AC-03
  - rotate refresh token: token cũ bị `RevokedAt` → AC-04
  - logout revoke toàn bộ → AC-05
  - OTP sai 3 lần → vô hiệu → AC-09a
- **Xong khi**: AC-01…AC-11 của spec 001 có ít nhất một test tự động (hiện là 0).

### B-04 · Unit test `MemberService.CreateAsync` — 3 nhánh email
- **Nguồn**: [002 T042](002-member-management/tasks.md)
- **Vấn đề**: logic phức tạp nhất spec 002 (email chưa tồn tại / là Member chưa có hồ sơ → `linkedToExistingAccount` / đã có hồ sơ hoặc role khác → 409) chưa có test.
- **Việc cần làm**: `tests/GymMaster.Api.Tests/MemberServiceTests.cs`, mỗi nhánh một test + test `GET /members/me` tự tạo hồ sơ (AC-08).
- **Xong khi**: 3 nhánh + AC-08 đều có test.

### ✅ B-05 · Unit test `PaymentService` + `MembershipLifecycle` — **xong**
- **Nguồn**: [003 T042, T043](003-membership-billing/tasks.md)
- **Vấn đề**: `PaymentSummaryResponse` gom `byMethod`/`byDay` **theo giờ VN** — chỗ dễ sai nhất về múi giờ, chưa có test. `MembershipLifecycle.ExpireStalePending` (TTL 30 phút, AC-09) là logic thuần, test rất rẻ nhưng cũng chưa có.
- ✅ **Xong**: `tests/GymMaster.Api.Tests/MembershipLifecycleTests.cs` — **18 test** phủ `IsActiveOn` (kể cả biên EndDate == hôm nay), `ExpireIfPastDue`, `ExpireStalePending` (TTL 30 phút), `ApplyPaidRenewalWindow`, và một test khẳng định hai luật hết hạn **cùng chạy** trong một lượt (chống ai đó đổi toán tử `|` thành `||`).
- **Ưu tiên phụ**: `MembershipLifecycle` được **7 feature** dùng chung — test ở đây bảo vệ nhiều nhất trên một đơn vị công sức.

---

## P2 — Lỗ hổng test còn lại

- [ ] **B-06** · `WorkoutPlanServiceTests.cs` — luồng tra/tạo `exercise_catalog` theo tên (D-506) và `UpdateAsync` thay toàn bộ exercises (D-507) · [005 T038](005-pt-training/tasks.md)
- [ ] **B-07** · `TrainerNoteServiceTests.cs` — kiểm chủ sở hữu note (403 khi sửa note của PT khác) · [005 T039](005-pt-training/tasks.md)
- [ ] **B-08** · Test `GetProfile360Async` nhánh `currentMembership = null` (AC-06) — member chỉ còn đơn Cancelled/Expired · [006 T029](006-progress-tracking/tasks.md)
- [ ] **B-09** · Test tier free 20 món (AC-07) — giới hạn universe **trước** khi lọc từ khoá · [007 T032](007-nutrition-calorie/tasks.md)
- [ ] **B-10** · Test đầu-cuối: Gemini timeout → 502 mà luồng nhập tay spec 007 vẫn chạy (AC-06) · [009 T026](009-image-food-recognition/tasks.md)

---

## P3 — Chưa có số đo NFR nào

5 feature khai báo ngưỡng hiệu năng trong spec nhưng **chưa mục nào được đo**. Làm chung một đợt bằng `tests/blackbox/Performance.Tests.ps1`, ghi kết quả vào `docs/04-Test-Specs/test-plan.md`.

| | Feature | Ngưỡng cam kết | Nguồn |
|---|---|---|---|
| [ ] **B-11** | 008 Dashboard | < 2s với ~1000 hội viên — **endpoint nặng nhất** (15 chỉ số, không cache) | [008 T035](008-dashboard-audit/tasks.md) |
| [ ] **B-12** | 006 Member 360° | < 1.5s — ~6 query/lần gọi; vượt thì cân nhắc gộp truy vấn | [006 T028](006-progress-tracking/tasks.md) |
| [ ] **B-13** | 004 Check-in | < 300ms P95 · ~50 check-in/phút | [004 T025](004-checkin/tasks.md) |
| [ ] **B-14** | 002 Tìm kiếm hội viên | < 1s với 1000 hội viên | [002 T043](002-member-management/tasks.md) |

> Cần seed ~1000 hội viên để đo cho đúng điều kiện — dùng `Data/DatabaseSeeder.cs` hoặc `database/010_demo_active_member_seed.sql` làm điểm khởi đầu.

---

## P4 — Tài liệu & kiến trúc (không gấp)

- [ ] **B-16** · Cân nhắc chuyển `IAuditService` từ `Features/Dashboard/` sang `Infrastructure/` — **chỉ làm khi** xuất hiện nơi tiêu thụ audit thứ hai; hiện 5 slice phải `using Features.Dashboard` chỉ để ghi log · [008 T036](008-dashboard-audit/tasks.md)

## P5 — Chỉ làm khi chuyển VNPay sang live

Không phải nợ của bản sandbox — sandbox đã đúng và đủ.

- [ ] **B-17** · Thêm cột `provider_ref` / `bank_txn_no` nullable vào `payments` (cần team DB) để lưu mã giao dịch VNPay phục vụ đối soát · [010 T036](010-online-payment-vnpay/tasks.md)
- [ ] **B-18** · Thêm `PaymentMethod.Online` để báo cáo `byMethod` tách được tiền online với chuyển khoản tay; hiện dùng chung `Transfer`. **Cảnh báo**: đụng mọi query thống kê của spec 003 · [010 T037](010-online-payment-vnpay/tasks.md)

---

## Thứ tự đề xuất

```text
B-01 (chờ team DB — đặt yêu cầu ngay, có phụ thuộc ngoài)
  ↓
B-03 (AuthService — service nhạy cảm nhất, chưa có test)  →  B-04
  ↓
B-06…B-10 (test, làm song song được)
  ↓
B-11…B-14 (đo NFR một đợt, cần seed ~1000 hội viên)
  ↓
B-16 · B-17, B-18 (khi live)
```

**Bắt đầu bằng B-02** vì nó là *quyết định* chứ không phải code — chốt xong mới biết có phải viết code hay chỉ viết ADR. **B-01 nên đặt yêu cầu với team DB ngay** dù chưa làm, vì đó là mục duy nhất bị chặn bởi bên ngoài.
