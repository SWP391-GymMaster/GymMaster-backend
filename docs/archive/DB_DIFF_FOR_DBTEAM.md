# DB cần sửa — CŨ vs MỚI (cho team DB)

> Chỉ rõ chỗ khác giữa DB hiện tại (`GymMaster_SQLServer_Final.sql`) và schema đúng theo code backend. Làm theo cột **MỚI**. Chi tiết đầy đủ ở `docs/init/15_DATABASE_SCHEMA.md`.

## ⭐ Quy ước chung (áp cho MỌI bảng)
| | CŨ | MỚI |
|---|---|---|
| Tên bảng | PascalCase `Users`, `MemberProfiles` | **snake_case** `users`, `member_profiles` |
| `Id` | `INT` | **`BIGINT`** IDENTITY |
| `RowVersion` | có | **bỏ** |

---

# SPEC 001 — AUTH (khác nhiều nhất)

## 1. `users` (cũ `Users`)
| Điểm | CŨ | MỚI | Cần làm |
|---|---|---|---|
| Tên | `Users` | `users` | đổi thường |
| Id | INT | BIGINT | đổi kiểu |
| **Role** | cột `RoleId` FK | ❌ **BỎ** | dùng bảng `user_roles` (mục 3) |
| Phone | NVARCHAR(20) | NVARCHAR(30) | nới |
| **Status** | TINYINT (1,2,3) | **NVARCHAR(20)** `'active'`/`'locked'` | đổi sang CHỮ |
| Khóa login | `LockoutUntil` | **`LockedUntil`** + thêm **`LoginWindowStartedAt`** | đổi tên + thêm cột |
| RowVersion | có | bỏ | xóa |

Giữ nguyên: Email(UNIQUE), PasswordHash, FullName, FailedLoginCount, LastLoginAt, IsDeleted, CreatedAt, UpdatedAt.

## 2. `roles` (cũ `Roles`)
- `Roles` → `roles`; Id INT → BIGINT; Name giữ (giá trị chữ thường: admin/staff/pt/member).

## 3. `user_roles` — ⚠️ MỚI HOÀN TOÀN (chưa có)
| Cột | Kiểu | Ràng buộc |
|---|---|---|
| UserId | BIGINT | FK → users.Id |
| RoleId | BIGINT | FK → roles.Id |
| | | **PK kép (UserId, RoleId)** |

## 4. `refresh_tokens` (cũ `RefreshTokens`)
- Đổi tên + Id BIGINT. Cột giữ: UserId, TokenHash, ExpiresAt, RevokedAt, CreatedAt.

## 5. `password_reset_tokens` — ⚠️ MỚI HOÀN TOÀN (chưa có)
| Cột | Kiểu |
|---|---|
| Id | BIGINT PK |
| UserId | BIGINT FK → users.Id |
| TokenHash | NVARCHAR(255) |
| ExpiresAt | DATETIME2 (30 phút) |
| UsedAt | DATETIME2 NULL |
| CreatedAt | DATETIME2 |

---

# SPEC 002 — USER/MEMBER/PT (chỉ đổi tên + kiểu)
- `MemberProfiles` → **`member_profiles`** (Id BIGINT, UserId UNIQUE). Cột: DateOfBirth, Gender, Address, EmergencyContact, JoinedAt, IsDeleted, CreatedAt, UpdatedAt.
- `TrainerProfiles` → **`trainer_profiles`** (Id BIGINT, UserId UNIQUE). Cột: Specialty, Bio, **Gender, DateOfBirth, YearsOfExperience**, IsDeleted, CreatedAt, UpdatedAt.
- `AuditLogs` → **`audit_logs`** (Id BIGINT, UserId BIGINT **NULL**, Action, Entity, EntityId, Metadata NVARCHAR(MAX), CreatedAt).

---

# Tóm tắt phải nhớ
1. Mọi tên bảng → **snake_case**.
2. **Id = BIGINT** hết.
3. Bỏ `RoleId` trên users → tạo **`user_roles`**.
4. Tạo **`password_reset_tokens`**.
5. `users.Status` = **chữ** (`active`/`locked`).
6. `LockoutUntil` → `LockedUntil` + thêm `LoginWindowStartedAt`.
7. Bỏ `RowVersion`.

> Bảng nghiệp vụ khác (membership/payment/checkin/workout/nutrition) thuộc spec 003–008, CHƯA code — chưa cần khớp gấp.
