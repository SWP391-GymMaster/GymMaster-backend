# Implementation Plan: Meal Journal & Daily Calorie Summary

**Feature Branch**: `007-nutrition-calorie` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)
**Status**: `Implemented`
**Input**: `docs/03-Interface-Specs/feature-specs/007-nutrition-calorie/spec.md`

---

## 1. Summary

Nhật ký ăn uống nhập tay (ADR-04) + tính calo/macro theo ngày so với mục tiêu. Ba đặc điểm kiến trúc chi phối feature:

1. **Snapshot calo tại thời điểm ghi** — `meal_log_items.Calories` lưu giá trị đã nhân, không tính lại từ `food_items`. Admin sửa món về sau không làm thay đổi lịch sử. (Macro thì chưa snapshot — nợ kỹ thuật đã biết, xem D-706.)
2. **Tier miễn phí** — Member chưa có gói active chỉ tra được **20 món đầu A→Z**. Đây là ràng buộc thương mại được cài vào tầng truy vấn, không phải tầng quyền.
3. **Tìm món không phân biệt dấu** — dựa vào collation `Latin1_General_100_CI_AI` của SQL Server, không tự viết hàm bỏ dấu trong C#.

## 2. Technical Context

| Hạng mục | Giá trị thực tế |
|---|---|
| **Language/Version** | C# 13 / .NET 10 |
| **Primary Dependencies** | EF Core 10 (SqlServer) |
| **Storage** | SQL Server — `food_items`, `meal_logs`, `meal_log_items`, `calorie_targets` |
| **Collation** | `Latin1_General_100_CI_AI` cho `food_items.Name` — tìm không phân biệt **dấu + hoa thường** |
| **Kiểu số** | `DECIMAL(8,2)` cho calo / macro / khẩu phần |
| **Đồng hồ** | Ngày theo **giờ VN** (`Common/AppClock.cs`) |
| **Testing** | xUnit — `NutritionServiceTests.cs`, `FoodItemServiceTests.cs` |
| **Target Platform** | Cloud Run + Cloud SQL |
| **Performance Goals** | Cập nhật summary < 500ms (NFR-01) |
| **Constraints** | Food search phân trang 20/trang (tối đa 100); tier free = 20 món |
| **Scale/Scope** | 8 endpoint, 3 controller, 4 bảng |

## 3. Constitution Check

> **Nguồn của các ID:** `SEC-*` `ARCH-*` `DATA-*` `AUDIT-*` = [`CONSTITUTION.md`](../../../../CONSTITUTION.md) (luật gốc) · `GBL-*` = [constraints/global.md](../../../01-SRS-Requirements/constraints/global.md) · `BIZ-*` = [constraints/business.md](../../../01-SRS-Requirements/constraints/business.md) · `SAFE-*` = [constraints/safety.md](../../../01-SRS-Requirements/constraints/safety.md).

| Điều luật | Trạng thái | Bằng chứng |
|---|---|---|
| GBL-05 — identity từ JWT claim | ✅ PASS | ownership kiểm bằng `CurrentUserId` |
| GBL-02 — không lặp business rule | ✅ PASS | dùng lại `MembershipLifecycle` (003) cho tier free, cửa quyền PT (005) |
| GBL-04 — kiểm quyền ở Service layer | ✅ PASS | `NutritionService` |
| GBL-01 — ngày nghiệp vụ theo giờ VN | ✅ PASS | `AppClock` cho `LogDate` và `EffectiveDate` |
| SAFE-03 — dữ liệu lịch sử phải snapshot | ⚠️ PARTIAL | **calo** đã snapshot; **macro** vẫn đọc live từ `food_items` — xem [Complexity Tracking](#8-complexity-tracking) |
| ARCH-02 — wrapper `ApiResponse<T>` / `PagedResult<T>` | ✅ PASS | mọi action |

## 4. Project Structure

```text
backend/GymMaster.API/Features/Nutrition/
├── FoodItemsController.cs        # route "api/v1/food-items" — tìm + thêm món
├── IFoodItemService.cs · FoodItemService.cs
├── MealLogsController.cs         # route "api/v1/meal-logs" — ghi/đọc bữa ăn
├── MemberNutritionController.cs  # route "api/v1/members" — calorie-target/summary/history
├── INutritionService.cs · NutritionService.cs   # ★ tính summary + macro
├── NutritionDtos.cs              # FoodItemResponse, MealLogResponse, CalorieSummaryResponse…
└── (FoodScan*.cs — thuộc spec 009)

backend/GymMaster.API/Entities/
├── FoodItem.cs                   # Name UNIQUE, CaloriesPerUnit, ServingSize (mặc định 100), Source {Admin, AI}
├── MealLog.cs                    # index (MemberId, LogDate)
├── MealLogItem.cs                # Quantity > 0, Calories **snapshot**
├── CalorieTarget.cs              # UNIQUE(MemberId, EffectiveDate)
└── NutritionEnums.cs             # MealType {1 Breakfast, 2 Lunch, 3 Dinner, 4 Snack}

tests/GymMaster.Api.Tests/
├── NutritionServiceTests.cs
└── FoodItemServiceTests.cs
```

**Structure Decision**: `MemberNutritionController` tách riêng khỏi `MealLogsController` vì hai gốc route khác nhau (`/members/{id}/…` và `/meal-logs`), nhưng cùng gọi `NutritionService` — công thức calo chỉ tồn tại một nơi, để spec 006 (360°) và spec 009 (quét ảnh) tái sử dụng được.

## 5. Design Decisions

> **Chi tiết hoá ADR dự án**: [D-17](../../../06-Management/decision-log.md) (SQL Server → collation `CI_AI` ở D-704).
> **Chưa có ADR dự án tương ứng, đáng cân nhắc nâng lên**: **D-705** (tier free 20 món — đây là **ràng buộc thương mại**, không phải quyết định kỹ thuật, nên đáng được chốt ở cấp dự án).
> **Nợ đã ghi nhận**: D-706 (macro chưa snapshot) → việc **B-01** trong [BACKLOG](../BACKLOG.md), là mục ưu tiên cao nhất toàn dự án.

| ID | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| D-701 | **Snapshot** `Calories = CaloriesPerUnit × Quantity` vào `meal_log_items` | Admin sửa calo của món không được phép làm sai lệch nhật ký đã ghi | Dữ liệu dư thừa; sửa sai số liệu món cũ không "chữa" được log cũ |
| D-702 | Nhập tay thay vì tích hợp food API bên thứ ba (ADR-04) | Không phụ thuộc dịch vụ ngoài, không tốn phí, dữ liệu tiếng Việt sát thực tế | Kho món phải tự bồi đắp; chất lượng dữ liệu do người dùng quyết định |
| D-703 | `POST /food-items` là **find-or-create** theo tên: trùng → 200 món cũ, mới → 201 | Người dùng thêm món tự do mà kho không sinh bản trùng | Client phải phân biệt 200/201; tên gõ sai chính tả vẫn tạo món mới |
| D-704 | Tìm món dựa vào **collation `CI_AI`** của SQL Server | Gõ "pho bo" ra "Phở bò" mà không cần hàm bỏ dấu trong C# | Ràng buộc vào SQL Server; đổi DBMS phải viết lại |
| D-705 | Tier free = **20 món đầu A→Z**, giới hạn *universe* **trước** khi lọc từ khoá | Cho dùng thử mà vẫn tạo động lực mua gói | Người chưa mua gói gõ đúng tên món vẫn không thấy → dễ hiểu nhầm là lỗi |
| D-706 | Macro (protein/carb/fat) đọc **live** từ `food_items`, chưa snapshot | Bảng `meal_log_items` do team DB quản lý, thêm cột cần vòng migration riêng | Sửa macro của món **làm đổi số liệu macro lịch sử** — nợ kỹ thuật đã ghi nhận |
| D-707 | Thêm lại cùng món trong cùng bữa/ngày → **cộng dồn** khẩu phần | Người dùng ăn thêm phần nữa là cùng một mục, không phải 2 dòng | Không truy được "ăn lúc mấy giờ" trong ngày |
| D-708 | `CalorieTarget` theo `EffectiveDate` (UNIQUE), đọc mục tiêu hiệu lực gần nhất ≤ hôm nay | Mục tiêu đổi theo giai đoạn tập luyện mà vẫn giữ lịch sử | Muốn biết mục tiêu của một ngày quá khứ phải quét ngược |
| D-709 | Chưa đặt target → `remaining = null`, không phải `0` | `0` nghĩa là "đã ăn hết hạn mức", `null` nghĩa là "chưa đặt" — hai chuyện khác nhau | FE phải xử lý null ở mọi chỗ hiển thị |
| D-710 | Không có endpoint gộp tổng macro của `GET /meal-logs`; trả macro từng món để FE tự cộng | Tránh nhân bản công thức khi FE cần chia nhỏ theo bữa | FE phải cộng đúng; rủi ro lệch số với `calorie-summary` |

## 6. Data Flow

```text
Thêm món vào bữa:
  POST /meal-logs {memberId, logDate, mealType, items:[{foodItemId, quantity}]}
    → NutritionService
        ├─ ownership: Member(self) | Admin/Staff                 → 403
        ├─ quantity ≤ 0 / mealType không hợp lệ                  → 422 INVALID_QUANTITY
        ├─ FoodItem không tồn tại / không active                 → 404 FOOD_NOT_FOUND
        ├─ tìm MealLog theo (member, LogDate, MealType) — không có thì tạo
        ├─ món đã có trong bữa? → cộng dồn Quantity + Calories   (D-707)
        └─ INSERT meal_log_items { Calories = CaloriesPerUnit × Quantity }   ★ snapshot (D-701)

Tổng kết ngày:
  GET /members/{id}/calorie-summary?date=
    → consumed        = Σ meal_log_items.Calories trong ngày (giờ VN)
    → target          = CalorieTarget hiệu lực gần nhất ≤ ngày đó   (D-708)
    → remaining       = target − consumed   (null nếu chưa đặt target — D-709)
    → macro consumed  = Σ (food_items.ProteinG/CarbG/FatG × Quantity)  ← đọc LIVE (D-706)
    → CalorieSummaryResponse (dùng lại ở 360° — spec 006)

Tìm món (tier):
  GET /food-items?query=&page=
    → actor là Member VÀ không có Membership Active còn hạn?
         (MembershipLifecycle.IsActiveOn — spec 003)
      ├─ có  → universe = toàn bộ food_items active
      └─ không → universe = **20 món đầu A→Z**       ★ giới hạn TRƯỚC khi lọc query (D-705)
    → lọc theo Name với collation Latin1_General_100_CI_AI (bỏ dấu + hoa thường)
    → PagedResult (mặc định 20, tối đa 100)

Thêm món:
  POST /food-items {name, unit, caloriesPerUnit, …}
    → trùng tên → 200 trả món có sẵn
    → chưa có   → 201 tạo mới (Source = Admin; spec 009 tạo với Source = AI)
```

## 7. Traceability (FR → code)

| FR | Triển khai tại |
|---|---|
| FR-CAL-TGT-01 | `NutritionService` — upsert theo `UNIQUE(MemberId, EffectiveDate)` |
| FR-CAL-TGT-02 | `NutritionService` — lấy target hiệu lực gần nhất ≤ hôm nay, 404 `NO_TARGET` |
| FR-FOOD-01 | `Features/Nutrition/FoodItemService.cs` — collation CI_AI + giới hạn tier |
| FR-FOOD-02 | `FoodItemService` — find-or-create theo tên |
| FR-MEAL-01 | `NutritionService` — tạo/ghép `MealLog` + snapshot `Calories` |
| FR-MEAL-02 | `NutritionService` — 422 `INVALID_QUANTITY` |
| FR-MEAL-03 | `NutritionService` — cộng dồn khẩu phần cùng món/bữa/ngày |
| FR-CAL-01 | `NutritionService` — `CalorieSummaryResponse` (calo + macro) |
| FR-CAL-02 | `NutritionService` — cửa quyền assignment PT (spec 005) |

## 8. Complexity Tracking

| Vi phạm / lệch chuẩn | Vì sao chấp nhận | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| **Macro chưa snapshot** (D-706) — lệch DATA-02 | `meal_log_items` do team DB quản lý; thêm 3 cột cần vòng migration + đồng bộ lịch. Calo (chỉ số chính) đã snapshot đúng | Bỏ macro khỏi response → mất tính năng theo dõi dinh dưỡng; snapshot ngay → cần migration ngoài tầm kiểm soát của backend ở thời điểm làm |
| Giới hạn tier nằm ở tầng **truy vấn**, không phải tầng quyền | Đây là ràng buộc thương mại, không phải bảo mật — trả 403 sẽ sai ngữ nghĩa | Chặn bằng `[Authorize]` → Member không mua gói mất luôn tính năng, không dùng thử được |
| Phụ thuộc collation SQL Server (D-704) | Dự án đã chốt SQL Server (CONSTITUTION Layer 3) | Bỏ dấu trong C# → mất khả năng dùng index, tìm chậm dần theo kho món |
| Không có endpoint tổng macro (D-710) | FE cần macro chi tiết theo từng món để hiển thị | Thêm endpoint gộp → nhân bản công thức đã có ở `calorie-summary` |
| `NutritionService` phục vụ cả spec 006 và 009 | Công thức calo phải là một nguồn duy nhất | Mỗi spec tự tính → 3 kết quả lệch nhau cho cùng một ngày |
