# Implementation Plan: Member Check-in

**Feature Branch**: `004-checkin` | **Spec**: [spec.md](spec.md)
**Status**: `Implemented`
**Input**: `docs/03-Interface-Specs/feature-specs/004-checkin/spec.md`

---

## 1. Summary

Ghi nhận lượt đến phòng tập — bảng dữ liệu đơn giản nhất hệ thống (`check_ins` chỉ 4 cột) nhưng có **chuỗi gác cửa dài nhất**: 5 lớp kiểm tra trước khi được ghi nhận. Toàn bộ độ phức tạp nằm ở tầng validate, không nằm ở mô hình dữ liệu.

Đặc điểm kiến trúc: hai quy tắc siết chặt nhất (bắt buộc có gói còn hạn, giới hạn lượt/ngày) được đưa ra **cấu hình runtime** (`Options/CheckInOptions.cs`) thay vì hardcode, vì phòng gym demo và phòng gym thật vận hành khác nhau — `EnforceMembership` mặc định `false` để demo không bị chặn.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | EF Core 10 (SqlServer) |
| **Storage** | SQL Server — `check_ins`, index `(MemberId, CheckInAt)` |
| **Cấu hình** | `Options/CheckInOptions.cs` — `EnforceMembership` (mặc định `false`), `MaxPerDay` (mặc định `2`), `OncePerDay` |
| **Đồng hồ** | Lưu **UTC**, khung "ngày" reset nửa đêm **giờ VN** (`Common/AppClock.cs`) |
| **Testing** | xUnit — `tests/GymMaster.Api.Tests/CheckInServiceTests.cs` |
| **Target Platform** | Cloud Run + Cloud SQL |
| **Performance Goals** | < 300ms P95, ≤ 3 click; chịu ~50 check-in/phút (NFR-01, NFR-03) |
| **Scale/Scope** | 5 endpoint, 1 bảng |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| GBL-05 — identity từ JWT claim | ✅ PASS | ownership Member/PT kiểm bằng `CurrentUserId` |
| ARCH-02 — wrapper `ApiResponse<T>` | ✅ PASS | mọi action |
| GBL-02 — không lặp business rule | ✅ PASS | dùng lại `Features/Billing/MembershipLifecycle.cs` của spec 003, không tự viết lại "gói còn hạn" |
| GBL-01 — ngày nghiệp vụ theo giờ VN | ✅ PASS | `AppClock` cho khung ngày; lưu UTC |
| GBL-06 — quy tắc vận hành thay đổi được không cần build lại | ✅ PASS | `CheckInOptions` bind từ configuration |
| AUDIT-01 — hành động quan trọng ghi AuditLog | ✅ PASS | `CheckInService` ghi `CREATE_CHECKIN` ở cả 2 đường (quầy và PT check-in hộ) |

## 4. Project Structure

```text
backend/GymMaster.API/
├── Features/CheckIns/
│   ├── CheckInsController.cs       # route "api/v1/checkins" + /members/{id}/checkins
│   ├── ICheckInService.cs · CheckInService.cs
│   └── CheckInDtos.cs              # CheckInResponse { id, memberId, checkInAt, source, memberName? }
├── Features/Training/PtController.cs   # route "api/v1/pt" — /members/{id}/checkins, /checkins/today
├── Entities/CheckIn.cs             # Id, MemberId, CheckInAt (UTC), CreatedBy (nullable)
├── Options/CheckInOptions.cs       # EnforceMembership, MaxPerDay, OncePerDay
└── Common/AppClock.cs

database/
└── 011_fix_check_ins_createdby_column.sql

tests/GymMaster.Api.Tests/CheckInServiceTests.cs
```

**Structure Decision**: endpoint dành cho PT (`/pt/members/{id}/checkins`, `/pt/checkins/today`) nằm ở `Features/Training/PtController.cs` chứ **không** ở slice `CheckIns/`. Lý do: điều kiện gác là "PT có assignment Active với member này" — dữ liệu thuộc spec 005; gom về `PtController` để mọi endpoint PT dùng chung một chỗ kiểm tra phân công, tránh `CheckInService` phải phụ thuộc ngược lên `AssignmentService`.

## 5. Design Decisions

> **Kế thừa ADR dự án**: [D-13](../../../06-Management/decision-log.md) — "chưa trả tiền → không check-in" là hệ quả trực tiếp của trạng thái `PendingPayment`; luật gác lấy từ `MembershipLifecycle` (003/plan.md D-202), **không viết lại**.

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-301 | `EnforceMembership` **mặc định `false`** | Demo/đồ án cần check-in được ngay mà không phải bán gói trước | Môi trường thật phải nhớ bật, nếu quên thì ai cũng vào được |
| D-302 | `MaxPerDay` mặc định **2** (không phải 1) | Thực tế hội viên tập 2 ca sáng/tối trong ngày | Vẫn chặn được spam nhưng không tuyệt đối; `OncePerDay=true` để siết về 1 |
| D-303 | `CreatedBy = null` khi member **tự** check-in | Phân biệt được "tự đến" (source `"member"`) và "quầy làm hộ" (`"front-desk"`) mà không thêm cột | `null` mang ý nghĩa nghiệp vụ — người đọc DB trực tiếp dễ hiểu nhầm là thiếu dữ liệu |
| D-304 | Lưu timestamp **UTC**, ép `Kind=Utc` khi đọc ra | Tránh lệch giờ khi container chạy ở timezone khác | Mọi truy vấn "hôm nay" phải quy đổi sang giờ VN trước khi so sánh |
| D-305 | Khung "ngày" theo **giờ VN**, không theo UTC | Người tập lúc 23:30 và 00:30 phải là 2 ngày khác nhau theo cảm nhận địa phương | Truy vấn theo ngày không dùng thẳng được index trên cột UTC |
| D-306 | Tra cứu bằng **SĐT** thay vì mã thẻ | Không có phần cứng quét thẻ; SĐT là thứ hội viên luôn nhớ | Trùng SĐT giữa 2 tài khoản sẽ gây nhập nhằng — chặn bằng unique index ở spec 002 |
| D-307 | Không ghi AuditLog riêng cho check-in | Bảng `check_ins` **chính là** nhật ký, đã có `CreatedBy` | Nếu sau này cần audit việc *xoá* check-in thì phải bổ sung |
| D-308 | Tái dùng `MembershipLifecycle` của spec 003 | Định nghĩa "gói còn hiệu lực" phải giống hệt nơi bán gói | Slice `CheckIns` phụ thuộc biên dịch vào slice `Billing` |

## 6. Data Flow

```text
POST /api/v1/checkins { memberId? | memberCode?(=SĐT), source? }
  → CheckInsController → CheckInService.CreateAsync
      ┌─ Lớp 1: tìm member theo MemberId hoặc SĐT   → 404 MEMBER_NOT_FOUND
      ├─ Lớp 2: ownership
      │     Member  → profile.UserId phải == CurrentUserId  → 403 FORBIDDEN
      │     PT      → phải có assignment Active với member   → 403 (qua PtController)
      ├─ Lớp 3: trạng thái tài khoản (status locked / LockedUntil) → 403 ACCOUNT_LOCKED
      ├─ Lớp 4: WHERE EnforceMembership == true
      │     ├─ có đơn PendingPayment        → 422 PAYMENT_PENDING
      │     └─ không có Active còn hạn      → 422 NO_ACTIVE_MEMBERSHIP
      │        (dùng MembershipLifecycle.IsActiveOn — spec 003)
      ├─ Lớp 5: đếm check-in trong ngày (giờ VN)
      │        MaxPerDay > 0 và đã đủ lượt  → 409 DAILY_LIMIT_REACHED
      └─ INSERT check_ins { CheckInAt = UtcNow, CreatedBy = null|actorUserId }
  → 201 CheckInResponse { …, source: "member" | "front-desk" }

Đầu ra dùng ở đâu:
  → spec 008 Dashboard: đếm lượt đến hôm nay / theo ngày
  → spec 005 PT: GET /pt/checkins/today — hội viên được phân công đã đến chưa
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-CHK-01 | `Features/CheckIns/CheckInService.cs` — INSERT với `CheckInAt` UTC |
| FR-CHK-02 | `CheckInService` + `Options/CheckInOptions.EnforceMembership` + `MembershipLifecycle.IsActiveOn` |
| FR-CHK-03 | `CheckInService` + `CheckInOptions.MaxPerDay` / `OncePerDay`, đếm theo ngày VN |
| FR-CHK-04 | `CheckInService` — tra theo `MemberId` hoặc `users.Phone` |
| FR-CHK-05 | `CheckInService` — kiểm `users.Status` / `LockedUntil` (cột của spec 001) |
| FR-CHK-06 | `Entities/CheckIn.CreatedBy` nullable + `database/011_fix_check_ins_createdby_column.sql` |
| FR-CHK-07 | `CheckInsController.cs` — ownership Member |
| FR-CHK-08 | `Features/Training/PtController.cs` — kiểm assignment Active (spec 005) |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| Endpoint check-in nằm ở **2 slice** (`CheckIns` + `Training/PtController`) | Nhánh PT cần dữ liệu phân công của spec 005 | Để trong `CheckIns` → `CheckInService` phải phụ thuộc ngược lên `AssignmentService`, tạo vòng phụ thuộc giữa 2 slice |
| Quy tắc nghiệp vụ nằm trong config, không trong code | Phòng gym demo và thật vận hành khác nhau; đổi quy tắc không nên phải build lại | Hardcode → mỗi lần đổi chính sách phải deploy; đưa vào DB → thêm bảng cho 3 giá trị |
| `EnforceMembership` mặc định tắt (D-301) | Ưu tiên demo chạy được ngay | Mặc định bật → mọi thao tác thử nghiệm đều phải bán gói trước, cản trở nghiệm thu |
| `CreatedBy = null` mang nghĩa nghiệp vụ (D-303) | Tránh thêm cột `Source` chỉ để lưu 2 giá trị suy ra được | Thêm cột `Source` → dữ liệu dư thừa, có thể mâu thuẫn với `CreatedBy` |
| Slice `CheckIns` phụ thuộc slice `Billing` | Định nghĩa "gói còn hạn" **bắt buộc** phải giống nơi bán gói | Copy logic sang → chính là lỗi đã từng xảy ra, dẫn tới việc gom `MembershipLifecycle` |
