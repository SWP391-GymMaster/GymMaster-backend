# 03 — SRS Use Cases

# GymMaster — Use Case Specification

**Status:** Updated — Approved 4 Roles

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
| UC-24 | Barcode Lookup | Member | Medium |
| UC-25 | Basic In-app Reminder | System/Member | Medium |
| UC-26 | Image Food Recognition Assist | Member | Low / Enhancement |

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

**Main Flow:** mở hồ sơ Member → Assign PT → chọn PT → đóng assignment cũ nếu có → tạo assignment mới → ghi AuditLog.
**Exception Flow:** PT inactive/locked → không cho; Member not found → 404; assign trùng PT active → warning/422.
**Acceptance Criteria:** Member có tối đa 1 PT active; PT thấy Member được phân công; audit log ghi.

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
| Objective | Member upload ảnh bữa ăn → hệ thống gợi ý tên món/nguyên liệu, giúp nhập MealLog nhanh hơn. |
| Actors | Member |
| Pre-condition | Meal Journal đã chạy ổn định. |
| Post-condition | Hiển thị gợi ý; Member xác nhận/chỉnh sửa trước khi lưu. |

**Main Flow:** mở Meal Journal → upload ảnh → gửi dịch vụ nhận diện → trả tên món/nguyên liệu → map FoodItem → Member xác nhận/chỉnh + nhập khẩu phần → tính calories từ FoodItem+quantity → lưu MealLog.
**Exception Flow:** Không nhận diện → nhập thủ công; gợi ý sai → xóa/sửa; service lỗi → fallback manual; không có FoodItem → Add Custom Food.
**Acceptance Criteria:** Không thay thế manual; AI chỉ gợi ý tên/nguyên liệu; calories không tự lưu nếu chưa xác nhận; phải nhập khẩu phần trước khi lưu.

---

# 4. Approved Technology Stack
| Layer | Công nghệ |
|---|---|
| Frontend | Next.js |
| Backend | C# / ASP.NET Core 10 Web API (.NET 10) |
| Database | SQL Server |
| ORM | Entity Framework Core 10 - Code First Migrations |
| Authentication | JWT Bearer Token + BCrypt |
| Token Policy | Access 15 phút, Refresh 7 ngày |
| AI Vision | Google Cloud Vision API |
| File Storage | Azure Blob Storage |
| Deploy | Vercel (FE) + Azure App Service (BE) |

---

# 5. (Bổ sung theo sách) Use Case Quality Checklist
Trước khi approve mỗi UC, kiểm tra (Ch.16.7 Spec Quality Review):
- [ ] Có đủ Objective/Actor/Pre/Post/Main/Exception/Acceptance.
- [ ] Mỗi Acceptance Criteria **test được** (map ra test case ở `09_TEST_PLAN.md`).
- [ ] Exception Flow phủ ≥1 error case (sách: 40–60% là xử lý lỗi).
- [ ] Không mâu thuẫn UC khác; naming nhất quán glossary.
- [ ] Không còn open question chặn.
