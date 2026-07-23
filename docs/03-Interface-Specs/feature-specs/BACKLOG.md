# GymMaster — Backlog kỹ thuật (agent-executable)

**Lập ngày**: 2026-07-23 · **Nguồn**: rút từ các task `[ ]` trong `docs/03-Interface-Specs/feature-specs/*/tasks.md` khi đối chiếu spec ↔ code
**Tổng**: 18 mục còn mở · **Trạng thái code**: đang chạy production, không mục nào là blocker
**Cập nhật 2026-07-23 (lần 2):** rà lại code phát hiện **B-02 và B-19 không còn hiệu lực như mô tả ban đầu** — xem [mục Đã đóng](#đã-đóng).

> **File này dùng để làm gì.** `tasks.md` của mỗi feature là **bản ghi as-built** (hầu hết `[X]`), không chạy `/speckit-implement` được. File này gom **phần còn nợ** — mỗi mục có đủ file path, lý do và điều kiện hoàn thành để giao thẳng cho agent theo vòng B3 Implement trong [`docs/06-Management/ai-workflow.md`](../../06-Management/ai-workflow.md).
>
> **Quy tắc**: làm xong một mục → tick `[X]` **cả ở đây và ở `tasks.md` gốc** → qua Validation Gate ở [`docs/04-Test-Specs/test-plan.md`](../../04-Test-Specs/test-plan.md) §6 → ghi vào AI Interaction Log (`ai-workflow.md` §4) nếu làm bằng AI.

---

## Đã đóng

### ✅ B-19 · `CONSTITUTION.md` ARCH-01 / ARCH-04 mô tả sai code — **xong 2026-07-23**
Hai điều luật Layer 2 chưa bao giờ khớp code: ARCH-01 đòi tầng Repository (không tồn tại), ARCH-04 đòi EF Core Migration (dự án dùng SQL script do team DB sở hữu).
**Đã xử lý theo hướng sửa luật cho khớp code:** `CONSTITUTION.md` → **v1.3.0** (ARCH-01 = Vertical Slice, ARCH-04 = Database First) · ADR **D-24** trong [decision-log.md](../../06-Management/decision-log.md) · đồng bộ `.specify/memory/constitution.md` · gỡ cảnh báo ở [global.md](../../01-SRS-Requirements/constraints/global.md) GBL-07/GBL-08.
> ⚠️ `CONSTITUTION.md` quy định thay đổi cần **đồng thuận toàn team** — bản sửa này cần được team xác nhận lại trong buổi họp gần nhất.

### ✅ B-02 · "Giáo án + ghi chú chưa ghi AuditLog" — **là lỗi tài liệu, không phải lỗi code**
Rà code 2026-07-23: `WorkoutPlanService` **đã ghi** `CREATE/UPDATE/DELETE_WORKOUT_PLAN`, `TrainerNoteService` **đã ghi** `CREATE/UPDATE/DELETE_TRAINER_NOTE`. Tổng cộng **34 action audit / 13 service** — AUDIT-01 phủ đủ.
Bản tài liệu đầu tiên (2026-07-23) khẳng định nhầm là thiếu; đã đính chính ở `005-pt-training/{plan,tasks}.md`, `008-dashboard-audit/{plan,tasks}.md` (thêm §7.1 liệt kê đủ 34 action), và `constraints/safety.md` SAFE-08.
Phần **thực sự** còn thiếu chỉ là một dòng ADR ghi nhận ngoại lệ DATA-01 → đã hạ xuống **P4 (B-02)**.

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

### B-20 · Ba hàm nghiệp vụ bị **copy nguyên xi** giữa `MembershipService` và `VnPayService`
- **Nguồn**: [feat_flow/membership_billing_feature_analysis.md §8](../../02-SDD-Architecture/feat_flow/membership_billing_feature_analysis.md) (phát hiện 2026-07-23 khi đọc code)
- **Vấn đề**: 3 hàm private **giống hệt nhau từng dòng**, chứa luật **nối hạn (BIZ-01)**:

  | Hàm | `MembershipService.cs` | `VnPayService.cs` |
  |---|---|---|
  | `CancelSiblingPendingAsync` | L558–571 | L244–257 |
  | `SaveActivationAsync` | L573–594 | L259–280 |
  | `ApplyPaidRenewalWindow` | L596–618 | L292–314 |

  Đây đúng loại lỗi mà [`GBL-02`](../../01-SRS-Requirements/constraints/global.md) cấm. `MembershipLifecycle.cs` sinh ra chính vì vấn đề này (gom 3 bản sao lệch nhau ở `MembershipService`/`ProgressService`/`VnPayService`) — nhưng **đợt gom đó bỏ sót 3 hàm này**.
- **Rủi ro**: sửa luật nối hạn ở một file mà quên file kia → **luồng thu tiền thủ công và luồng VNPay kích hoạt gói khác nhau**. Chính là kiểu bug đã từng xảy ra.
- **Việc cần làm**:
  1. Chuyển `ApplyPaidRenewalWindow` (thuần, không I/O) vào `MembershipLifecycle.cs`.
  2. Gom `CancelSiblingPendingAsync` + `SaveActivationAsync` (có chạm `DbContext`) vào một helper dùng chung trong slice `Billing/`.
  3. Thêm test khẳng định hai luồng cho ra **cùng** `EndDate` và **cùng** trạng thái gói cũ.
- **Xong khi**: `grep ApplyPaidRenewalWindow` chỉ ra **một** kết quả.
- **Vì sao P1**: đụng tiền và hạn gói của hội viên; hai đường kích hoạt phải giống hệt nhau.

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

### B-05 · Unit test `PaymentService` + `MembershipLifecycle`
- **Nguồn**: [003 T042, T043](003-membership-billing/tasks.md)
- **Vấn đề**: `PaymentSummaryResponse` gom `byMethod`/`byDay` **theo giờ VN** — chỗ dễ sai nhất về múi giờ, chưa có test. `MembershipLifecycle.ExpireStalePending` (TTL 30 phút, AC-09) là logic thuần, test rất rẻ nhưng cũng chưa có.
- **Việc cần làm**: ~~`PaymentServiceTests.cs` cho summary~~ ✅ **xong 2026-07-23** (PR #10, 25 test gồm gom doanh thu theo ngày VN). **Còn lại:** thêm test `ExpireStalePending` / `ExpireIfPastDue` / `IsActiveOn` vào `MembershipServiceTests.cs`.
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

- [ ] **B-02** · Thêm dòng ADR ghi nhận **ngoại lệ của DATA-01**: giáo án + ghi chú xoá cứng (2 chỗ duy nhất trong hệ thống), lý do ở [`005-pt-training/plan.md`](005-pt-training/plan.md) D-509. Audit **đã có** (`DELETE_WORKOUT_PLAN`, `DELETE_TRAINER_NOTE`) nên chỉ còn thiếu tài liệu · [safety.md SAFE-08](../../01-SRS-Requirements/constraints/safety.md)
- [ ] **B-15** · Bổ sung lý do chọn **HS256 thay vì RS256** vào D-05 trong `docs/06-Management/decision-log.md` (hiện D-05 chỉ ghi "JWT Bearer + BCrypt") · [001 T038](001-auth-rbac/tasks.md)
- [ ] **B-16** · Cân nhắc chuyển `IAuditService` từ `Features/Dashboard/` sang `Infrastructure/` — **chỉ làm khi** xuất hiện nơi tiêu thụ audit thứ hai; hiện 5 slice phải `using Features.Dashboard` chỉ để ghi log · [008 T036](008-dashboard-audit/tasks.md)

## P5 — Chỉ làm khi chuyển VNPay sang live

Không phải nợ của bản sandbox — sandbox đã đúng và đủ.

- [ ] **B-17** · Thêm cột `provider_ref` / `bank_txn_no` nullable vào `payments` (cần team DB) để lưu mã giao dịch VNPay phục vụ đối soát · [010 T036](010-online-payment-vnpay/tasks.md)
- [ ] **B-18** · Thêm `PaymentMethod.Online` để báo cáo `byMethod` tách được tiền online với chuyển khoản tay; hiện dùng chung `Transfer`. **Cảnh báo**: đụng mọi query thống kê của spec 003 · [010 T037](010-online-payment-vnpay/tasks.md)

---

## Thứ tự đề xuất

```text
B-02 (chốt audit — quyết định, không phải code)
  ↓
B-05 (MembershipLifecycle: 7 feature hưởng lợi)  →  B-03  →  B-04
  ↓
B-01 (chờ team DB, khởi động sớm vì có phụ thuộc ngoài)
  ↓
B-06…B-10 (test, làm song song được)
  ↓
B-11…B-14 (đo NFR một đợt)
  ↓
B-15, B-16 · B-17, B-18 (khi live)
```

**Bắt đầu bằng B-02** vì nó là *quyết định* chứ không phải code — chốt xong mới biết có phải viết code hay chỉ viết ADR. **B-01 nên đặt yêu cầu với team DB ngay** dù chưa làm, vì đó là mục duy nhất bị chặn bởi bên ngoài.
