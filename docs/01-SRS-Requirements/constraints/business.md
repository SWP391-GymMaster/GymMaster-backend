# Business Constraints — GymMaster

**Phiên bản:** 1.0

> Các **luật nghiệp vụ bất biến** của hệ thống. Khác với ràng buộc kỹ thuật ([global.md](global.md)): vi phạm luật ở đây làm **sai dữ liệu kinh doanh** (sai doanh thu, sai hạn gói, sai quyền), không chỉ xấu code.
>
> Mỗi luật ghi rõ **thi hành ở đâu trong code** — kiểm chứng được, không phải lời hứa suông.

---

## Membership & doanh thu

### BIZ-01 · Tối đa **1 Membership `Active`** trên một hội viên
Bất biến quan trọng nhất hệ thống. Khi kích hoạt gói mới mà member còn gói Active → **nối hạn** (`EndDate mới = EndDate cũ + DurationDays`) và chuyển gói cũ sang `Cancelled`.
📍 `Features/Billing/MembershipService.cs` · spec 003 FR-MS-01/02

### BIZ-02 · Bán gói và thu tiền là **hai bước tách rời**
Bán gói tạo đơn `PendingPayment`; chỉ khi có `Payment` trạng thái `Paid` mới chuyển `Active`. Không có đường tắt nào kích hoạt gói mà không qua bước thanh toán.
📍 `MembershipService` · `VnPayService` · spec 003 D-13, spec 010

### BIZ-03 · Đơn `PendingPayment` quá **30 phút** tự huỷ
Đơn bỏ dở chặn mất BIZ-01 và làm bẩn báo cáo doanh thu.
📍 `MembershipLifecycle.PendingPaymentTtl` · spec 003 FR-MS-07

### BIZ-04 · "Gói đang hiệu lực" = `Status == Active` **AND** `EndDate >= hôm nay (giờ VN)`
Định nghĩa duy nhất, dùng chung cho check-in, phân công PT, tier dinh dưỡng, quét ảnh AI.
📍 `MembershipLifecycle.IsActiveOn` · xem [GBL-02](global.md#gbl-02--một-luật-nghiệp-vụ--một-nguồn-duy-nhất)

### BIZ-05 · Tiền dùng `DECIMAL(12,2)`, **không dùng float**
`vnp_Amount` gửi VNPay là số nguyên (×100), cũng không float.
📍 `Entities/Payment.cs` · `MembershipPackage.cs` · `VnPayService`

### BIZ-06 · Mọi giao dịch phải truy được **người thực hiện**
`CreatedByUserId` lấy từ JWT claim, không nhận từ client. Hành động do hệ thống (VNPay IPN) ghi `null` — có nghĩa là "hệ thống", không phải thiếu dữ liệu.
📍 `Features/Billing/*` · `Features/Dashboard/AuditService.cs`

### BIZ-07 · Số tiền thanh toán online **luôn lấy từ `Package` ở server**
Không bao giờ nhận `amount` từ client. Callback lệch số tiền → từ chối, không kích hoạt.
📍 `VnPayService` · spec 010 FR-VNP-05/08

---

## Quyền và vai trò

### BIZ-08 · **Role bất biến** sau khi tạo tài khoản
Đổi role → 422 `ROLE_TRANSITION_NOT_ALLOWED`. Muốn đổi vai trò: tạo tài khoản mới + khoá tài khoản cũ.
*Vì sao:* đổi role khi đã có dữ liệu gắn kèm (PT đang kèm member, Member đang có gói) để lại dữ liệu mồ côi.
📍 `Features/Users/UserService.cs` · spec 002 FR-USR-04

### BIZ-09 · Tối đa **1 PT `Active`** trên một hội viên
Phân công PT mới **tự đóng** assignment cũ (`Status → Ended`), không báo lỗi.
📍 `Features/Training/AssignmentService.cs` · spec 005 FR-PT-01/02 · ADR **D-12**

### BIZ-10 · Quyền dùng PT **suy ra động từ gói**, không lưu cờ trên member
Được dùng PT ⇔ có Membership thoả BIZ-04 **AND** `Package.SupportsPT = true`. Gói hết hạn là mất quyền ngay, không cần job đồng bộ.
📍 `AssignmentService` + `Entities/MembershipPackage.SupportsPT` · spec 003 FR-PKG-04

---

## Ngưỡng vận hành (cấu hình được — xem [GBL-06](global.md#gbl-06--quy-tắc-vận-hành-đưa-ra-cấu-hình))

### BIZ-11 · Check-in mặc định **2 lượt/ngày** (giờ VN)
`CheckIn:MaxPerDay = 2` (hội viên tập 2 ca sáng/tối). `OncePerDay = true` ⇒ 1; `≤ 0` ⇒ không giới hạn.
📍 `Options/CheckInOptions.cs` · spec 004 FR-CHK-03

### BIZ-12 · Bắt buộc có gói khi check-in là **tuỳ chọn, mặc định TẮT**
`CheckIn:EnforceMembership = false` để demo chạy được ngay. **Môi trường thật phải bật.**
📍 `CheckInOptions` · spec 004 FR-CHK-02

### BIZ-13 · Tier miễn phí: Member **chưa có gói** chỉ tra được **20 món đầu (A→Z)**
Giới hạn tập món **trước** khi lọc từ khoá. Đây là **ràng buộc thương mại**, không phải bảo mật — nên trả kết quả rỗng, không trả 403.
📍 `Features/Nutrition/FoodItemService.cs` · spec 007 FR-FOOD-01

### BIZ-14 · Quét ảnh AI chỉ dành cho hội viên **có gói active**
Mỗi lần quét tốn phí API → gắn với gói trả phí. Không thoả → 403 `MEMBERSHIP_REQUIRED`.
📍 `Features/Nutrition/FoodScanController.cs` · spec 009 FR-IMG-05

---

## Dữ liệu nghiệp vụ

### BIZ-15 · Tiến độ luyện tập: **1 ngày = 1 bản ghi**
Ghi lại cùng ngày thì **đè** bản cũ (200), không tạo điểm mới (201).
*Vì sao:* biểu đồ tiến bộ cần 1 điểm/ngày; cân 3 lần/ngày tạo răng cưa vô nghĩa.
📍 `Features/Training/ProgressService.cs` · spec 006 FR-PROG-01

### BIZ-16 · Số liệu dashboard **phải từ dữ liệu thật**, không mock, không cache
Kỳ không có dữ liệu → trả **0**, không báo lỗi.
📍 `Features/Dashboard/DashboardService.cs` · spec 008 FR-DASH-01/04 · xem [GBL-09](global.md#gbl-09--read-model-tính-lúc-gọi-không-cache)

### BIZ-17 · `currentMembership` không bao giờ là đơn `Cancelled`/`Expired`
Thứ tự suy: `Active` còn hạn (EndDate lớn nhất) → `PendingPayment` mới nhất → **`null`**.
📍 `ProgressService.GetProfile360Async` · spec 006 FR-360-03

---

> **Thêm luật mới vào đây khi nào?** Khi một quyết định ở `plan.md` cấp feature **ảnh hưởng ra ngoài feature đó**. Kèm theo: ghi một dòng ADR ở [`06-Management/decision-log.md`](../../06-Management/decision-log.md).
