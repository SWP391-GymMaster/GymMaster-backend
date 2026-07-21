# Phân công Feature — GymMaster (5 người)

> Sinh tu dong tu `out/inventory.csv`.
> Chay lai: `uv run python .claude/skills/swp391-docs/scripts/gen_phancong.py`
> **Khong sua tay** — sua xong lan regenerate sau se mat. Sua trong script.

Toan du an: **46 man hinh** · **85 endpoint** · **49 function tinh diem** (46 man + 3 non-UI).

Nguyen tac: **lat cat doc (vertical slice)** — moi nguoi nhan tron mot mien nghiep vu
gom *ca frontend lan backend*, de khong ai phai cho nguoi khac lam xong API moi rap duoc UI.

## Tổng quan

| Nhóm | Người | Git | Miền nghiệp vụ | Màn FE | API BE | Tổng |
|---|---|---|---|---:|---:|---:|
| **N1** | **Như** | `BanhMiChao` | Xác thực, Tài khoản cá nhân & Quản trị tài khoản | 12 | 20 | 32 |
| **N2** | **Quang Anh** | `anhdaijka` | Hồ sơ Hội viên & PT | 7 | 20 | 27 |
| **N3** | **Lộc** | `Loc-LX` | Gói tập, Membership & Thanh toán | 8 | 16 | 27 |
| **N4** | **Đam** | `vandam2005` | Tập luyện, Tiến độ & Check-in | 9 | 17 | 26 |
| **N5** | **Minh** | `Minhdicodedao` | Dinh dưỡng, Dashboard & Trang giới thiệu | 10 | 12 | 22 |
| | | | **Tổng** | **46** | **85** | **134** |

---

## N1 — Như (`BanhMiChao`)

### Xác thực, Tài khoản cá nhân & Quản trị tài khoản

**Khối lượng:** 12 màn FE + 20 endpoint BE

**Spec phải đọc:** `specs/001-auth-rbac/spec.md` · `specs/002-member-management/spec.md`

**Code backend:** `backend/GymMaster.API/Features/Auth/` · `backend/GymMaster.API/Features/Account/` · `backend/GymMaster.API/Features/Users/`

**Code frontend:** `src/features/auth/` · `src/features/account/` · `src/features/member-profile/` · `src/app/(auth)/` · `src/features/member-management/ (chi 2 man /admin/users, /admin/staff)`

> Gom `Auth` + `Users` ve mot moi: dang nhap/dang ky va admin tao-khoa-reset tai khoan cung la mien **danh tinh & truy cap**, ai sua phan quyen chi dung mot cho. 5 man profile dung chung API `/users/me/*`.
>
> ⚠️ `/admin/staff` dung chung file `ManagementWorkspace.tsx` voi `/admin/members` va `/admin/trainers` cua **N2** — bao Quang Anh truoc khi sua. Rieng `/admin/users` co component rieng (`AdminUsersTemplateWorkspace.tsx`), sua thoai mai.

#### Màn hình frontend (12)

| Route | Actor |
|---|---|
| `/login` | Anonymous |
| `/signup` | Anonymous |
| `/forgot-password` | Anonymous |
| `/reset-password` | Anonymous |
| `/change-password` | Anonymous |
| `/admin/profile` | Admin |
| `/staff/profile` | Staff |
| `/pt/profile` | PT |
| `/member/profile` | Member |
| `/member/profile/edit` | Member |
| `/admin/users` | Admin |
| `/admin/staff` | Admin |

#### Endpoint backend (20)

| Feature | Method + Route | Quyền |
|---|---|---|
| Auth | `GET /api/v1/auth/me` | Authenticated |
| Auth | `POST /api/v1/auth/change-password` | Authenticated |
| Auth | `POST /api/v1/auth/forgot-password` | Anonymous |
| Auth | `POST /api/v1/auth/google` | Anonymous |
| Auth | `POST /api/v1/auth/login` | Anonymous |
| Auth | `POST /api/v1/auth/logout` | Authenticated |
| Auth | `POST /api/v1/auth/refresh` | Anonymous |
| Auth | `POST /api/v1/auth/register` | Anonymous |
| Auth | `POST /api/v1/auth/reset-password` | Anonymous |
| Account | `GET /api/v1/users/me/profile` | Authenticated |
| Account | `POST /api/v1/users/me/avatar` | Authenticated |
| Account | `PUT /api/v1/users/me` | Authenticated |
| Account | `PUT /api/v1/users/me/profile` | Authenticated |
| Users | `DELETE /api/v1/users/{id:long}` | Admin |
| Users | `GET /api/v1/users` | Admin |
| Users | `GET /api/v1/users/{id:long}` | Admin |
| Users | `PATCH /api/v1/users/{id:long}/status` | Admin |
| Users | `POST /api/v1/users` | Admin |
| Users | `POST /api/v1/users/{id:long}/reset-password` | Admin |
| Users | `PUT /api/v1/users/{id:long}` | Admin |

#### Tài liệu phải nộp

- [ ] **RDS mục II** — mỗi use case 1 bảng 15 dòng + bảng Business Rules
- [ ] **RDS mục III** — bảng field UI + Database Access + câu LINQ thật
- [ ] **SDS mục II** — class diagram · đặc tả method · sequence diagram · DB queries
- [ ] **Project Tracking** — 12 dòng function, cột In Charge ghi `Như`
- [ ] **Issues Report** — cột `Functions/Screens` khớp y hệt tên ở Project Tracking
- [ ] **AI Usage Report** — ghi theo tuần, cột *Validation* và *Risks* là chỗ ăn điểm

---

## N2 — Quang Anh (`anhdaijka`)

### Hồ sơ Hội viên & PT

**Khối lượng:** 7 màn FE + 20 endpoint BE

**Spec phải đọc:** `specs/002-member-management/spec.md` · `specs/006-progress-tracking/spec.md`

**Code backend:** `backend/GymMaster.API/Features/Members/` · `backend/GymMaster.API/Features/Trainers/`

**Code frontend:** `src/features/member-management/` · `src/features/member-360/`

> **Chu so huu `Features/Members/`** — bi 7 feature FE khac goi vao, nhieu nhat he thong. Ai can them/sua endpoint cua Members phai qua N2.

#### Màn hình frontend (7)

| Route | Actor |
|---|---|
| `/admin/members` | Admin |
| `/admin/members/[id]` | Admin |
| `/admin/trainers` | Admin |
| `/staff/members` | Staff |
| `/staff/members/[id]` | Staff |
| `/pt/members` | PT |
| `/pt/members/[id]` | PT |

#### Endpoint backend (20)

| Feature | Method + Route | Quyền |
|---|---|---|
| Members | `DELETE /api/v1/members/{id:long}` | Admin |
| Members | `GET /api/v1/members` | Admin/Staff |
| Members | `GET /api/v1/members/me` | Member |
| Members | `GET /api/v1/members/me/notes` | Member |
| Members | `GET /api/v1/members/me/profile-360` | Member |
| Members | `GET /api/v1/members/me/workout-plans` | Member |
| Members | `GET /api/v1/members/{id:long}` | Authenticated |
| Members | `GET /api/v1/members/{id:long}/360` | Admin/Staff/PT/Member |
| Members | `GET /api/v1/members/{id:long}/notes` | PT/Admin/Member |
| Members | `GET /api/v1/members/{id:long}/workout-plans` | PT/Admin/Member |
| Members | `POST /api/v1/members` | Admin/Staff |
| Members | `POST /api/v1/members/{id:long}/notes` | PT |
| Members | `POST /api/v1/members/{id:long}/workout-plans` | PT |
| Members | `PUT /api/v1/members/me` | Member |
| Members | `PUT /api/v1/members/{id:long}` | Authenticated |
| Trainers | `GET /api/v1/trainers` | Admin |
| Trainers | `GET /api/v1/trainers/me` | PT |
| Trainers | `GET /api/v1/trainers/{id:long}` | Admin |
| Trainers | `POST /api/v1/trainers` | Admin |
| Trainers | `PUT /api/v1/trainers/{id:long}` | Admin |

#### Tài liệu phải nộp

- [ ] **RDS mục II** — mỗi use case 1 bảng 15 dòng + bảng Business Rules
- [ ] **RDS mục III** — bảng field UI + Database Access + câu LINQ thật
- [ ] **SDS mục II** — class diagram · đặc tả method · sequence diagram · DB queries
- [ ] **Project Tracking** — 7 dòng function, cột In Charge ghi `Quang Anh`
- [ ] **Issues Report** — cột `Functions/Screens` khớp y hệt tên ở Project Tracking
- [ ] **AI Usage Report** — ghi theo tuần, cột *Validation* và *Risks* là chỗ ăn điểm

---

## N3 — Lộc (`Loc-LX`)

### Gói tập, Membership & Thanh toán

**Khối lượng:** 8 màn FE + 16 endpoint BE + 3 function non-UI

**Spec phải đọc:** `specs/003-membership-billing/spec.md` · `specs/010-online-payment-vnpay/spec.md`

**Code backend:** `backend/GymMaster.API/Features/Billing/`

**Code frontend:** `src/features/billing/` · `src/features/staff-front-desk/`

> Nhan them **3 function khong co man hinh** — van tinh diem, dung quen ghi vao Project Tracking.

#### Màn hình frontend (8)

| Route | Actor |
|---|---|
| `/admin/packages` | Admin |
| `/admin/memberships` | Admin |
| `/admin/payments` | Admin |
| `/staff/sell-package` | Staff |
| `/staff/renew-package` | Staff |
| `/staff/payments` | Staff |
| `/member/membership` | Member |
| `/member/membership/vnpay-return` | Member |

#### Endpoint backend (16)

| Feature | Method + Route | Quyền |
|---|---|---|
| Billing | `GET /api/v1/members/{memberId:long}/memberships` | Authenticated |
| Billing | `GET /api/v1/members/{memberId:long}/payments` | Authenticated |
| Billing | `GET /api/v1/memberships` | Admin/Staff |
| Billing | `GET /api/v1/packages` | Authenticated |
| Billing | `GET /api/v1/payments` | Admin/Staff |
| Billing | `GET /api/v1/payments/summary` | Admin/Staff |
| Billing | `GET /api/v1/payments/vnpay/ipn` | Anonymous |
| Billing | `GET /api/v1/payments/vnpay/return` | Anonymous |
| Billing | `POST /api/v1/memberships/renewal-request` | Member |
| Billing | `POST /api/v1/memberships/sell` | Admin/Staff |
| Billing | `POST /api/v1/memberships/{id:long}/cancel` | Member/Staff/Admin |
| Billing | `POST /api/v1/memberships/{id:long}/payment` | Admin/Staff |
| Billing | `POST /api/v1/memberships/{id:long}/renew` | Admin/Staff |
| Billing | `POST /api/v1/packages` | Admin |
| Billing | `POST /api/v1/payments/vnpay/create-url` | Authenticated |
| Billing | `PUT /api/v1/packages/{id:long}` | Admin |

#### Function không có màn hình (3)

- VNPay IPN callback (`/payments/vnpay/ipn`)
- Auto-cancel membership sau 30 phut (`MembershipLifecycle`)
- Lazy-expire membership khi truy van (`MembershipLifecycle`)

#### Tài liệu phải nộp

- [ ] **RDS mục II** — mỗi use case 1 bảng 15 dòng + bảng Business Rules
- [ ] **RDS mục III** — bảng field UI + Database Access + câu LINQ thật
- [ ] **SDS mục II** — class diagram · đặc tả method · sequence diagram · DB queries
- [ ] **Project Tracking** — 11 dòng function, cột In Charge ghi `Lộc`
- [ ] **Issues Report** — cột `Functions/Screens` khớp y hệt tên ở Project Tracking
- [ ] **AI Usage Report** — ghi theo tuần, cột *Validation* và *Risks* là chỗ ăn điểm

---

## N4 — Đam (`vandam2005`)

### Tập luyện, Tiến độ & Check-in

**Khối lượng:** 9 màn FE + 17 endpoint BE

**Spec phải đọc:** `specs/004-checkin/spec.md` · `specs/005-pt-training/spec.md` · `specs/006-progress-tracking/spec.md`

**Code backend:** `backend/GymMaster.API/Features/Training/` · `backend/GymMaster.API/Features/CheckIns/`

**Code frontend:** `src/features/pt-training/` · `src/features/pt-assignment/` · `src/features/pt-dashboard/` · `src/features/member-progress-tracking/`

> Man `/pt/members/[id]/*` va `/member/notes|workout` goi endpoint nam trong `Features/Members/` (chu so huu N2) — thong nhat truoc khi sua.

#### Màn hình frontend (9)

| Route | Actor |
|---|---|
| `/admin/assignments` | Admin |
| `/pt/check-in` | PT |
| `/staff/check-in` | Staff |
| `/pt/members/[id]/workout` | PT |
| `/pt/members/[id]/notes` | PT |
| `/pt/members/[id]/progress` | PT |
| `/member/workout` | Member |
| `/member/notes` | Member |
| `/member/progress` | Member |

#### Endpoint backend (17)

| Feature | Method + Route | Quyền |
|---|---|---|
| Training | `DELETE /api/v1/trainer-notes/{id:long}` | PT |
| Training | `DELETE /api/v1/workout-plans/{id:long}` | PT |
| Training | `GET /api/v1/assignments/candidates/members` | Admin |
| Training | `GET /api/v1/assignments/candidates/trainers` | Admin |
| Training | `GET /api/v1/exercises` | Authenticated |
| Training | `GET /api/v1/members/{id:long}/profile-360` | Authenticated |
| Training | `GET /api/v1/members/{id:long}/progress` | Authenticated |
| Training | `GET /api/v1/pt/checkins/today` | PT |
| Training | `GET /api/v1/pt/members` | PT |
| Training | `POST /api/v1/assignments` | Admin |
| Training | `POST /api/v1/members/{id:long}/progress` | Authenticated |
| Training | `POST /api/v1/pt/members/{memberId:long}/checkins` | PT |
| Training | `PUT /api/v1/trainer-notes/{id:long}` | PT |
| Training | `PUT /api/v1/workout-plans/{id:long}` | PT |
| CheckIns | `GET /api/v1/checkins` | Admin/Staff |
| CheckIns | `GET /api/v1/members/{id:long}/checkins` | Authenticated |
| CheckIns | `POST /api/v1/checkins` | Admin/Staff/Member |

#### Tài liệu phải nộp

- [ ] **RDS mục II** — mỗi use case 1 bảng 15 dòng + bảng Business Rules
- [ ] **RDS mục III** — bảng field UI + Database Access + câu LINQ thật
- [ ] **SDS mục II** — class diagram · đặc tả method · sequence diagram · DB queries
- [ ] **Project Tracking** — 9 dòng function, cột In Charge ghi `Đam`
- [ ] **Issues Report** — cột `Functions/Screens` khớp y hệt tên ở Project Tracking
- [ ] **AI Usage Report** — ghi theo tuần, cột *Validation* và *Risks* là chỗ ăn điểm

---

## N5 — Minh (`Minhdicodedao`)

### Dinh dưỡng, Dashboard & Trang giới thiệu

**Khối lượng:** 10 màn FE + 12 endpoint BE

**Spec phải đọc:** `specs/007-nutrition-calorie/spec.md` · `specs/008-dashboard-audit/spec.md` · `specs/009-image-food-recognition/spec.md`

**Code backend:** `backend/GymMaster.API/Features/Nutrition/` · `backend/GymMaster.API/Features/Dashboard/`

**Code frontend:** `src/features/member-nutrition/` · `src/features/admin-dashboard/`

> Endpoint `food-items/online-search` moi co mock MSW, **BE chua implement** — lam not thi tinh them function.
>
> 3 trang tinh (`/`, `/about`, `/welcome`) khong goi API, khong dinh code ai — de o day vi day la nhom nhe nhat sau khi chuyen `Users` sang N1.

#### Màn hình frontend (10)

| Route | Actor |
|---|---|
| `/member/nutrition/meal-journal` | Member |
| `/member/nutrition/summary` | Member |
| `/admin/dashboard` | Admin |
| `/admin/audit-logs` | Admin |
| `/staff/dashboard` | Staff |
| `/pt/dashboard` | PT |
| `/member/dashboard` | Member |
| `/ (landing)` | - |
| `/about` | Anonymous |
| `/welcome` | Anonymous |

#### Endpoint backend (12)

| Feature | Method + Route | Quyền |
|---|---|---|
| Nutrition | `GET /api/v1/food-items` | Authenticated |
| Nutrition | `GET /api/v1/meal-logs` | Authenticated |
| Nutrition | `GET /api/v1/members/{id:long}/calorie-history` | Authenticated |
| Nutrition | `GET /api/v1/members/{id:long}/calorie-summary` | Authenticated |
| Nutrition | `GET /api/v1/members/{id:long}/calorie-target` | Authenticated |
| Nutrition | `POST /api/v1/food-items` | Member/Admin/Staff |
| Nutrition | `POST /api/v1/foods/confirm-ai-food` | Member |
| Nutrition | `POST /api/v1/foods/scan-image` | Member |
| Nutrition | `POST /api/v1/meal-logs` | Authenticated |
| Nutrition | `POST /api/v1/members/{id:long}/calorie-target` | Authenticated |
| Dashboard | `GET /api/v1/audit-logs` | Admin |
| Dashboard | `GET /api/v1/dashboard/summary` | Admin |

#### Tài liệu phải nộp

- [ ] **RDS mục II** — mỗi use case 1 bảng 15 dòng + bảng Business Rules
- [ ] **RDS mục III** — bảng field UI + Database Access + câu LINQ thật
- [ ] **SDS mục II** — class diagram · đặc tả method · sequence diagram · DB queries
- [ ] **Project Tracking** — 10 dòng function, cột In Charge ghi `Minh`
- [ ] **Issues Report** — cột `Functions/Screens` khớp y hệt tên ở Project Tracking
- [ ] **AI Usage Report** — ghi theo tuần, cột *Validation* và *Risks* là chỗ ăn điểm

---

## Luật chung — đọc trước khi code

### 1. Phần dùng chung, không thuộc riêng ai

Sửa mấy chỗ này ảnh hưởng cả 5 người → **báo nhóm trước khi đụng**:

| Thành phần | Đường dẫn |
|---|---|
| Kiểu trả về của mọi service | `backend/GymMaster.API/Common/ServiceResult.cs` |
| Vỏ response API | `backend/GymMaster.API/Common/ApiResponse.cs` |
| Phân trang | `backend/GymMaster.API/Common/PagedResult.cs` |
| Validate thông tin cá nhân | `backend/GymMaster.API/Common/PersonValidation.cs` |
| Entity + DbContext | `backend/GymMaster.API/Entities/` · `Data/GymMasterDbContext.cs` |
| DI, middleware, CORS, JWT | `backend/GymMaster.API/Program.cs` |
| HTTP client | `GymMaster-frontend/src/lib/api/http-client.ts` |
| Session / auth store | `GymMaster-frontend/src/features/auth/session/auth-session.ts` |
| Component dùng chung | `GymMaster-frontend/src/components/ui/` · `components/layout/` |
| Mock MSW | `GymMaster-frontend/src/mocks/handlers/` |

### 2. `Features/Members/` do N2 (Quang Anh) làm chủ

Nó bị **7 feature frontend** khác gọi vào — nhiều nhất hệ thống:
`billing` · `member-360` · `member-management` · `member-nutrition` · `member-profile` ·
`member-progress-tracking` · `pt-training` · `staff-front-desk`.

Ai cần thêm hoặc đổi endpoint của `Members` thì báo N2, đừng tự sửa — đổi một chỗ
là 4 người khác vỡ.

### 3. `ManagementWorkspace.tsx` bị 2 người dùng chung

| Màn | Component | Chủ |
|---|---|---|
| `/admin/users` | `AdminUsersTemplateWorkspace.tsx` | N1 — riêng, sửa thoải mái |
| `/admin/staff` | `ManagementWorkspace.tsx` | **N1** |
| `/admin/members` | `ManagementWorkspace.tsx` | **N2** |
| `/admin/trainers` | `ManagementWorkspace.tsx` | **N2** |

Ba màn cuối chung một file. N1 và N2 báo nhau trước khi sửa `ManagementWorkspace.tsx`.

### 4. Đặt tên function phải khớp nhau

Tên function ghi trong **Project Tracking**, **Issues Report** và **RDS/SDS** phải
giống hệt nhau. Lệch tên là thầy không dò được đóng góp cá nhân.

### 5. Điểm cá nhân

Individual Results = `LOC × Quality` theo function — 60 (đơn giản) / 120 (trung bình) /
240 (phức tạp), Quality 100% / 75% / 50%. Cần **≥720 cả dự án** để đạt tối đa.

Ước lượng độ nặng sau khi chia (**không phải điểm thật** — mức LOC và Quality do thầy
duyệt, bảng này chỉ để so tương đối giữa 5 người):

| Hạng | Nhóm | Người | Ước lượng | Nặng nhất ở đâu |
|---:|---|---|---:|---|
| 1 | N3 | Lộc | ~1740 | 2 wizard bán/gia hạn gói + VNPay + 3 function ngầm |
| 2 | N2 | Quang Anh | ~1440 | 5 màn CRUD/chi tiết hội viên & PT, 20 endpoint |
| 3 | N4 | Đam | ~1380 | Workout plan builder + check-in terminal + assign PT |
| 4 | N1 | Như | ~1320 | `/admin/users` CRUD + 5 form auth |
| 5 | N5 | Minh | ~1260 | Meal journal + admin dashboard |

**Cả 5 người đều vượt ngưỡng ≥720 rất xa** — thấp nhất ~1260, gần gấp đôi. Nên chênh
lệch giữa các nhóm ảnh hưởng cảm giác công bằng nhiều hơn là ảnh hưởng điểm thật.
Lộc đang nặng nhất; muốn cân thêm thì gỡ bớt của Lộc (vd `/admin/payments`,
`/staff/payments`) chứ không phải của người nhẹ nhất.

**Git history là bằng chứng.** Ai nhận nhóm nào thì commit thật vào nhóm đó —
phân công trên giấy mà git không ghi nhận thì không tính được.
