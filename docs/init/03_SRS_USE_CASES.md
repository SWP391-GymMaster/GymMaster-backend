# 03 — SRS Use Cases

# GymMaster — Use Case Specification

**Status:** Implemented — đồng bộ code 2026-07-15 (4 Roles). Chi tiết API/mã lỗi ở `specs/001-010/`.

---

# 1. Actors

| Actor | Description |
|---|---|
| Admin | Quản lý hệ thống, tài khoản, gói tập, phân công PT, dashboard, audit log. |
| Staff | Hỗ trợ bán/gia hạn gói, check-in, tìm hội viên và vận hành tại quầy. |
| PT | Quản lý hội viên được phân công, giáo án, ghi chú, tiến độ. |
| Member | Hội viên sử dụng hệ thống để check-in, xem tiến độ, meal journal. |
| System | Tự động tính toán, ghi log, cập nhật dashboard. |

> Decision: Hệ thống dùng **4 role chính thức: Admin, Staff, PT, Member**.

---

# 2. Use Case Overview

| UC ID | Use Case | Actor | Priority |
|---|---|---|---|
| UC-01 | Login | All | High |
| UC-02 | Logout | All | High |
| UC-03 | Manage User Accounts | Admin | High |
| UC-03A | Manage Staff Accounts | Admin | High |
| UC-04 | Manage Member Profiles | Admin/Staff | High |
| UC-05 | Manage PT Profiles | Admin | High |
| UC-06 | Manage Membership Packages | Admin | High |
| UC-07 | Sell Membership Package | Admin/Staff | High |
| UC-08 | Renew Membership Package | Admin/Staff/Member | High |
| UC-09 | Check-in | Member/Admin/Staff | High |
| UC-10 | Assign PT to Member | Admin | High |
| UC-11 | View Assigned Members | PT | High |
| UC-12 | Create Workout Plan | PT | High |
| UC-13 | Add Trainer Note | PT | High |
| UC-14 | View Member 360° Profile | Member/PT/Admin | High |
| UC-15 | Track Member Progress | Member/PT | High |
| UC-16 | Set Calorie Target | PT/Member | High |
| UC-17 | Add Meal Log | Member | High |
| UC-18 | Search Food Item | Member | High |
| UC-19 | Add Custom Food | Member | High |
| UC-20 | View Daily Calorie Summary | Member | High |
| UC-21 | View Calorie History | Member/PT | High |
| UC-22 | View Revenue & Payment Dashboard | Admin | High |
| UC-23 | View Audit Logs | Admin | Medium |
| UC-24 | Barcode Lookup | Member | Medium — Deferred (chưa làm) |
| UC-25 | Basic In-app Reminder | System/Member | ~~Removed~~ (đã gỡ khỏi phạm vi) |
| UC-26 | Image Food Recognition Assist (Gemini AI) | Member | Enhancement (đã làm) |
| UC-27 | Online Payment via VNPay (sandbox, IPN auto-activate) | Member/Admin/Staff | High (đã làm — spec 010) |
| UC-28 | Cancel Membership (đơn Pending / gói Active) | Member/Admin/Staff | Medium (đã làm — spec 003) |
| UC-29 | Self-service hồ sơ + avatar (Cloudinary) | All | Medium (đã làm — spec 002) |

> **UC-24 (Barcode Lookup)** vẫn **Deferred** — chưa làm, còn trong `specs/SECONDARY_BACKLOG.md`.
>
> **UC-25 (Basic In-app Reminder) đã bị GỠ khỏi phạm vi dự án** (2026-07-17). Trước đó nó chỉ là vỏ rỗng: không có bảng `notifications` trong DB, không entity, không service — `NotificationsController` trả mảng rỗng cứng để FE không bị 404, và chỉ mock MSW mới tự sinh thông báo lúc demo offline. Hệ thống thật chưa bao giờ tạo thông báo nào, nên nút chuông luôn mở ra panel trống. Đã xoá toàn bộ code liên quan (BE `c61cadc`, FE `54475f0`). SEC-02 trong `SECONDARY_BACKLOG.md` cũng đóng theo.

---

# 3. Detailed Core Use Cases

## UC-01 — Login
| Field | Content |
|---|---|
| Objective | Người dùng đăng nhập vào hệ thống. |
| Actors | Admin, Staff, PT, Member |
| Trigger | Người dùng mở trang login. |
| Pre-condition | Người dùng có tài khoản hợp lệ. |
| Post-condition | Người dùng được chuyển đến dashboard theo vai trò. |

**Main Flow:** 1. Nhập email/username + password. 2. Hệ thống kiểm tra. 3. Xác định role. 4. Tạo token. 5. Redirect theo role.
**Exception Flow:** Sai thông tin → lỗi đăng nhập (chung, chống enumeration). · Tài khoản khóa → từ chối. · Thiếu input → yêu cầu nhập đủ. · Sai >5 lần/15 phút → khóa tạm 15 phút.
**Acceptance Criteria:** User hợp lệ login OK; phân quyền đúng role; user không hợp lệ không vào được.

## UC-04 — Manage Member Profiles
| Field | Content |
|---|---|
| Objective | Quản lý hồ sơ hội viên. |
| Actors | Admin, Staff |
| Pre-condition | Có quyền quản lý hội viên. |
| Post-condition | Hồ sơ được tạo/cập nhật/xem. |

**Main Flow:** mở danh sách → search/filter → tạo/cập nhật → validate → lưu → thông báo.
**Exception Flow:** Email/phone trùng → 409; dữ liệu không hợp lệ → 400; không tồn tại → 404.
**Acceptance Criteria:** Tạo/cập nhật/tìm kiếm OK; email/phone không trùng.

## UC-07 — Sell Membership Package
| Field | Content |
|---|---|
| Objective | Bán gói tập cho hội viên. |
| Actors | Admin, Staff |
| Pre-condition | Hội viên & gói tồn tại. |
| Post-condition | Membership + payment được tạo. |

**Main Flow:** tìm Member → chọn gói → hiển thị giá/thời hạn → xác nhận → tạo Payment → tạo Membership → ghi AuditLog.
**Exception Flow:** Gói inactive → không bán; Member locked → không bán; chưa thanh toán → Membership `PendingPayment`.
**Acceptance Criteria:** Bán gói tạo payment + membership; audit log ghi; dashboard lấy được dữ liệu.

## UC-08 — Renew Membership Package
| Field | Content |
|---|---|
| Objective | Gia hạn gói tập. |
| Actors | Admin, Staff, Member (gửi yêu cầu) |
| Pre-condition | Member có tài khoản + lịch sử membership. |
| Post-condition | Membership gia hạn hoặc tạo mới. |

**Main Flow:** mở membership → hiển thị ngày hết hạn → chọn gói gia hạn → tính thời hạn mới (nối tiếp EndDate cũ) → xác nhận → tạo Renewal/Payment → cập nhật membership.
**Exception Flow:** Payment chưa hoàn tất → không active; gói không tồn tại → lỗi; tài khoản khóa → không cho gia hạn.
**Acceptance Criteria:** Gia hạn cập nhật ngày hết hạn; có payment/renewal record; có audit log nếu Admin/Staff thực hiện.

## UC-09 — Check-in
| Field | Content |
|---|---|
| Objective | Ghi nhận lượt đến phòng tập. |
| Actors | Member, Admin, Staff |
| Pre-condition | Member có tài khoản. |
| Post-condition | Check-in record được tạo. |

**Main Flow:** quét QR/card hoặc tìm Member → xác định Member → kiểm tra membership còn hạn → tạo CheckIn → cập nhật statistics.
**Exception Flow:** Membership expired → cảnh báo gia hạn / từ chối (theo rule); QR/card invalid → từ chối; Member locked → từ chối.
**Acceptance Criteria:** Check-in hợp lệ được lưu; invalid bị từ chối; dashboard thống kê đúng.

## UC-10 — Assign PT to Member
| Field | Content |
|---|---|
| Objective | Admin phân công PT cho hội viên. |
| Actors | Admin |
| Pre-condition | Member & PT tồn tại. |
| Post-condition | TrainerAssignment được tạo. |

**Main Flow:** mở hồ sơ Member → Assign PT → chọn PT → **tự đóng assignment cũ** nếu có (Ended) → tạo assignment mới → ghi AuditLog.
**Exception Flow:** Member/PT not found → 404; Member không có gói `SupportsPT` còn hạn → **409 `PACKAGE_PT_REQUIRED`** (chỉ hội viên gói có PT còn hạn mới được phân công — quyền PT suy động từ gói).
**Acceptance Criteria:** Member có tối đa 1 PT active (tự đóng cái cũ, không báo lỗi trùng); PT thấy Member được phân công; audit log ghi.

## UC-17 — Add Meal Log
| Field | Content |
|---|---|
| Objective | Member ghi lại bữa ăn. |
| Actors | Member |
| Post-condition | MealLog + MealLogItems được lưu. |

**Main Flow:** chọn meal type → search FoodItem / add custom food → nhập quantity → hệ thống tính calories → lưu → cập nhật daily summary.
**Exception Flow:** Food không tồn tại → cho Add Custom Food; quantity ≤ 0 → 422; save failed → lỗi.
**Acceptance Criteria:** Meal log được lưu; tổng calories đúng; xem được lịch sử.

## UC-22 — View Revenue & Payment Dashboard
| Field | Content |
|---|---|
| Objective | Admin xem dashboard vận hành. |
| Actors | Admin |
| Post-condition | Dashboard hiển thị số liệu thật. |

**Main Flow:** mở dashboard → lấy dữ liệu payment/membership/check-in → hiển thị doanh thu, payment status, active/expired, check-in stats.
**Acceptance Criteria:** Dữ liệu từ records thật; cập nhật sau workflow; Member/PT không truy cập được (403).

## UC-26 — Image Food Recognition Assist (Enhancement)
| Field | Content |
|---|---|
| Objective | Member (có gói active) upload ảnh bữa ăn → **Gemini Vision** nhận diện **nhiều món** + ước lượng dinh dưỡng (calo/macro/gram), giúp nhập MealLog nhanh hơn. |
| Actors | Member (có gói tập active) |
| Pre-condition | Meal Journal đã chạy; hội viên có gói tập active; `Gemini:ApiKey` đã cấu hình. |
| Post-condition | Hiển thị danh sách món (Database/AI draft); Member xác nhận từng món AI trước khi lưu FoodItem. |

**Main Flow:** upload ảnh (`POST /foods/scan-image`) → Gemini trả nhiều món + dinh dưỡng/gram → mỗi món: khớp DB → dùng luôn; chưa có → nháp AI (`requiresConfirmation=true`) → Member xác nhận (`POST /foods/confirm-ai-food`) lưu FoodItem `Source="AI"` → dùng để ghi MealLog (spec 007).
**Exception Flow:** Member không có gói active → 403 `MEMBERSHIP_REQUIRED`; ảnh sai định dạng/>5MB → 422 `INVALID_FILE`; Gemini lỗi/timeout → 502, fallback nhập tay.
**Acceptance Criteria:** Không thay thế manual; món AI phải xác nhận trước khi lưu; không tự tạo MealLog từ ảnh; chỉ hội viên có gói dùng được. Chi tiết: spec 009.

---

# 4. Approved Technology Stack
| Layer | Công nghệ |
|---|---|
| Frontend | Next.js (App Router) |
| Backend | C# / ASP.NET Core 10 Web API (.NET 10) |
| Database | SQL Server (Cloud SQL) |
| ORM | Entity Framework Core 10 - Code First |
| Authentication | JWT Bearer (HS256) + BCrypt cost 12; Google ID token |
| Token Policy | Access 15 phút, Refresh 7 ngày (rotate) |
| AI Vision | **Google Gemini Vision** (`gemini-2.5-flash`) — nhận nhiều món + ước lượng dinh dưỡng (đã đổi từ Google Cloud Vision) |
| File Storage (avatar) | **Cloudinary** (đã đổi từ Azure Blob) |
| Online Payment | **VNPay** sandbox (HMAC-SHA512 + IPN) |
| Email | SMTP (Gmail App Password) cho OTP reset |
| Deploy | **Google Cloud Run** (cả FE + BE) + Cloud SQL |

> Đồng bộ code 2026-07-15: stack thực tế đã đổi so với thiết kế gốc (Gemini thay Vision, Cloudinary thay Azure Blob, Cloud Run thay Vercel/Azure). Xem memory GCP deploy.

---

# 5. (Bổ sung theo sách) Use Case Quality Checklist
Trước khi approve mỗi UC, kiểm tra (Ch.16.7 Spec Quality Review):
- [ ] Có đủ Objective/Actor/Pre/Post/Main/Exception/Acceptance.
- [ ] Mỗi Acceptance Criteria **test được** (map ra test case ở `09_TEST_PLAN.md`).
- [ ] Exception Flow phủ ≥1 error case (sách: 40–60% là xử lý lỗi).
- [ ] Không mâu thuẫn UC khác; naming nhất quán glossary.
- [ ] Không còn open question chặn.
