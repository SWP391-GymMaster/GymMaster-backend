# Danh sách Feature — GymMaster

> Sinh tu dong tu `out/inventory.csv`. Chay lai: `uv run python .claude/skills/swp391-docs/scripts/gen_list.py`
> **Khong sua tay** — sua xong lan sau regenerate se mat.

Toan du an: **46 man hinh frontend** · **85 endpoint backend** · **10 feature backend** · **6 nhom route frontend**.

## Tom tat so luong

### Backend — theo feature

| Feature | Endpoint | Spec | Thu muc code |
|---|---:|---|---|
| **Billing** | 16 | `specs/003-membership-billing/` · `specs/010-online-payment-vnpay/` | `Features/Billing/` |
| **Members** | 15 | `specs/002-member-management/` · `specs/006-progress-tracking/` | `Features/Members/` |
| **Training** | 14 | `specs/005-pt-training/` · `specs/006-progress-tracking/` | `Features/Training/` |
| **Nutrition** | 10 | `specs/007-nutrition-calorie/` · `specs/009-image-food-recognition/` | `Features/Nutrition/` |
| **Auth** | 9 | `specs/001-auth-rbac/` | `Features/Auth/` |
| **Users** | 7 | `specs/002-member-management/` | `Features/Users/` |
| **Trainers** | 5 | `specs/002-member-management/` | `Features/Trainers/` |
| **Account** | 4 | `specs/001-auth-rbac/` | `Features/Account/` |
| **CheckIns** | 3 | `specs/004-checkin/` | `Features/CheckIns/` |
| **Dashboard** | 2 | `specs/008-dashboard-audit/` | `Features/Dashboard/` |
| | **85** | | |

### Frontend — theo nhom route

| Nhom route | So man | Actor chinh |
|---|---:|---|
| **Admin (quản trị)** | 12 | Admin |
| **Member (hội viên)** | 10 | Member |
| **PT (huấn luyện viên)** | 8 | PT |
| **Staff (lễ tân)** | 8 | Staff |
| **Auth (đăng nhập / đăng ký)** | 7 | Anonymous |
| **Trang tĩnh** | 1 | - |
| | **46** | |

---

## Endpoint backend (85)

### Account — 4 endpoint

Spec: `specs/001-auth-rbac/spec.md`

| Method + Route | Quyen |
|---|---|
| `GET /api/v1/users/me/profile` | Authenticated |
| `POST /api/v1/users/me/avatar` | Authenticated |
| `PUT /api/v1/users/me` | Authenticated |
| `PUT /api/v1/users/me/profile` | Authenticated |

### Auth — 9 endpoint

Spec: `specs/001-auth-rbac/spec.md`

| Method + Route | Quyen |
|---|---|
| `GET /api/v1/auth/me` | Authenticated |
| `POST /api/v1/auth/change-password` | Authenticated |
| `POST /api/v1/auth/forgot-password` | Anonymous |
| `POST /api/v1/auth/google` | Anonymous |
| `POST /api/v1/auth/login` | Anonymous |
| `POST /api/v1/auth/logout` | Authenticated |
| `POST /api/v1/auth/refresh` | Anonymous |
| `POST /api/v1/auth/register` | Anonymous |
| `POST /api/v1/auth/reset-password` | Anonymous |

### Billing — 16 endpoint

Spec: `specs/003-membership-billing/spec.md` · `specs/010-online-payment-vnpay/spec.md`

| Method + Route | Quyen |
|---|---|
| `GET /api/v1/members/{memberId:long}/memberships` | Authenticated |
| `GET /api/v1/members/{memberId:long}/payments` | Authenticated |
| `GET /api/v1/memberships` | Admin/Staff |
| `GET /api/v1/packages` | Authenticated |
| `GET /api/v1/payments` | Admin/Staff |
| `GET /api/v1/payments/summary` | Admin/Staff |
| `GET /api/v1/payments/vnpay/ipn` | Anonymous |
| `GET /api/v1/payments/vnpay/return` | Anonymous |
| `POST /api/v1/memberships/renewal-request` | Member |
| `POST /api/v1/memberships/sell` | Admin/Staff |
| `POST /api/v1/memberships/{id:long}/cancel` | Member/Staff/Admin |
| `POST /api/v1/memberships/{id:long}/payment` | Admin/Staff |
| `POST /api/v1/memberships/{id:long}/renew` | Admin/Staff |
| `POST /api/v1/packages` | Admin |
| `POST /api/v1/payments/vnpay/create-url` | Authenticated |
| `PUT /api/v1/packages/{id:long}` | Admin |

### CheckIns — 3 endpoint

Spec: `specs/004-checkin/spec.md`

| Method + Route | Quyen |
|---|---|
| `GET /api/v1/checkins` | Admin/Staff |
| `GET /api/v1/members/{id:long}/checkins` | Authenticated |
| `POST /api/v1/checkins` | Admin/Staff/Member |

### Dashboard — 2 endpoint

Spec: `specs/008-dashboard-audit/spec.md`

| Method + Route | Quyen |
|---|---|
| `GET /api/v1/audit-logs` | Admin |
| `GET /api/v1/dashboard/summary` | Admin |

### Members — 15 endpoint

Spec: `specs/002-member-management/spec.md` · `specs/006-progress-tracking/spec.md`

| Method + Route | Quyen |
|---|---|
| `DELETE /api/v1/members/{id:long}` | Admin |
| `GET /api/v1/members` | Admin/Staff |
| `GET /api/v1/members/me` | Member |
| `GET /api/v1/members/me/notes` | Member |
| `GET /api/v1/members/me/profile-360` | Member |
| `GET /api/v1/members/me/workout-plans` | Member |
| `GET /api/v1/members/{id:long}` | Authenticated |
| `GET /api/v1/members/{id:long}/360` | Admin/Staff/PT/Member |
| `GET /api/v1/members/{id:long}/notes` | PT/Admin/Member |
| `GET /api/v1/members/{id:long}/workout-plans` | PT/Admin/Member |
| `POST /api/v1/members` | Admin/Staff |
| `POST /api/v1/members/{id:long}/notes` | PT |
| `POST /api/v1/members/{id:long}/workout-plans` | PT |
| `PUT /api/v1/members/me` | Member |
| `PUT /api/v1/members/{id:long}` | Authenticated |

### Nutrition — 10 endpoint

Spec: `specs/007-nutrition-calorie/spec.md` · `specs/009-image-food-recognition/spec.md`

| Method + Route | Quyen |
|---|---|
| `GET /api/v1/food-items` | Authenticated |
| `GET /api/v1/meal-logs` | Authenticated |
| `GET /api/v1/members/{id:long}/calorie-history` | Authenticated |
| `GET /api/v1/members/{id:long}/calorie-summary` | Authenticated |
| `GET /api/v1/members/{id:long}/calorie-target` | Authenticated |
| `POST /api/v1/food-items` | Member/Admin/Staff |
| `POST /api/v1/foods/confirm-ai-food` | Member |
| `POST /api/v1/foods/scan-image` | Member |
| `POST /api/v1/meal-logs` | Authenticated |
| `POST /api/v1/members/{id:long}/calorie-target` | Authenticated |

### Trainers — 5 endpoint

Spec: `specs/002-member-management/spec.md`

| Method + Route | Quyen |
|---|---|
| `GET /api/v1/trainers` | Admin |
| `GET /api/v1/trainers/me` | PT |
| `GET /api/v1/trainers/{id:long}` | Admin |
| `POST /api/v1/trainers` | Admin |
| `PUT /api/v1/trainers/{id:long}` | Admin |

### Training — 14 endpoint

Spec: `specs/005-pt-training/spec.md` · `specs/006-progress-tracking/spec.md`

| Method + Route | Quyen |
|---|---|
| `DELETE /api/v1/trainer-notes/{id:long}` | PT |
| `DELETE /api/v1/workout-plans/{id:long}` | PT |
| `GET /api/v1/assignments/candidates/members` | Admin |
| `GET /api/v1/assignments/candidates/trainers` | Admin |
| `GET /api/v1/exercises` | Authenticated |
| `GET /api/v1/members/{id:long}/profile-360` | Authenticated |
| `GET /api/v1/members/{id:long}/progress` | Authenticated |
| `GET /api/v1/pt/checkins/today` | PT |
| `GET /api/v1/pt/members` | PT |
| `POST /api/v1/assignments` | Admin |
| `POST /api/v1/members/{id:long}/progress` | Authenticated |
| `POST /api/v1/pt/members/{memberId:long}/checkins` | PT |
| `PUT /api/v1/trainer-notes/{id:long}` | PT |
| `PUT /api/v1/workout-plans/{id:long}` | PT |

### Users — 7 endpoint

Spec: `specs/002-member-management/spec.md`

| Method + Route | Quyen |
|---|---|
| `DELETE /api/v1/users/{id:long}` | Admin |
| `GET /api/v1/users` | Admin |
| `GET /api/v1/users/{id:long}` | Admin |
| `PATCH /api/v1/users/{id:long}/status` | Admin |
| `POST /api/v1/users` | Admin |
| `POST /api/v1/users/{id:long}/reset-password` | Admin |
| `PUT /api/v1/users/{id:long}` | Admin |

---

## Man hinh frontend (46)

### Admin (quản trị) — 12 man

| Route | Actor | File nguon |
|---|---|---|
| `/admin/assignments` | Admin | `src/app/(admin)/admin/assignments/page.tsx` |
| `/admin/audit-logs` | Admin | `src/app/(admin)/admin/audit-logs/page.tsx` |
| `/admin/dashboard` | Admin | `src/app/(admin)/admin/dashboard/page.tsx` |
| `/admin/members` | Admin | `src/app/(admin)/admin/members/page.tsx` |
| `/admin/members/[id]` | Admin | `src/app/(admin)/admin/members/[id]/page.tsx` |
| `/admin/memberships` | Admin | `src/app/(admin)/admin/memberships/page.tsx` |
| `/admin/packages` | Admin | `src/app/(admin)/admin/packages/page.tsx` |
| `/admin/payments` | Admin | `src/app/(admin)/admin/payments/page.tsx` |
| `/admin/profile` | Admin | `src/app/(admin)/admin/profile/page.tsx` |
| `/admin/staff` | Admin | `src/app/(admin)/admin/staff/page.tsx` |
| `/admin/trainers` | Admin | `src/app/(admin)/admin/trainers/page.tsx` |
| `/admin/users` | Admin | `src/app/(admin)/admin/users/page.tsx` |

### Auth (đăng nhập / đăng ký) — 7 man

| Route | Actor | File nguon |
|---|---|---|
| `/about` | Anonymous | `src/app/(auth)/about/page.tsx` |
| `/change-password` | Anonymous | `src/app/(auth)/change-password/page.tsx` |
| `/forgot-password` | Anonymous | `src/app/(auth)/forgot-password/page.tsx` |
| `/login` | Anonymous | `src/app/(auth)/login/page.tsx` |
| `/reset-password` | Anonymous | `src/app/(auth)/reset-password/page.tsx` |
| `/signup` | Anonymous | `src/app/(auth)/signup/page.tsx` |
| `/welcome` | Anonymous | `src/app/(auth)/welcome/page.tsx` |

### Member (hội viên) — 10 man

| Route | Actor | File nguon |
|---|---|---|
| `/member/dashboard` | Member | `src/app/(member)/member/dashboard/page.tsx` |
| `/member/membership` | Member | `src/app/(member)/member/membership/page.tsx` |
| `/member/membership/vnpay-return` | Member | `src/app/(member)/member/membership/vnpay-return/page.tsx` |
| `/member/notes` | Member | `src/app/(member)/member/notes/page.tsx` |
| `/member/nutrition/meal-journal` | Member | `src/app/(member)/member/nutrition/meal-journal/page.tsx` |
| `/member/nutrition/summary` | Member | `src/app/(member)/member/nutrition/summary/page.tsx` |
| `/member/profile` | Member | `src/app/(member)/member/profile/page.tsx` |
| `/member/profile/edit` | Member | `src/app/(member)/member/profile/edit/page.tsx` |
| `/member/progress` | Member | `src/app/(member)/member/progress/page.tsx` |
| `/member/workout` | Member | `src/app/(member)/member/workout/page.tsx` |

### PT (huấn luyện viên) — 8 man

| Route | Actor | File nguon |
|---|---|---|
| `/pt/check-in` | PT | `src/app/(pt)/pt/check-in/page.tsx` |
| `/pt/dashboard` | PT | `src/app/(pt)/pt/dashboard/page.tsx` |
| `/pt/members` | PT | `src/app/(pt)/pt/members/page.tsx` |
| `/pt/members/[id]` | PT | `src/app/(pt)/pt/members/[id]/page.tsx` |
| `/pt/members/[id]/notes` | PT | `src/app/(pt)/pt/members/[id]/notes/page.tsx` |
| `/pt/members/[id]/progress` | PT | `src/app/(pt)/pt/members/[id]/progress/page.tsx` |
| `/pt/members/[id]/workout` | PT | `src/app/(pt)/pt/members/[id]/workout/page.tsx` |
| `/pt/profile` | PT | `src/app/(pt)/pt/profile/page.tsx` |

### Trang tĩnh — 1 man

| Route | Actor | File nguon |
|---|---|---|
| `/ (landing)` | - | `src/app/page.tsx` |

### Staff (lễ tân) — 8 man

| Route | Actor | File nguon |
|---|---|---|
| `/staff/check-in` | Staff | `src/app/(staff)/staff/check-in/page.tsx` |
| `/staff/dashboard` | Staff | `src/app/(staff)/staff/dashboard/page.tsx` |
| `/staff/members` | Staff | `src/app/(staff)/staff/members/page.tsx` |
| `/staff/members/[id]` | Staff | `src/app/(staff)/staff/members/[id]/page.tsx` |
| `/staff/payments` | Staff | `src/app/(staff)/staff/payments/page.tsx` |
| `/staff/profile` | Staff | `src/app/(staff)/staff/profile/page.tsx` |
| `/staff/renew-package` | Staff | `src/app/(staff)/staff/renew-package/page.tsx` |
| `/staff/sell-package` | Staff | `src/app/(staff)/staff/sell-package/page.tsx` |

---

## Anh xa: feature frontend goi feature backend nao

`src/features/` (chia theo **nghiep vu + vai tro**) khong trung ten voi
`Features/` cua backend (chia theo **nghiep vu**). Bang duoi doc thang tu code:
gom moi chuoi `/api/v1/...` trong tung thu muc feature FE roi doi chieu voi
danh sach endpoint that cua BE.

| Feature frontend | Endpoint goi | Cham vao feature backend |
|---|---:|---|
| `account` | 4 | Account (3), Trainers (1) |
| `admin-dashboard` | 2 | Dashboard (2) |
| `auth` | 1 | **1 khong khop** |
| `billing` | 13 | Billing (11), CheckIns (2) |
| `member-360` | 1 | Training (1) |
| `member-management` | 8 | Users (4), Members (2), Trainers (2) |
| `member-nutrition` | 9 | Nutrition (7), **2 khong khop** |
| `member-profile` | 1 | Members (1) |
| `member-progress-tracking` | 1 | Training (1) |
| `pt-assignment` | 3 | Training (3) |
| `pt-dashboard` | 3 | Training (3) |
| `pt-training` | 6 | Members (4), Training (2) |
| `staff-front-desk` | 8 | Billing (5), Members (2), CheckIns (1) |

**Chi tiet 3 duong dan khong khop endpoint BE nao:**

| Feature FE | Duong dan | Ly do |
|---|---|---|
| `auth` | `/api/v1/auth` | Hang base path, khong phai endpoint that — bo qua |
| `member-nutrition` | `/api/v1/food-items/online-search` | **BE chua implement** (chi co mock MSW) |

**Chieu nguoc — feature backend nao bi nhieu ben dung chung** (cang nhieu
cang de dung tay nhau khi chia viec):

| Feature backend | So feature FE dung | Ai dung |
|---|---:|---|
| **Training** | 5 | `member-360`, `member-progress-tracking`, `pt-assignment`, `pt-dashboard`, `pt-training` |
| **Members** | 4 | `member-management`, `member-profile`, `pt-training`, `staff-front-desk` |
| **Trainers** | 2 | `account`, `member-management` |
| **CheckIns** | 2 | `billing`, `staff-front-desk` |
| **Billing** | 2 | `billing`, `staff-front-desk` |
| **Account** | 1 | `account` |
| **Dashboard** | 1 | `admin-dashboard` |
| **Users** | 1 | `member-management` |
| **Nutrition** | 1 | `member-nutrition` |

---

## Phan dung chung — khong thuoc rieng feature nao

Sua may cho nay anh huong toan bo he thong, nen bao nhom truoc khi dung:

| Thanh phan | Duong dan |
|---|---|
| Kieu tra ve cua moi service | `backend/GymMaster.API/Common/ServiceResult.cs` |
| Vo response API | `backend/GymMaster.API/Common/ApiResponse.cs` |
| Phan trang | `backend/GymMaster.API/Common/PagedResult.cs` |
| Validate thong tin ca nhan | `backend/GymMaster.API/Common/PersonValidation.cs` |
| Entity + DbContext | `backend/GymMaster.API/Entities/` · `Data/GymMasterDbContext.cs` |
| DI, middleware, CORS, JWT | `backend/GymMaster.API/Program.cs` |
| HTTP client | `../GymMaster-frontend/src/lib/api/http-client.ts` |
| Session / auth store | `../GymMaster-frontend/src/features/auth/session/auth-session.ts` |
| Component dung chung | `../GymMaster-frontend/src/components/ui/` · `components/layout/` |
| Mock MSW | `../GymMaster-frontend/src/mocks/handlers/` |

---

## Ghi chu

- Con **3 function khong co man hinh** (non-UI), dung quen khi chia: VNPay IPN,
  auto-cancel membership sau 30 phut, lazy-expire membership (`MembershipLifecycle`).
  Tong don vi tinh diem = 46 man + 3 = 49 function.
- Diem ca nhan SWP391 = `LOC x Quality` theo function (60 don gian / 120 trung binh /
  240 phuc tap), can **>=720 ca du an** de dat toi da.
- Ten function khi ghi vao Project Tracking / Issues Report phai khop nhau y het.
