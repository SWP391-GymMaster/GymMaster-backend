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

## UC-02 — Logout
| Field | Content |
|---|---|
| Objective | Người dùng đăng xuất, thu hồi toàn bộ refresh token đang hoạt động của mình. |
| Actors | Admin, Staff, PT, Member |
| Trigger | Người dùng bấm nút đăng xuất trên thanh điều hướng. |
| Pre-condition | Người dùng đang đăng nhập (có access token hợp lệ). |
| Post-condition | Mọi refresh token còn hiệu lực của user bị đánh dấu thu hồi; phiên phía client bị xoá; người dùng về trang login. |

**Main Flow:** 1. Người dùng bấm đăng xuất. 2. Client gọi `POST /api/v1/auth/logout` kèm access token. 3. Hệ thống lấy `userId` từ claim trong token. 4. Truy vấn các refresh token của user còn hiệu lực (`RevokedAt == null` và `ExpiresAt > UtcNow`). 5. Đặt `RevokedAt = UtcNow` cho từng token. 6. Lưu thay đổi và trả về `204 No Content`. 7. Client xoá session khỏi localStorage và chuyển về trang login.
**Alternative Flow:** Gọi API thất bại (mất mạng) → client **vẫn** xoá session local và về trang login; refresh token trên server sẽ hết hạn theo thời gian thay vì bị thu hồi ngay.
**Exception Flow:** Không có/không hợp lệ access token → `401 UNAUTHORIZED` "Token khong hop le."
**Acceptance Criteria:** Sau khi đăng xuất, refresh token cũ không dùng để làm mới phiên được nữa; client không còn session; quay lại trang cần đăng nhập thì bị đẩy về login.

> Nguồn: `Features/Auth/AuthController.cs:95-104`, `Features/Auth/AuthService.cs:528-553`, `../GymMaster-frontend/src/features/auth/session/auth-session.ts:173-186`.

## UC-03 — Manage User Accounts
| Field | Content |
|---|---|
| Objective | Admin tạo, tra cứu, cập nhật, khoá/mở, đặt lại mật khẩu và xoá mềm tài khoản người dùng. |
| Actors | Admin |
| Trigger | Admin mở màn Quản lý tài khoản (`/admin/users`). |
| Pre-condition | Đăng nhập với vai trò Admin (toàn bộ `UsersController` yêu cầu `[Authorize(Roles = Admin)]`). |
| Post-condition | Tài khoản được tạo/cập nhật/khoá/xoá mềm; mọi thay đổi được ghi vào audit log. |

**Main Flow:** 1. Admin mở danh sách tài khoản (`GET /api/v1/users`), lọc theo vai trò/trạng thái. 2. Chọn tạo mới và nhập email, số điện thoại, vai trò, thông tin cá nhân. 3. Hệ thống chuẩn hoá email/phone/role rồi kiểm tra trùng (`Email` và `Phone` trên user chưa bị xoá mềm). 4. Nếu bỏ trống mật khẩu, hệ thống tự sinh mật khẩu tạm. 5. Tạo user với trạng thái Active và gán vai trò đã chọn. 6. Ghi audit log và trả về tài khoản vừa tạo. 7. Admin có thể sửa hồ sơ (`PUT /users/{id}`), khoá/mở (`PATCH /users/{id}/status`), đặt lại mật khẩu (`POST /users/{id}/reset-password`), hoặc xoá mềm (`DELETE /users/{id}` → `204`).
**Alternative Flow:** Xoá tài khoản là **xoá mềm** (`IsDeleted`), không xoá vật lý — dữ liệu lịch sử vẫn giữ và email/phone của tài khoản đã xoá được phép dùng lại.
**Exception Flow:** Email đã dùng → `409 DUPLICATE` "Email nay da duoc su dung." · Số điện thoại đã dùng → `409 DUPLICATE` · Không tìm thấy tài khoản → `404 NOT_FOUND` · Trạng thái ngoài active/locked → `400 VALIDATION_ERROR` · Vai trò ngoài admin/staff/pt/member → `400 VALIDATION_ERROR` · **Đổi vai trò của tài khoản đã tạo → `422 ROLE_TRANSITION_NOT_ALLOWED`** — vai trò gán một lần khi tạo và không đổi được; muốn đổi phải tạo tài khoản mới rồi khoá tài khoản cũ.
**Acceptance Criteria:** Chỉ Admin gọi được; email/phone không trùng; khoá tài khoản thì không đăng nhập được; đổi vai trò bị từ chối; mọi thao tác có audit log.

> Nguồn: `Features/Users/UsersController.cs`, `Features/Users/UserService.cs:24-96,196-285`.

## UC-03A — Manage Staff Accounts
| Field | Content |
|---|---|
| Objective | Admin quản lý riêng nhóm tài khoản Lễ tân (Staff) kèm hồ sơ nhân sự. |
| Actors | Admin |
| Trigger | Admin mở màn Quản lý nhân viên (`/admin/staff`). |
| Pre-condition | Đăng nhập với vai trò Admin. |
| Post-condition | Tài khoản Staff được tạo/cập nhật kèm `StaffProfile`. |

**Main Flow:** 1. Admin mở màn quản lý nhân viên. 2. Client gọi `GET /api/v1/users?role=staff` — **dùng chung API với UC-03**, chỉ khác bộ lọc vai trò. 3. Tạo/sửa tài khoản theo đúng luồng UC-03 với `role = staff`. 4. Khi request có các trường thông tin cá nhân, hệ thống tạo/cập nhật kèm bản ghi `StaffProfile`. 5. Ghi audit log.
**Exception Flow:** Giống UC-03 (trùng email/phone → `409 DUPLICATE`; không tìm thấy → `404 NOT_FOUND`; đổi vai trò → `422 ROLE_TRANSITION_NOT_ALLOWED`).
**Acceptance Criteria:** Danh sách chỉ hiện tài khoản vai trò Staff; tạo Staff thì có `StaffProfile` đi kèm.

> Nguồn: `Features/Users/UsersController.cs` (không có controller riêng cho Staff — cùng `UsersController`, lọc theo `role`), `Features/Users/UserService.cs:285+` (`HasPersonalProfileFields` → `StaffProfile`).

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

## UC-05 — Manage PT Profiles
| Field | Content |
|---|---|
| Objective | Admin tạo, tra cứu và cập nhật hồ sơ huấn luyện viên; PT tự xem hồ sơ của mình. |
| Actors | Admin (quản lý), PT (chỉ xem hồ sơ mình) |
| Trigger | Admin mở màn Quản lý PT (`/admin/trainers`), hoặc PT mở hồ sơ cá nhân. |
| Pre-condition | Đăng nhập. Các thao tác tạo/sửa/liệt kê yêu cầu vai trò Admin. |
| Post-condition | Hồ sơ PT được tạo/cập nhật; audit log ghi lại (`CREATE_TRAINER` / `UPDATE_TRAINER`). |

**Main Flow:** 1. Admin mở danh sách PT (`GET /api/v1/trainers`). 2. Chọn tạo mới, nhập thông tin PT. 3. Hệ thống kiểm tra trùng và tính hợp lệ của dữ liệu cá nhân. 4. Tạo hồ sơ PT, ghi audit log `CREATE_TRAINER`. 5. Admin sửa hồ sơ → ghi audit log `UPDATE_TRAINER`. 6. PT gọi `GET /api/v1/trainers/me` để xem hồ sơ của chính mình.
**Alternative Flow:** Phân quyền đặt ở **từng action** chứ không ở cả controller: `[Authorize]` ở lớp chỉ bắt buộc đăng nhập; `POST`/`GET` danh sách giới hạn Admin, còn `GET /me` giới hạn PT.
**Exception Flow:** Trùng dữ liệu → `409 DUPLICATE` · Không tìm thấy PT → `404 NOT_FOUND` · Dữ liệu không hợp lệ → `400 VALIDATION_ERROR` · Không xác định được người dùng từ token → `401 UNAUTHORIZED`.
**Acceptance Criteria:** Chỉ Admin tạo/sửa được PT; PT chỉ xem được hồ sơ của mình, không xem được của PT khác; audit log ghi đủ.

> Nguồn: `Features/Trainers/TrainersController.cs:9-49`, `Features/Trainers/TrainerService.cs`.

## UC-06 — Manage Membership Packages
| Field | Content |
|---|---|
| Objective | Admin tạo và cập nhật gói tập (giá, thời hạn, có hỗ trợ PT hay không, bật/tắt bán). |
| Actors | Admin (quản lý); mọi vai trò đã đăng nhập đều xem được danh sách gói. |
| Trigger | Admin mở màn Quản lý gói tập (`/admin/packages`). |
| Pre-condition | Đăng nhập; tạo/sửa yêu cầu vai trò Admin. |
| Post-condition | Gói tập được tạo/cập nhật; gói mới mặc định `IsActive = true`. |

**Main Flow:** 1. Admin mở danh sách gói (`GET /api/v1/packages`). 2. Chọn tạo mới, nhập tên, giá, thời hạn và cờ `SupportsPT`. 3. Hệ thống kiểm tra trùng tên gói và tính hợp lệ. 4. Tạo gói với `IsActive = true`. 5. Admin sửa gói (`PUT /api/v1/packages/{id}`) — có thể bật/tắt `IsActive` để ngừng bán, hoặc đổi `SupportsPT`.
**Alternative Flow:** Ngừng bán một gói là **đặt `IsActive = false`**, không xoá — các membership đã bán theo gói đó vẫn giữ nguyên hiệu lực.
**Exception Flow:** Tên gói đã tồn tại → `409 DUPLICATE` · Không tìm thấy gói → `404 NOT_FOUND` · Dữ liệu không hợp lệ (giá/thời hạn) → `400 VALIDATION_ERROR`.
**Acceptance Criteria:** Chỉ Admin tạo/sửa được gói; gói `IsActive = false` không bán được nữa (xem UC-07 `PACKAGE_INACTIVE`); `SupportsPT` quyết định gói có gán PT được hay không.

> Nguồn: `Features/Billing/PackagesController.cs:9-43`, `Features/Billing/MembershipPackageService.cs:20-129`.

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

## UC-11 — View Assigned Members
| Field | Content |
|---|---|
| Objective | PT xem danh sách hội viên đang được phân công cho mình. |
| Actors | PT |
| Trigger | PT mở màn Hội viên của tôi (`/pt/members`). |
| Pre-condition | Đăng nhập với vai trò PT (`PtController` gắn `[Authorize(Roles = Pt)]` ở cả lớp). |
| Post-condition | Danh sách hội viên được phân công cho đúng PT đang đăng nhập được hiển thị. |

**Main Flow:** 1. PT mở màn hội viên của mình. 2. Client gọi `GET /api/v1/pt/members`. 3. Hệ thống lấy `trainerProfile` từ token của PT đang đăng nhập. 4. Truy vấn các assignment còn hiệu lực của PT đó. 5. Trả về danh sách hội viên kèm thông tin cơ bản.
**Alternative Flow:** Phạm vi dữ liệu **suy từ token**, không nhận `trainerId` từ client — nên PT không thể xem danh sách của PT khác dù có sửa request.
**Exception Flow:** Không phải vai trò PT → `403` (chặn ở tầng `[Authorize(Roles = Pt)]`) · Chưa đăng nhập → `401`.
**Acceptance Criteria:** PT chỉ thấy hội viên được phân công cho chính mình; vai trò khác không gọi được endpoint này.

> Nguồn: `Features/Training/PtController.cs:9-23`, `Features/Training/AssignmentService.cs`.

## UC-12 — Create Workout Plan
| Field | Content |
|---|---|
| Objective | PT tạo, cập nhật và xoá giáo án tập cho hội viên được phân công. |
| Actors | PT |
| Trigger | PT mở màn Giáo án (`/pt/workout-planner`) và chọn một hội viên. |
| Pre-condition | Đăng nhập vai trò PT; hội viên **đã được phân công** cho PT này (xem UC-10). |
| Post-condition | Giáo án được lưu kèm danh sách bài tập; audit log ghi `CREATE_WORKOUT_PLAN` / `UPDATE_WORKOUT_PLAN` / `DELETE_WORKOUT_PLAN`. |

**Main Flow:** 1. PT chọn hội viên và bấm tạo giáo án. 2. Nhập tên giáo án và thêm ít nhất một bài tập (danh mục lấy từ `GET /api/v1/exercises`). 3. Client gửi request tạo giáo án. 4. Hệ thống kiểm tra danh sách bài tập không rỗng. 5. Lấy `trainerProfile` từ token và kiểm tra hội viên có được phân công cho PT này không. 6. Phân giải từng bài tập về `Exercise` trong danh mục. 7. Lưu giáo án và ghi audit log `CREATE_WORKOUT_PLAN`. 8. PT có thể sửa (`PUT /api/v1/workout-plans/{id}`) hoặc xoá (`DELETE /api/v1/workout-plans/{id}`).
**Exception Flow:** Giáo án không có bài tập nào → `422 EMPTY_PLAN` "Giao an phai co it nhat 1 bai tap." · **Hội viên chưa được phân công cho PT này → `403 FORBIDDEN` "PT chua duoc phan cong cho hoi vien nay."** · Không tìm thấy giáo án → `404 NOT_FOUND` · Dữ liệu không hợp lệ → `400 VALIDATION_ERROR`.
**Acceptance Criteria:** PT chỉ tạo được giáo án cho hội viên của mình; giáo án rỗng bị từ chối; audit log ghi đủ 3 loại thao tác.

> Nguồn: `Features/Training/WorkoutPlansController.cs:8-33`, `Features/Training/WorkoutPlanService.cs:32-52,122-131`.

## UC-13 — Add Trainer Note
| Field | Content |
|---|---|
| Objective | PT ghi chú về buổi tập / tình trạng của hội viên được phân công. |
| Actors | PT (ghi); Member xem ghi chú về mình. |
| Trigger | PT mở màn Ghi chú (`/pt/trainer-notes`). |
| Pre-condition | Đăng nhập vai trò PT; hội viên đã được phân công cho PT này. |
| Post-condition | Ghi chú được lưu; audit log ghi `CREATE_TRAINER_NOTE` / `UPDATE_TRAINER_NOTE` / `DELETE_TRAINER_NOTE`. |

**Main Flow:** 1. PT chọn hội viên và nhập nội dung ghi chú. 2. Hệ thống lấy `trainerProfile` từ token. 3. Kiểm tra hội viên có được phân công cho PT này không. 4. Lưu ghi chú và ghi audit log `CREATE_TRAINER_NOTE`. 5. PT có thể sửa (`PUT /api/v1/trainer-notes/{id}`) hoặc xoá (`DELETE /api/v1/trainer-notes/{id}`). 6. Hội viên xem các ghi chú về mình ở màn `/member/trainer-notes`.
**Exception Flow:** Hội viên chưa được phân công cho PT này → `403 FORBIDDEN` · Không tìm thấy ghi chú → `404 NOT_FOUND` · Nội dung không hợp lệ → `400 VALIDATION_ERROR` · Không xác định được PT từ token → `401 UNAUTHORIZED`.
**Acceptance Criteria:** PT chỉ ghi chú cho hội viên của mình; hội viên chỉ đọc được ghi chú về bản thân; audit log ghi đủ.

> Nguồn: `Features/Training/TrainerNotesController.cs:8-32`, `Features/Training/TrainerNoteService.cs`.

## UC-14 — View Member 360° Profile
| Field | Content |
|---|---|
| Objective | Xem hồ sơ tổng hợp của một hội viên: thông tin cá nhân, gói hiện tại, lịch sử gói, check-in, PT phụ trách, tiến độ và dinh dưỡng — trên cùng một màn. |
| Actors | Admin, Staff, PT (hội viên của mình), Member (chính mình) |
| Trigger | Mở màn Member 360 từ danh sách hội viên hoặc từ hồ sơ cá nhân. |
| Pre-condition | Đăng nhập; có quyền truy cập hội viên đó theo `CanAccessAsync`. |
| Post-condition | Toàn bộ dữ liệu 360° của hội viên được trả về trong một lần gọi. |

**Main Flow:** 1. Người dùng mở màn Member 360 của một hội viên. 2. Client gọi `GET /api/v1/members/{id}/profile-360`. 3. Hệ thống tìm `MemberProfile` theo `id`. 4. Kiểm tra quyền truy cập theo vai trò: Admin/Staff xem mọi hội viên; Member chỉ xem chính mình (`actorId == profile.UserId`); PT chỉ xem hội viên **đang được phân công active** cho mình. 5. Tổng hợp và trả về: hồ sơ, gói hiện tại, lịch sử gói, lịch sử check-in, PT phụ trách, tiến độ, dinh dưỡng.
**Alternative Flow:** Có một endpoint trùng chức năng ở `MembersController` đã bị **gỡ bỏ có chủ đích** để tránh `AmbiguousMatchException`; bản chính thức (canonical) là `/members/{id}/profile-360` trong `MemberProgressController`.
**Exception Flow:** Không tìm thấy hội viên → `404 NOT_FOUND` "Khong tim thay hoi vien." · Không đủ quyền (Member xem người khác, PT xem hội viên không được phân công) → `403 FORBIDDEN`.
**Acceptance Criteria:** Admin/Staff xem được mọi hội viên; Member chỉ xem được chính mình; PT chỉ xem được hội viên của mình; một lần gọi trả đủ 7 nhóm dữ liệu.

> Nguồn: `Features/Training/MemberProgressController.cs:40-46`, `Features/Training/ProgressService.cs:300-330` (`CanAccessAsync`).

## UC-15 — Track Member Progress
| Field | Content |
|---|---|
| Objective | Ghi nhận và xem diễn biến chỉ số cơ thể của hội viên theo thời gian. |
| Actors | Member (chính mình), PT (hội viên được phân công), Admin/Staff |
| Trigger | Mở màn Tiến độ (`/member/progress` hoặc `/pt/members/{id}/progress`) và nhập chỉ số đo. |
| Pre-condition | Đăng nhập; có quyền truy cập hội viên đó. |
| Post-condition | Bản ghi tiến độ được lưu; audit log ghi `CREATE_PROGRESS` / `UPDATE_PROGRESS`. |

**Main Flow:** 1. Người dùng mở màn tiến độ của hội viên. 2. Nhập các chỉ số: cân nặng, tỉ lệ mỡ, vòng ngực/eo/mông, thời điểm đo và ghi chú. 3. Client gọi `POST /api/v1/members/{id}/progress`. 4. Hệ thống tìm hội viên và kiểm tra quyền (`CanAccessAsync`). 5. Kiểm tra phải có **ít nhất một** chỉ số và mọi chỉ số nằm trong dải cho phép: cân nặng 20–300 kg, tỉ lệ mỡ 0–70 %, vòng ngực/eo/mông 30–200 cm. 6. Kiểm tra thời điểm đo không ở tương lai. 7. Lưu bản ghi và ghi audit log. 8. Xem diễn biến qua `GET /api/v1/members/{id}/progress`.
**Alternative Flow:** Thời điểm đo so với **giờ Việt Nam** (`AppClock.NowVn()`) chứ không phải UTC — nếu so với UTC thì lúc rạng sáng giờ VN, bản ghi của "hôm nay" sẽ bị từ chối oan vì UTC vẫn là hôm trước.
**Exception Flow:** Không tìm thấy hội viên → `404 NOT_FOUND` · Không đủ quyền → `403 FORBIDDEN` "Ban khong co quyen ghi tien do nay." · Không nhập chỉ số nào, hoặc chỉ số ngoài dải → `422 INVALID_MEASUREMENT` "Chi so tien do khong hop le." · Thời điểm đo ở tương lai → `422 INVALID_MEASUREMENT` "Thoi diem do khong duoc o tuong lai." · Ghi chú quá dài → `422 INVALID_MEASUREMENT` "Ghi chu tien do qua dai."
**Acceptance Criteria:** Chỉ số ngoài dải bị từ chối; ngày đo tương lai bị từ chối; Member không ghi được tiến độ của người khác; PT chỉ ghi cho hội viên của mình.

> Nguồn: `Features/Training/MemberProgressController.cs:19-36`, `Features/Training/ProgressService.cs:40-80,300-330`.

## UC-17 — Add Meal Log
| Field | Content |
|---|---|
| Objective | Member ghi lại bữa ăn. |
| Actors | Member |
| Post-condition | MealLog + MealLogItems được lưu. |

**Main Flow:** chọn meal type → search FoodItem / add custom food → nhập quantity → hệ thống tính calories → lưu → cập nhật daily summary.
**Exception Flow:** Food không tồn tại → cho Add Custom Food; quantity ≤ 0 → 422; save failed → lỗi.
**Acceptance Criteria:** Meal log được lưu; tổng calories đúng; xem được lịch sử.

## UC-16 — Set Calorie Target
| Field | Content |
|---|---|
| Objective | Đặt và xem mục tiêu calo/macro hằng ngày cho hội viên. |
| Actors | Member (chính mình), PT (hội viên được phân công), Admin/Staff |
| Trigger | Mở màn Dinh dưỡng và chọn đặt mục tiêu. |
| Pre-condition | Đăng nhập; có quyền truy cập hội viên đó. |
| Post-condition | Mục tiêu calo được lưu kèm ngày hiệu lực; audit log ghi `SET_CALORIE_TARGET`. |

**Main Flow:** 1. Người dùng mở màn dinh dưỡng của hội viên. 2. Nhập calo/ngày, protein, carb, fat và ngày hiệu lực. 3. Client gọi `POST /api/v1/members/{id}/calorie-target`. 4. Hệ thống tìm hội viên và kiểm tra quyền. 5. Kiểm tra `DailyCalories > 0` và protein/carb/fat không âm. 6. Nếu không truyền ngày hiệu lực thì lấy ngày hôm nay. 7. Lưu mục tiêu và ghi audit log. 8. Xem lại qua `GET /api/v1/members/{id}/calorie-target`.
**Exception Flow:** Không tìm thấy hội viên → `404 NOT_FOUND` · Không đủ quyền → `403 FORBIDDEN` "Ban khong co quyen dat muc tieu calo." · Calo ≤ 0 hoặc macro âm → `422 INVALID_TARGET` "Muc tieu calo khong hop le." · Hội viên chưa từng đặt mục tiêu mà đi xem → `404 NO_TARGET` "Hoi vien chua dat muc tieu calo."
**Acceptance Criteria:** Mục tiêu không hợp lệ bị từ chối; Member không đặt được mục tiêu cho người khác; chưa đặt mục tiêu thì trả `NO_TARGET` chứ không trả 0.

> Nguồn: `Features/Nutrition/MemberNutritionController.cs:19-35`, `Features/Nutrition/NutritionService.cs:30-48,103-110`.

## UC-18 — Search Food Item
| Field | Content |
|---|---|
| Objective | Tìm món ăn trong danh mục để thêm vào nhật ký bữa ăn. |
| Actors | Admin, Staff, PT, Member (mọi vai trò đã đăng nhập) |
| Trigger | Người dùng gõ từ khoá vào ô tìm món ở màn Nhật ký bữa ăn. |
| Pre-condition | Đăng nhập. |
| Post-condition | Danh sách món khớp từ khoá được trả về theo trang. |

**Main Flow:** 1. Người dùng gõ từ khoá tìm món. 2. Client gọi `GET /api/v1/food-items?query=...&page=1&pageSize=20`. 3. Hệ thống tìm món khớp từ khoá trong danh mục. 4. Trả về kết quả phân trang kèm thông tin dinh dưỡng của từng món.
**Alternative Flow:** Không truyền `query` → trả về toàn bộ danh mục theo trang (mặc định `page=1`, `pageSize=20`).
**Exception Flow:** Chưa đăng nhập → `401` (chặn ở `[Authorize]` mức lớp).
**Acceptance Criteria:** Tìm theo từ khoá trả đúng món; kết quả có phân trang; mọi vai trò đã đăng nhập đều tìm được.

> Nguồn: `Features/Nutrition/FoodItemsController.cs:8-29`, `Features/Nutrition/FoodItemService.cs`.

## UC-19 — Add Custom Food
| Field | Content |
|---|---|
| Objective | Người dùng thêm món ăn mới vào danh mục khi không tìm thấy món có sẵn. |
| Actors | Member, Admin, Staff (**PT không được thêm**) |
| Trigger | Tìm không thấy món và chọn "Thêm món mới". |
| Pre-condition | Đăng nhập với vai trò Member, Admin hoặc Staff. |
| Post-condition | Món mới có trong danh mục và tìm được ở UC-18; audit log ghi `CREATE_FOOD`. |

**Main Flow:** 1. Người dùng chọn thêm món mới. 2. Nhập tên món và thông tin dinh dưỡng (calo, protein, carb, fat trên đơn vị khẩu phần). 3. Client gọi `POST /api/v1/food-items`. 4. Hệ thống kiểm tra tính hợp lệ của dữ liệu. 5. Lưu món vào danh mục và ghi audit log `CREATE_FOOD`. 6. Món mới lập tức tìm được qua UC-18.
**Alternative Flow:** Quyền đặt ở **action** `[Authorize(Roles = "Member,Admin,Staff")]` chứ không ở lớp — nên PT tìm món được (UC-18) nhưng không thêm món được.
**Exception Flow:** Dữ liệu dinh dưỡng không hợp lệ → `400 VALIDATION_ERROR` · Vai trò PT gọi endpoint này → `403`.
**Acceptance Criteria:** Món vừa thêm tìm được ngay; PT không thêm được món; dữ liệu không hợp lệ bị từ chối.

> Nguồn: `Features/Nutrition/FoodItemsController.cs:32-41`, `Features/Nutrition/FoodItemService.cs`.

## UC-20 — View Daily Calorie Summary
| Field | Content |
|---|---|
| Objective | Xem tổng calo/macro đã nạp trong một ngày và so với mục tiêu. |
| Actors | Member (chính mình), PT (hội viên được phân công), Admin/Staff |
| Trigger | Mở màn Tổng kết calo (`/member/nutrition-summary`). |
| Pre-condition | Đăng nhập; có quyền truy cập hội viên đó. |
| Post-condition | Tổng calo/macro của ngày được hiển thị kèm mức đạt so với mục tiêu. |

**Main Flow:** 1. Người dùng mở màn tổng kết calo. 2. Client gọi `GET /api/v1/members/{id}/calorie-summary?date=...`. 3. Hệ thống tìm hội viên và kiểm tra quyền. 4. Cộng dồn calo/macro từ các bản ghi bữa ăn trong ngày. 5. Đối chiếu với mục tiêu calo đang hiệu lực (UC-16). 6. Trả về tổng đã nạp, mục tiêu và phần chênh lệch.
**Alternative Flow:** Không truyền `date` → mặc định lấy ngày hôm nay.
**Exception Flow:** Không tìm thấy hội viên → `404 NOT_FOUND` · Không đủ quyền → `403 FORBIDDEN` · Hội viên chưa đặt mục tiêu → `404 NO_TARGET`.
**Acceptance Criteria:** Tổng calo khớp với các bữa đã ghi trong ngày; đổi ngày thì số liệu đổi theo; Member không xem được của người khác.

> Nguồn: `Features/Nutrition/MemberNutritionController.cs:38-46`, `Features/Nutrition/NutritionService.cs`.

## UC-21 — View Calorie History
| Field | Content |
|---|---|
| Objective | Xem diễn biến calo đã nạp qua nhiều ngày để đánh giá xu hướng. |
| Actors | Member (chính mình), PT (hội viên được phân công), Admin/Staff |
| Trigger | Mở tab Lịch sử ở màn Dinh dưỡng. |
| Pre-condition | Đăng nhập; có quyền truy cập hội viên đó. |
| Post-condition | Chuỗi calo theo ngày được trả về để vẽ biểu đồ. |

**Main Flow:** 1. Người dùng mở tab lịch sử calo. 2. Client gọi `GET /api/v1/members/{id}/calorie-history`. 3. Hệ thống tìm hội viên và kiểm tra quyền. 4. Tổng hợp calo đã nạp theo từng ngày trong khoảng thời gian. 5. Trả về chuỗi dữ liệu để hiển thị biểu đồ xu hướng.
**Exception Flow:** Không tìm thấy hội viên → `404 NOT_FOUND` · Không đủ quyền → `403 FORBIDDEN`.
**Acceptance Criteria:** Lịch sử khớp với tổng kết từng ngày (UC-20); PT xem được lịch sử của hội viên mình; Member chỉ xem được của mình.

> Nguồn: `Features/Nutrition/MemberNutritionController.cs:49`, `Features/Nutrition/NutritionService.cs`.

## UC-22 — View Revenue & Payment Dashboard
| Field | Content |
|---|---|
| Objective | Admin xem dashboard vận hành. |
| Actors | Admin |
| Post-condition | Dashboard hiển thị số liệu thật. |

**Main Flow:** mở dashboard → lấy dữ liệu payment/membership/check-in → hiển thị doanh thu, payment status, active/expired, check-in stats.
**Acceptance Criteria:** Dữ liệu từ records thật; cập nhật sau workflow; Member/PT không truy cập được (403).

## UC-23 — View Audit Logs
| Field | Content |
|---|---|
| Objective | Admin tra cứu nhật ký thao tác của toàn hệ thống. |
| Actors | Admin |
| Trigger | Admin mở màn Nhật ký (`/admin/audit-logs`). |
| Pre-condition | Đăng nhập vai trò Admin (`[Authorize(Roles = Admin)]` ở mức lớp). |
| Post-condition | Danh sách bản ghi audit được hiển thị theo trang và bộ lọc. |

**Main Flow:** 1. Admin mở màn nhật ký. 2. Client gọi `GET /api/v1/audit-logs` kèm bộ lọc và phân trang. 3. Hệ thống truy vấn bảng audit log. 4. Trả về danh sách: hành động, người thực hiện, đối tượng bị tác động và thời điểm.
**Alternative Flow:** Audit log **chỉ ghi và đọc**, không có endpoint sửa/xoá — để nhật ký không bị can thiệp.
**Exception Flow:** Vai trò khác Admin → `403` · Chưa đăng nhập → `401`.
**Acceptance Criteria:** Chỉ Admin xem được; các thao tác của UC-03/05/06/07/12/13/15/16 đều xuất hiện trong nhật ký với đúng loại hành động.

> Nguồn: `Features/Dashboard/AuditLogsController.cs:8-20`, `Common/` (`IAuditService` được inject vào các service nghiệp vụ).

## UC-27 — Online Payment via VNPay
| Field | Content |
|---|---|
| Objective | Hội viên thanh toán gói tập trực tuyến qua VNPay (sandbox); hệ thống tự kích hoạt membership khi VNPay xác nhận. |
| Actors | Member (thanh toán), Admin/Staff (theo dõi), VNPay (hệ thống ngoài) |
| Trigger | Hội viên chọn thanh toán online ở màn Mua/Gia hạn gói. |
| Pre-condition | Đăng nhập; đã có membership ở trạng thái chờ thanh toán; VNPay đã được cấu hình (`TmnCode`, `HashSecret`, `ReturnUrl`). |
| Post-condition | Thanh toán được ghi nhận và membership chuyển sang Active — **kể cả khi hội viên đóng trình duyệt giữa chừng**. |

**Main Flow:** 1. Hội viên chọn thanh toán online. 2. Client gọi `POST /api/v1/payments/vnpay/create-url`. 3. Hệ thống dựng URL thanh toán có chữ ký và trả về. 4. Hội viên được chuyển sang cổng VNPay và thanh toán. 5. VNPay gọi **IPN** `GET /api/v1/payments/vnpay/ipn` (server-to-server, `[AllowAnonymous]`). 6. Hệ thống xác thực chữ ký và đối chiếu số tiền. 7. Nếu `vnp_ResponseCode == "00"` → ghi nhận thanh toán và kích hoạt membership, trả `{"00", "Confirm Success"}`. 8. Hội viên được VNPay chuyển về `GET /api/v1/payments/vnpay/return` → FE hiển thị kết quả.
**Alternative Flow:** IPN và Return **đều có thể hoàn tất thanh toán** và thao tác này **idempotent** — gọi nhiều lần vẫn an toàn. IPN nhận đơn đã xác nhận rồi thì trả `{"02", "Order already confirmed"}`. Nhờ vậy hội viên đóng trình duyệt sau khi trả tiền thì membership **vẫn** được kích hoạt qua IPN.
**Exception Flow:** Chữ ký sai → `400 INVALID_SIGNATURE` "Chu ky khong hop le." · Số tiền không khớp → `INVALID_AMOUNT` · Membership sai trạng thái → `INVALID_MEMBERSHIP_STATE` · Không hoàn tất được → `PAYMENT_BLOCKED` · Không tìm thấy đơn → `404 NOT_FOUND` · Không có quyền với đơn này → `403 FORBIDDEN` · Chưa cấu hình VNPay → `VNPAY_NOT_CONFIGURED`.
**Acceptance Criteria:** Chữ ký sai bị từ chối; thanh toán thành công thì membership Active; gọi IPN hai lần không tạo thanh toán trùng; đóng trình duyệt sau khi trả tiền vẫn kích hoạt được gói.

> Nguồn: `Features/Billing/VnPayController.cs:7-41`, `Features/Billing/VnPayService.cs:136-202,408`. **`ReturnUrl` phải trỏ về trang FE** `<FE_URL>/member/membership/vnpay-return`, không trỏ về endpoint backend.

## UC-28 — Cancel Membership
| Field | Content |
|---|---|
| Objective | Huỷ đơn đang chờ thanh toán hoặc gói tập đang hoạt động. |
| Actors | Member (gói của mình), Admin/Staff (bất kỳ gói nào) |
| Trigger | Chọn Huỷ ở màn Gói tập của tôi, hoặc Staff/Admin huỷ hộ ở màn Hợp đồng. |
| Pre-condition | Đăng nhập; membership đang ở trạng thái `PendingPayment` hoặc `Active`. |
| Post-condition | Membership chuyển sang trạng thái đã huỷ; audit log ghi lại kèm trạng thái trước đó. |

**Main Flow:** 1. Người dùng chọn huỷ một membership. 2. Hệ thống tìm membership theo `id`. 3. Kiểm tra trạng thái phải là `PendingPayment` hoặc `Active`. 4. Kiểm tra quyền: Admin/Staff huỷ bất kỳ; Member chỉ huỷ được membership của chính mình (`membership.Member.UserId == actorId`). 5. Ghi lại trạng thái trước đó, chuyển sang đã huỷ. 6. Lưu và ghi audit log.
**Alternative Flow:** Đơn `PendingPayment` còn bị **tự động huỷ sau 30 phút** không thanh toán bởi `MembershipLifecycle` (non-UI function, xem mục I.2.4) — không cần ai bấm nút.
**Exception Flow:** Trạng thái không phải PendingPayment/Active → `422 CANNOT_CANCEL` "Chi huy duoc don cho thanh toan hoac goi dang hoat dong." · Member huỷ gói của người khác → `403 FORBIDDEN` "Ban khong co quyen huy membership nay." · Không tìm thấy → `404 NOT_FOUND`.
**Acceptance Criteria:** Không huỷ được gói đã hết hạn/đã huỷ; Member không huỷ được gói người khác; Staff/Admin huỷ được mọi gói; audit log giữ trạng thái trước khi huỷ.

> Nguồn: `Features/Billing/MembershipService.cs:417-431`, `Features/Billing/MembershipLifecycle.cs`.

## UC-29 — Self-service hồ sơ + avatar
| Field | Content |
|---|---|
| Objective | Người dùng tự xem/sửa hồ sơ cá nhân và đổi ảnh đại diện. |
| Actors | Admin, Staff, PT, Member (mọi vai trò, chỉ với hồ sơ của chính mình) |
| Trigger | Người dùng mở màn Hồ sơ của tôi. |
| Pre-condition | Đăng nhập. |
| Post-condition | Hồ sơ và/hoặc avatar được cập nhật cho chính người đang đăng nhập. |

**Main Flow:** 1. Người dùng mở màn hồ sơ cá nhân. 2. Client gọi `GET /api/v1/users/me/profile`. 3. Hệ thống suy ra người dùng **từ token**, trả về hồ sơ. 4. Người dùng sửa thông tin và gửi `PUT /api/v1/users/me/profile`. 5. Đổi ảnh đại diện: `POST /api/v1/users/me/avatar` — ảnh được tải lên **Cloudinary**, hệ thống lưu URL trả về.
**Alternative Flow:** Route là `api/v1/users/me` — **không nhận id từ client**, nên không ai sửa được hồ sơ người khác qua endpoint này. Việc Admin sửa hồ sơ người khác thuộc UC-03 (`/api/v1/users/{id}`).
**Exception Flow:** Chưa đăng nhập → `401` · Dữ liệu không hợp lệ → `400 VALIDATION_ERROR` · Tệp ảnh không hợp lệ → `INVALID_FILE`.
**Acceptance Criteria:** Mọi vai trò đều sửa được hồ sơ của mình; không sửa được hồ sơ người khác; avatar mới hiển thị ngay sau khi tải lên.

> Nguồn: `Features/Account/AccountController.cs:7-43`, `Features/Account/AccountService.cs`, `Infrastructure/` (Cloudinary).

## UC-24 — Barcode Lookup
| Field | Content |
|---|---|
| Objective | Hội viên quét mã vạch trên bao bì để tra cứu món và thêm vào nhật ký bữa ăn. |
| Actors | Member |
| Trigger | Hội viên chọn quét mã vạch ở màn Nhật ký bữa ăn. |
| Pre-condition | — |
| Post-condition | — |

**Main Flow:** **[CAN BO SUNG]** — UC này **Deferred, chưa được implement** (`specs/SECONDARY_BACKLOG.md` SEC-01). Không có code để suy ra luồng, và **không được bịa**: viết luồng cho chức năng không chạy được là tự đưa mình vào thế bí khi thầy yêu cầu demo. Nhóm cần chọn một trong hai: (a) implement rồi viết luồng thật, hoặc (b) giữ nguyên là Deferred có chủ đích và trình bày lý do ưu tiên.
**Exception Flow:** [CAN BO SUNG] — chưa có code.
**Acceptance Criteria:** [CAN BO SUNG] — chưa có code.

> Trạng thái: **Deferred**. Khác với UC-25 (đã gỡ hẳn khỏi phạm vi): UC-24 vẫn nằm trong backlog, chỉ là chưa làm.

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
