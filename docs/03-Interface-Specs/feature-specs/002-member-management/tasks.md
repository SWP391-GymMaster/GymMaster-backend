---
description: "Task list — User, Staff, PT & Member Management"
---

# Tasks: User, Staff, PT & Member Management

**Feature**: `002-member-management`
**Input**: [spec.md](spec.md) · [plan.md](plan.md)
**Trạng thái tổng**: 33/35 hoàn thành

> Bảng công việc **as-built**: `[X]` = đã có trong code (đường dẫn kiểm chứng được), `[ ]` = còn nợ, phát hiện khi đối chiếu spec ↔ code.

**Ký hiệu**: `[P]` = làm song song được · `[US*]` = thuộc user story nào

---

## Phase 1: Setup

- [X] T001 Thêm package `CloudinaryDotNet` 1.29.2 vào `backend/GymMaster.API/GymMaster.API.csproj`
- [X] T002 [P] `Options/CloudinaryOptions.cs` + binding trong `Program.cs`

## Phase 2: Foundational (chặn mọi user story)

- [X] T003 Entity `Entities/MemberProfile.cs`, `StaffProfile.cs`, `TrainerProfile.cs` (1-1 với `users`, có `IsDeleted`)
- [X] T004 Cấu hình quan hệ + unique index `UserId` trong `Data/GymMasterDbContext.cs`
- [X] T005 [P] `Common/PersonValidation.cs` — validate dob / gender / phone / emergencyContact dùng chung 4 slice
- [X] T006 [P] `Common/PagedResult.cs` — `{ items, page, pageSize, total }`, mặc định 20, tối đa 100
- [X] T007 Migration `database/012_users_avatarurl.sql` — thêm cột `users.AvatarUrl`
- [X] T008 Migration `database/013_staff_profiles_trainer_contact_filtered_unique.sql`
- [X] T009 Đăng ký DI `IUserService`, `IMemberService`, `ITrainerService`, `IAccountService`, `IAvatarStorage` trong `Program.cs`

**Checkpoint**: có bảng profile + validate + phân trang → các user story bắt đầu được.

---

## Phase 3: US1 — Admin quản trị tài khoản (P1) 🎯 MVP

**Goal**: Admin tạo/sửa/khoá/xoá tài khoản cho mọi role.
**Independent Test**: `POST /users` tạo Staff → `GET /users?query=` tìm thấy → `PATCH /users/{id}/status` khoá → `DELETE` rồi query lại không thấy nhưng DB vẫn còn bản ghi.

- [X] T010 [US1] `Features/Users/UserDtos.cs` — `CreateUserRequest`, `AdminUserResponse`, `UpdateUserRequest`
- [X] T011 [US1] `Features/Users/UserService.cs` — `CreateAsync`: tạo user + gán role, sinh mật khẩu tạm khi không nhập → **FR-USR-01**
- [X] T012 [US1] Nhánh role admin/staff có thông tin cá nhân → INSERT `staff_profiles` → **FR-USR-01**
- [X] T013 [US1] Chặn trùng email/phone (unique filter `IsDeleted=0`) → 409 `DUPLICATE` → **FR-USR-02**
- [X] T014 [US1] `SearchAsync` — lọc theo `query` / `role`, phân trang
- [X] T015 [US1] `UpdateAsync` — chặn đổi role, trả 422 `ROLE_TRANSITION_NOT_ALLOWED` → **FR-USR-04**
- [X] T016 [US1] `UpdateStatusAsync` (active/locked) + `ResetPasswordAsync` trả `temporaryPassword` → **FR-USR-05**
- [X] T017 [US1] Soft-delete `DELETE /users/{id}` → 204, set `IsDeleted=1` → **DATA-01**
- [X] T018 [US1] `Features/Users/UsersController.cs` — `[Authorize(Roles="admin")]` toàn controller → **FR-USR-03**
- [X] T019 [US1] Ghi AuditLog `CREATE_USER` / `UPDATE_USER` / `DELETE_USER` qua `IAuditService`
- [X] T020 [US1] Unit test `tests/GymMaster.Api.Tests/UserServiceTests.cs`

**Checkpoint**: Admin vận hành được toàn bộ tài khoản.

---

## Phase 4: US2 — Quản lý hồ sơ hội viên (P1)

**Goal**: Admin/Staff tạo và tra cứu hồ sơ hội viên tại quầy.
**Independent Test**: `POST /members` với email mới → 201; lặp lại với email của Member đã có tài khoản nhưng chưa có hồ sơ → `linkedToExistingAccount=true`; tìm theo một phần SĐT → kết quả phân trang.

- [X] T021 [US2] `Features/Members/MemberDtos.cs` — `MemberResponse` (kèm `memberCode` suy ra `MEM-{Id:D6}`), `CreateMemberResponse`
- [X] T022 [US2] `Features/Members/MemberService.cs` — `CreateAsync` xử lý **3 nhánh email**: chưa tồn tại / là Member chưa có hồ sơ / đã có hồ sơ hoặc role khác → **FR-MEM-01**
- [X] T023 [US2] `SearchAsync` theo tên/email/SĐT, `PagedResult`, mặc định 20/trang → **FR-MEM-02**
- [X] T024 [US2] `UpdateAsync` — chạy `PersonValidation`, cập nhật `UpdatedAt` → **FR-MEM-03**
- [X] T025 [US2] Soft-delete hồ sơ hội viên → **FR-MEM-04**
- [X] T026 [US2] Kiểm tra ownership trong `MembersController.cs`: Member chỉ xem/sửa hồ sơ của mình → 403 → **FR-MEM-05**
- [X] T027 [US2] `GET/PUT /members/me` — **tự tạo MemberProfile** nếu chưa có → **FR-MEM-06**
- [X] T028 [US2] Ghi AuditLog `CREATE_MEMBER` / `UPDATE_MEMBER` / `DELETE_MEMBER`

---

## Phase 5: US3 — Quản lý hồ sơ PT (P2)

**Goal**: Admin tạo PT kèm chuyên môn trong một bước.
**Independent Test**: `POST /trainers` → có tài khoản role PT + `trainer_profiles` + `initialPassword`; PT gọi `GET /trainers/me` thấy hồ sơ của mình.

- [X] T029 [US3] `Features/Trainers/TrainerDtos.cs` — `CreateTrainerResponse`, `TrainerResponse`
- [X] T030 [US3] `Features/Trainers/TrainerService.cs` — `CreateAsync` tạo **user role PT + TrainerProfile trong một transaction** → **FR-PT-PROF-01**
- [X] T031 [US3] `GET /trainers` (Admin, phân trang) · `GET /trainers/me` (PT) · `GET/PUT /trainers/{id}` (Admin)
- [X] T032 [US3] Unit test `tests/GymMaster.Api.Tests/TrainerServiceTests.cs`

---

## Phase 6: US4 — Self-service hồ sơ cá nhân & ảnh đại diện (P2)

**Goal**: mọi người tự sửa hồ sơ và đổi ảnh đại diện của chính mình.
**Independent Test**: `PUT /users/me` đổi tên → `PUT /users/me/profile` đổi dob (Admin/Staff→staff_profiles, PT→trainer_profiles, Member→422) → `POST /users/me/avatar` upload jpeg 2MB thấy `avatarUrl` đổi.

- [X] T033 [US4] `Features/Account/AccountController.cs` route `api/v1/users/me` + `AccountDtos.cs` (`PersonalProfileResponse`)
- [X] T034 [US4] `AccountService.cs` — `PUT /users/me` đổi `fullName`/`phone` (chặn trùng phone → 409) → **FR-ACC-01**
- [X] T035 [US4] `GET/PUT /users/me/profile` rẽ nhánh theo role; Member → 422 `NOT_SUPPORTED` (điều hướng `/members/me`) → **FR-ACC-01**
- [X] T036 [US4] `Infrastructure/IAvatarStorage.cs` + `CloudinaryAvatarStorage.cs` — validate jpeg/png/webp ≤5MB → **FR-ACC-02**
- [X] T037 [US4] `Infrastructure/AvatarStorageException.cs` → map 502; chưa cấu hình → 500 `CLOUDINARY_NOT_CONFIGURED`
- [X] T038 [US4] Ghi AuditLog `UPDATE_AVATAR`
- [X] T039 [US4] Unit test `tests/GymMaster.Api.Tests/AccountServiceTests.cs`

---

## Phase 7: Polish & Cross-cutting

- [X] T040 [P] Đảm bảo PII (email/SĐT) không xuất hiện trong log → **NFR-03**
- [X] T042 Unit test `tests/GymMaster.Api.Tests/MemberServiceTests.cs` — 14 test phủ đủ 3 nhánh email của `CreateAsync` + tự tạo hồ sơ ở `/members/me` + soft delete
- [ ] T043 **Còn nợ** — đo lại NFR-01 (tìm kiếm < 1s với 1000 hội viên) bằng `tests/blackbox/Performance.Tests.ps1` và ghi số đo vào `docs/04-Test-Specs/test-plan.md`

---

## Dependencies & Execution Order

- **Phase 2** phụ thuộc **spec 001 Phase 2** (bảng `users`/`roles` + `ApiControllerBase`).
- **US1** trước **US3**: `TrainerService.CreateAsync` tái dùng logic tạo user + sinh mật khẩu tạm của `UserService`.
- **US2** độc lập với US1 → làm song song được.
- **US4** cần US1/US2/US3 đã có bảng profile tương ứng.

```text
[spec 001] → Setup → Foundational ─┬→ US1 → US3
                                   ├→ US2        ─→ US4 → Polish
```

## Truy vết Acceptance Criteria

| AC (spec.md) | Task | Kiểm chứng bằng |
|---|---|---|
| AC-01, AC-02 | T011, T013 | `UserServiceTests.cs` |
| AC-03 | T023 | `tests/blackbox/Performance.Tests.ps1` (chưa có số đo — T043) |
| AC-04 | T026 | black-box (Member gọi `/members/{id}` của người khác → 403) |
| AC-05 | T017, T025 | `UserServiceTests.cs` |
| AC-06 | T028 | black-box (kiểm tra bảng `audit_logs`) |
| AC-07 | T015 | `UserServiceTests.cs` |
| AC-08 | T027 | black-box (chưa có unit test — T042) |
| AC-09 | T036 | thủ công (cần Cloudinary thật) |
| AC-10 | T030 | `TrainerServiceTests.cs` |
