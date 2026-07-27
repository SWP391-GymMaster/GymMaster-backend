# Luồng chạy phần của Minh (N5) — từ cú click đến bảng DB

**Người phụ trách:** Minh · N5 — Dinh dưỡng · AI quét ảnh · Dashboard & Audit · Trang giới thiệu
**Ngày viết:** 26/07/2026 · **Cập nhật:** 27/07/2026 — dựng từ source code thật.
**Đọc kèm:** [tong-quan-phan-cua-minh.md](tong-quan-phan-cua-minh.md) (cấu trúc tĩnh) — file này là **động**: ai bấm gì, chạy hàm nào, qua tầng nào, đụng bảng nào.

---

## QUY ƯỚC ĐỌC SƠ ĐỒ

Mọi luồng đều đi qua **9 tầng cố định**. Tầng nào không có thì bỏ trống, thứ tự không đổi:

```text
 ①  NGƯỜI DÙNG        ai · bấm gì
 ②  ROUTE             app/**/page.tsx           — file mỏng, chỉ render + guard
 ③  COMPONENT         features/**/components/   — state UI, gọi hook
 ④  HOOK              features/**/api/*.queries.ts — TanStack Query (cache + invalidate)
 ⑤  API FN            features/**/api/*.api.ts  — dựng URL + DTO
 ⑥  HTTP              lib/api/http-client.ts    — gắn Bearer, bóc ApiResponse   【DÙNG CHUNG】
 ─────────────────────── ranh giới mạng ───────────────────────
 ⑦  CONTROLLER        Features/**/XController.cs — [Authorize], chỉ điều phối
 ⑧  SERVICE           Features/**/XService.cs    — TOÀN BỘ nghiệp vụ
 ⑨  DB                GymMasterDbContext → bảng SQL Server

 Ký hiệu:   ★ = code Minh viết      ⚠ = code người khác, Minh chỉ đọc/gọi
            ⛔ = nhánh chặn (trả lỗi)   ↻ = invalidate cache → refetch
```

---

## BẢNG TRA NHANH — 13 API, AI GỌI, ĐỤNG BẢNG NÀO

| # | Người dùng làm gì | Endpoint | Service method ★ | Bảng đọc | Bảng ghi |
|---|---|---|---|---|---|
| 1 | Gõ tên món để tìm | `GET /food-items` | `FoodItemService.SearchAsync` | `food_items` ★, `member_profiles` ⚠, `memberships` ⚠ | — |
| 2 | Tạo món tự nhập | `POST /food-items` | `FoodItemService.AddAsync` | `food_items` ★ | `food_items` ★, `audit_logs` ★ |
| 3 | Bấm "Xác nhận" ghi bữa | `POST /meal-logs` | `NutritionService.CreateMealLogAsync` | `member_profiles` ⚠, `food_items` ★, `meal_logs` ★ | `meal_logs` ★, `meal_log_items` ★, `audit_logs` ★ |
| 4 | Mở nhật ký / đổi ngày | `GET /meal-logs` | `NutritionService.GetMealLogsAsync` | `meal_logs` ★, `meal_log_items` ★, `food_items` ★ | — |
| 5 | Bấm "Áp dụng" ở TDEE | `POST /members/{id}/calorie-target` | `NutritionService.SetTargetAsync` | `calorie_targets` ★ | `calorie_targets` ★, `audit_logs` ★ |
| 6 | Mở TDEE (nạp mục tiêu cũ) | `GET /members/{id}/calorie-target` | `NutritionService.GetTargetAsync` | `calorie_targets` ★ | — |
| 7 | Mở màn Tổng kết calo | `GET /members/{id}/calorie-summary` | `NutritionService.GetSummaryAsync` | `meal_logs` ★, `meal_log_items` ★, `food_items` ★, `calorie_targets` ★ | — |
| 8 | Xem biểu đồ 7 ngày | `GET /members/{id}/calorie-history` | `NutritionService.GetHistoryAsync` | như trên | — |
| 9 | Chụp/tải ảnh món | `POST /foods/scan-image` | `FoodScanService.ScanImageAsync` | `member_profiles` ⚠, `memberships` ⚠, `food_items` ★ | — *(gọi Gemini)* |
| 10 | Bấm "AI ước lượng" trong form món | `POST /foods/estimate-nutrition` | `FoodScanService.EstimateNutritionAsync` | `member_profiles` ⚠, `memberships` ⚠ | — *(gọi Gemini)* |
| 11 | Bấm "Chọn" ở món AI chưa có DB | `POST /foods/confirm-ai-food` | `FoodScanService.ConfirmAiFoodAsync` | `food_items` ★, `memberships` ⚠ | `food_items` ★, `audit_logs` ★ |
| 12 | Admin mở Dashboard | `GET /dashboard/summary` | `DashboardService.GetSummaryAsync` | `payments` ⚠, `memberships` ⚠, `check_ins` ⚠, `trainer_assignments` ⚠, `users` ⚠, `membership_packages` ⚠ | — |
| 13 | Admin mở/lọc Audit log | `GET /audit-logs` | `DashboardService.GetAuditLogsAsync` | `audit_logs` ★, `users` ⚠ | — |

> **Đọc bảng này trước khi sửa gì.** Cột "Bảng đọc" có ⚠ nghĩa là luồng đó **phụ thuộc người khác** —
> N3 đổi `memberships`/`payments` hay N4 đổi `check_ins` là luồng của Minh gãy.

---

## BẢN ĐỒ KỸ THUẬT — 13 API + 1 LUỒNG AUDIT NỘI BỘ

Phần này chỉ theo backend C# để khi debug có thể lần đúng file:

```text
Frontend → Controller → Interface → Service → DbContext/Gemini → DTO → HTTP response
```

Quyền chung:

- Mọi endpoint trong hai feature đều yêu cầu đăng nhập; thiếu token trả `401`.
- Dashboard và đọc Audit Log chỉ cho Admin; role khác trả `403`.
- Các API theo `memberId`: Admin/Staff được truy cập tất cả; Member chỉ chính mình; PT chỉ hội viên
  đang có `TrainerAssignment` active với PT đó.
- Ba API AI chỉ cho Member có membership active và chưa hết hạn.

### API 1 — Tìm món ăn

```text
Frontend
  → GET /api/v1/food-items?query=&page=&pageSize=
FoodItemsController.Search
  → IFoodItemService.SearchAsync
FoodItemService.SearchAsync
  → member_profiles + memberships (xác định full/free)
  → food_items (IsActive, lọc tên, sắp xếp, phân trang)
  → PagedResult<FoodItemResponse> trong NutritionDtos.cs
```

Nhánh:

- Admin/Staff/PT hoặc Member có gói active: tìm trong toàn bộ kho.
- Member không có gói active: chỉ tìm trong tập 20 món đầu theo tên A–Z.
- `query` rỗng: trả danh sách; có `query`: tìm không phân biệt dấu/hoa thường.
- `page < 1` được đưa về 1; `pageSize` ngoài 1–100 được đưa về 20.
- Không có món phù hợp: thành công với danh sách rỗng, không phải 404.

### API 2 — Tạo món tự nhập

```text
Frontend
  → POST /api/v1/food-items + CreateFoodItemRequest
FoodItemsController.Add
  → IFoodItemService.AddAsync
FoodItemService.AddAsync
  → food_items
  → IAuditService.LogAsync("CREATE_FOOD") nếu tạo mới
  → FoodItemResponse
```

Nhánh:

- Chỉ Member/Admin/Staff; PT bị `403`.
- Tên hoặc đơn vị trống, calo/macro âm: `400 VALIDATION_ERROR`.
- Trùng tên: không INSERT, trả món có sẵn với `200`.
- Chưa có: INSERT `food_items`, ghi `audit_logs`, trả `201`.

### API 3 — Ghi nhật ký bữa ăn

```text
Frontend
  → POST /api/v1/meal-logs + CreateMealLogRequest
MealLogsController.Create
  → INutritionService.CreateMealLogAsync
NutritionService.CreateMealLogAsync
  → member_profiles (tồn tại + ownership)
  → food_items (món active)
  → meal_logs + meal_log_items
  → IAuditService.LogAsync("CREATE_MEAL_LOG")
  → MealLogResponse trong NutritionDtos.cs
```

Nhánh:

- Không có hội viên: `404 NOT_FOUND`; sai quyền: `403 FORBIDDEN`.
- `Items` rỗng hoặc có `Quantity <= 0`: `422 INVALID_QUANTITY`.
- `MealType` không thuộc 1–4: `422 VALIDATION_ERROR`.
- Có food id không tồn tại/không active: `404 FOOD_NOT_FOUND`.
- Food id lặp trong request: gộp quantity trước khi tính.
- Chưa có `(MemberId, LogDate, MealType)`: tạo `MealLog`.
- Đã có: dùng log cũ; món cũ thì cộng quantity/calo, món mới thì thêm `MealLogItem`.
- Thành công luôn trả `201`, kể cả nhánh cộng vào log đã có.

### API 4 — Lấy nhật ký bữa ăn theo ngày

```text
Frontend
  → GET /api/v1/meal-logs?memberId=&date=
MealLogsController.GetByMemberAndDate
  → INutritionService.GetMealLogsAsync
NutritionService.GetMealLogsAsync
  → member_profiles
  → meal_logs + meal_log_items + food_items
  → IReadOnlyList<MealLogResponse>
```

Nhánh:

- Không có `date`: dùng hôm nay theo giờ Việt Nam.
- Không có hội viên: `404`; sai quyền: `403`.
- Không có bữa trong ngày: thành công với danh sách rỗng.
- Có dữ liệu: sắp theo `MealType` và tính `TotalCalories` cho từng bữa.

### API 5 — Đặt hoặc cập nhật mục tiêu calo

```text
Frontend
  → POST /api/v1/members/{id}/calorie-target + SetCalorieTargetRequest
MemberNutritionController.SetTarget
  → INutritionService.SetTargetAsync
NutritionService.SetTargetAsync
  → member_profiles
  → calorie_targets (UPSERT theo MemberId + EffectiveDate)
  → IAuditService.LogAsync("SET_CALORIE_TARGET")
  → CalorieTargetResponse
```

Nhánh:

- Không có hội viên: `404`; sai quyền: `403`.
- `DailyCalories <= 0` hoặc macro âm: `422 INVALID_TARGET`.
- Không gửi `EffectiveDate`: dùng hôm nay theo giờ Việt Nam.
- Chưa có mục tiêu cùng ngày hiệu lực: INSERT, trả `201`.
- Đã có: UPDATE đè calo/macro, trả `200`.

### API 6 — Lấy mục tiêu calo hiện hành

```text
Frontend
  → GET /api/v1/members/{id}/calorie-target
MemberNutritionController.GetTarget
  → INutritionService.GetTargetAsync
NutritionService.GetTargetAsync
  → member_profiles + calorie_targets
  → CalorieTargetResponse
```

Nhánh:

- Chỉ xét mục tiêu có `EffectiveDate <= hôm nay`, lấy ngày hiệu lực mới nhất.
- Không có hội viên: `404`; sai quyền: `403`.
- Hội viên chưa từng đặt mục tiêu phù hợp: `404 NO_TARGET`.

### API 7 — Tổng kết calo/macro một ngày

```text
Frontend
  → GET /api/v1/members/{id}/calorie-summary?date=
MemberNutritionController.GetSummary
  → INutritionService.GetSummaryAsync
NutritionService.GetSummaryAsync
  → meal_logs + meal_log_items + food_items
  → calorie_targets
  → CalorieSummaryResponse
```

Tính:

```text
ConsumedCalories = Σ MealLogItem.Calories
ConsumedMacro    = Σ FoodItem.Macro × MealLogItem.Quantity
Target           = mục tiêu mới nhất có EffectiveDate <= ngày đang xem
Remaining        = Target - Consumed
```

Nhánh:

- Không gửi `date`: dùng hôm nay.
- Không có hội viên: `404`; sai quyền: `403`.
- Chưa ăn: consumed bằng 0.
- Chưa có mục tiêu: target/remaining bằng `null`.
- Ăn vượt mục tiêu: remaining âm, không bị chặn.

### API 8 — Lịch sử calo theo khoảng ngày

```text
Frontend
  → GET /api/v1/members/{id}/calorie-history?from=&to=
MemberNutritionController.GetHistory
  → INutritionService.GetHistoryAsync
NutritionService.GetHistoryAsync
  → meal_logs + meal_log_items + calorie_targets
  → IReadOnlyList<CalorieSummaryResponse>
```

Nhánh:

- Không gửi `to`: dùng hôm nay; không gửi `from`: lấy `to - 6 ngày` (đủ 7 ngày).
- `from > to`: `422 VALIDATION_ERROR`.
- Không có hội viên: `404`; sai quyền: `403`.
- Luôn sinh một item cho từng ngày; ngày không ăn có consumed bằng 0.
- Mỗi ngày dùng mục tiêu mới nhất có ngày hiệu lực không lớn hơn ngày đó.
- Hiện tại history chỉ tổng hợp calo; các trường macro giữ giá trị mặc định.

### API 9 — Quét ảnh món ăn

```text
Frontend
  → POST /api/v1/foods/scan-image (multipart/form-data)
FoodScanController.ScanImage
  → IFoodScanService.ScanImageAsync
FoodScanService.ScanImageAsync
  → member_profiles + memberships (gác cổng)
  → IFoodImageAnalyzer.DetectFoodsAsync
  → GeminiService → Google Generative Language API
  → food_items (đối chiếu từng tên nhận diện)
  → FoodScanResponse trong FoodScanDtos.cs
```

Nhánh:

- Không phải Member/không có gói active: `403 MEMBERSHIP_REQUIRED`.
- File rỗng, không phải JPG/PNG hoặc lớn hơn 5MB: `422 INVALID_FILE`.
- Gemini lỗi/response hỏng: `502` với mã lỗi do analyzer trả.
- Tên trùng trong cùng kết quả AI: chỉ giữ một lần.
- Match DB: `ResultSource="Database"`, có `Food`, không cần xác nhận.
- Không match: `ResultSource="AI"`, có `Draft`, `RequiresConfirmation=true`; chưa ghi DB.

### API 10 — AI ước lượng dinh dưỡng từ tên

```text
Frontend
  → POST /api/v1/foods/estimate-nutrition + { name }
FoodScanController.EstimateNutrition
  → IFoodScanService.EstimateNutritionAsync
FoodScanService.EstimateNutritionAsync
  → member_profiles + memberships
  → IFoodImageAnalyzer.EstimateNutritionAsync
  → GeminiService
  → FoodNutritionDraft
```

Nhánh:

- Không có gói active: `403 MEMBERSHIP_REQUIRED`.
- Tên ngắn hơn 2 hoặc dài hơn 150 ký tự: `400 VALIDATION_ERROR`.
- Gemini lỗi: `502`.
- Thành công chỉ trả draft dinh dưỡng trên 100g, không ghi database.

### API 11 — Xác nhận và lưu món AI

```text
Frontend
  → POST /api/v1/foods/confirm-ai-food + ConfirmAiFoodRequest
FoodScanController.ConfirmAiFood
  → IFoodScanService.ConfirmAiFoodAsync
FoodScanService.ConfirmAiFoodAsync
  → member_profiles + memberships
  → food_items
  → IAuditService.LogAsync("CONFIRM_AI_FOOD") nếu tạo mới
  → ScannedFood
```

Nhánh:

- Không có gói active: `403`.
- Tên trống hoặc calo/macro âm: `400 VALIDATION_ERROR`.
- Tên đã tồn tại (so sánh không dấu/hoa thường): trả món cũ `200`, không ghi log mới.
- Chưa tồn tại: INSERT món `Unit="g"`, `ServingSize=100`, `Source="AI"`; ghi Audit; trả `201`.

### API 12 — Tổng hợp Admin Dashboard

```text
Frontend Admin
  → GET /api/v1/dashboard/summary?from=&to=
DashboardController.GetSummary
  → IDashboardService.GetSummaryAsync
DashboardService.GetSummaryAsync
  → payments + memberships + membership_packages
  → check_ins + trainer_assignments + member_profiles + users
  → DashboardSummaryResponse trong DashboardDtos.cs
```

Service tính doanh thu theo khoảng ngày, membership active/expired, check-in hôm nay, giao dịch chờ,
doanh thu 6 tháng, 10 hội viên vừa hết hạn, tải cơ sở, tải khu PT/khu chung, doanh thu tháng trước,
membership mới trong tháng và giờ cao điểm 30 ngày.

Nhánh:

- Chỉ Admin; role khác `403`.
- Không gửi `from/to`: doanh thu từ đầu tháng Việt Nam đến hiện tại.
- `from > to`: `422 INVALID_RANGE`.
- Không có dữ liệu: số bằng 0/danh sách rỗng; giờ cao điểm mặc định 17–19.
- `from/to` chỉ đổi tổng doanh thu theo khoảng; các chỉ số “hôm nay/tháng này” vẫn theo hiện tại.

### API 13 — Đọc và lọc Audit Log

```text
Frontend Admin
  → GET /api/v1/audit-logs?userId=&action=&from=&to=&search=&page=&pageSize=
AuditLogsController.GetAuditLogs
  → IDashboardService.GetAuditLogsAsync
DashboardService.GetAuditLogsAsync
  → audit_logs
  → users (ghép UserDisplayName)
  → PagedResult<AuditLogResponse>
```

Nhánh:

- Chỉ Admin; role khác `403`.
- `page < 1` thành 1; `pageSize` bị kẹp trong 1–100.
- `action` khớp chính xác; `search` tìm chứa trong `Action` hoặc `Entity`.
- Sắp xếp `CreatedAt` giảm dần rồi `Skip/Take`.
- User đã soft-delete/không tồn tại: log vẫn trả nhưng `UserDisplayName=null`.
- Không khớp filter: thành công với danh sách rỗng.

### LUỒNG NỘI BỘ 14 — Ghi Audit Log

Luồng này không có controller/endpoint riêng:

```text
Bất kỳ service nào vừa thay đổi dữ liệu
  → IAuditService.LogAsync(action, entity, entityId, metadata)
AuditService.LogAsync
  → lấy UserId từ NameIdentifier hoặc sub trong JWT
  → JsonSerializer.Serialize(metadata)
  → GymMasterDbContext.AuditLogs.Add(new AuditLog)
  → SaveChangesAsync
  → bảng audit_logs
```

Nhánh:

- Lấy được claim số: ghi `UserId`.
- Không có/claim không parse được: vẫn ghi log với `UserId=null`.
- `metadata=null`: cột Metadata để null; có object: lưu chuỗi JSON.
- Các action do phần Minh phát sinh: `CREATE_FOOD`, `CONFIRM_AI_FOOD`,
  `CREATE_MEAL_LOG`, `SET_CALORIE_TARGET`.
- Đổi chữ ký `IAuditService.LogAsync` ảnh hưởng mọi service của thành viên khác đang gọi nó.

---

## LUỒNG 0 — VÀO HỆ THỐNG (trang tĩnh của Minh)

### Hệ thống đang chạy ở đâu

Hệ thống **đã deploy lên Google Cloud Run** (project `gymmaster-500004`, region `asia-southeast1`) —
người dùng thật vào bằng URL công khai, không phải `localhost`:

| Môi trường | Frontend | Backend |
|---|---|---|
| **Production** (người dùng vào) | Cloud Run service `gymmaster-os` | `https://gymmaster-api-741815287158.asia-southeast1.run.app` |
| **Dev** (chỉ khi lập trình viên chạy máy mình) | `http://localhost:3000` | `http://localhost:5042` |

```text
   Trình duyệt người dùng
        │  https://<gymmaster-os>.asia-southeast1.run.app
        ▼
   ┌──────────────────────────┐        NEXT_PUBLIC_API_BASE_URL        ┌──────────────────────────┐
   │  Cloud Run: gymmaster-os │ ─────────────────────────────────────► │ Cloud Run: gymmaster-api │
   │  Next.js (node:22-alpine)│   bake lúc BUILD qua Dockerfile ARG    │  .NET 10, port 8080      │
   └──────────────────────────┘   (KHÔNG phải env runtime)             └────────────┬─────────────┘
                                                                                    │
                                                                       ┌────────────▼─────────────┐
                                                                       │ Cloud SQL for SQL Server │
                                                                       │  gymmaster-sql-sg        │
                                                                       └──────────────────────────┘
```

**3 điều ràng buộc luồng chạy của Minh trên production:**

| Điều | Ảnh hưởng tới phần Minh |
|---|---|
| **Scale-to-zero** — không request thì không container nào chạy | Không có `BackgroundService` nào. Request **đầu tiên sau khi ngủ bị lạnh (cold start)** — luồng 9 (Admin Dashboard, 11 truy vấn) là chỗ cảm nhận rõ nhất |
| **`NEXT_PUBLIC_API_BASE_URL` bake lúc build** (`Dockerfile:19` ARG) | Đổi URL backend phải **sửa Dockerfile + build lại FE**, sửa env của Cloud Run không ăn thua |
| **`/openapi/v1.json` chỉ sống ở local** (`MapOpenApi()` bọc trong `IsDevelopment()`) | Trên cloud endpoint này luôn 404 — 404 ở đó **không phải** deploy hỏng |

### Luồng vào của người dùng

```text
①  Khách mở https://<gymmaster-os>.asia-southeast1.run.app
       │  (dev: localhost:3000 — cùng một code, chỉ khác host)
       │
②  app/page.tsx ★                          5 dòng
       │  redirect("/welcome")             ← KHÔNG gọi API
       ▼
②  app/(auth)/welcome/page.tsx ★           541 dòng, Server Component tĩnh
       │  const DEMO_ACCOUNTS  = [4 tài khoản demo]
       │  const ONBOARDING_SLIDES = [3 slide]
       │  <WelcomeFeature />               ← hàm render nội bộ, dòng 513
       │
       ├──► /about  → app/(auth)/about/page.tsx ★  400 dòng
       │              3 section: hero · #workspaces · #innovations
       │
       └──► /login  → ⚠ N1 (Như) — HẾT PHẦN CỦA MINH
                        │  AuthService.LoginAsync → JWT
                        ▼  redirectPath theo role
              ┌─────────┴──────────┬──────────────┬──────────────┐
              ▼                    ▼              ▼              ▼
        /member/dashboard ★  /admin/dashboard ★  /staff/…  /pt/…
```

**Đặc điểm:** 3 màn này **không gọi 1 API nào**, không có state server. Tầng ④⑤⑥⑦⑧⑨ trống hoàn toàn.

---

## LUỒNG 1 — MEMBER MỞ NHẬT KÝ ĂN (màn load)

```text
①  Member vào /member/nutrition/meal-journal
       │
②  app/(member)/member/nutrition/meal-journal/page.tsx ★
       │  <PermissionGuard allowedRoles={["member"]}>      ⚠ N1 — sai role → đá về
       │  <WorkspaceShell role="member">                   ⚠ dùng chung
       │  <MembershipGate allowFreeTier>                   ⚠ N1
       │     ▲ allowFreeTier = CHƯA có gói VẪN VÀO ĐƯỢC (khác 2 màn còn lại)
       ▼
③  MealJournalWorkspace.tsx ★  (234 dòng)
       │  const today = getTodayDate()                    ★ utils/nutrition-formatters.ts
       │  const [selectedDate, setSelectedDate] = useState(today)
       │  useEffect → đọc ?view=add&type=lunch / #add-meal → setActiveView("add")
       │
       ├─④─► useMemberCalorieSummary(selectedDate) ★
       │        │
       │     ⑤─► getMemberCalorieSummary(token, memberId, date) ★
       │        │   GET /api/v1/members/{id}/calorie-summary?date=…
       │        │
       │     ⑥─► apiRequest()  ⚠ lib/api/http-client.ts — gắn Bearer
       │        ══════════════════════════════════════════════
       │     ⑦─► MemberNutritionController.GetSummary ★     [Authorize]
       │     ⑧─► NutritionService.GetSummaryAsync ★
       │        │   ├─ FindMemberAsync         → member_profiles ⚠ (404 nếu không có) ⛔
       │        │   ├─ CanAccessAsync          → 403 nếu không phải chính mình ⛔
       │        │   ├─ Σ meal_log_items.Calories
       │        │   ├─ Σ FoodItem.macro × Quantity     ← macro KHÔNG lưu, luôn tính lại
       │        │   └─ calorie_targets: EffectiveDate ≤ ngày, mới nhất
       │     ⑨─► meal_logs ★ · meal_log_items ★ · food_items ★ · calorie_targets ★
       │
       └─④─► useMemberMealLogs(selectedDate) ★
                │  ⑤ getMemberMealLogs → GET /api/v1/meal-logs?memberId=&date=
                │  ⑦ MealLogsController.GetByMemberAndDate ★
                │  ⑧ NutritionService.GetMealLogsAsync ★  (Include Items → FoodItem)
                │  ⑨ meal_logs ★ · meal_log_items ★ · food_items ★
                ▼
③  render:  <NutritionSummaryCard>  ★ 395 dòng — vòng calo + 3 thanh macro
            <MealLogList>           ★ 230 dòng — 4 nhóm buổi ăn
            <MealLogForm>           ★ 597 dòng — khi activeView === "add"
            <TdeeCalculator>        ★ 639 dòng — dialog, mở bằng isTdeeOpen
```

**2 request song song** khi mở màn. Cache key: `["member-nutrition","summary",memberId,date]` và `[…,"meal-logs",memberId,date]` → đổi ngày là đổi key, tự fetch lại.

---

## LUỒNG 2 — TÌM MÓN ĂN

```text
①  Member gõ "com" vào ô tìm
       │
③  FoodSearchPanel.tsx ★ (963 dòng — component to nhất)
       │  const foods = useFoodSearch(query)
       │  ▲ chỉ bắn khi query.trim().length >= 2
       │
④  useFoodSearch ★     queryKey: ["member-nutrition","foods","com"]
⑤  searchFoodItems ★   GET /api/v1/food-items?query=com&page=1
⑥  apiRequest ⚠
   ══════════════════════════════════════════════════════════════════
⑦  FoodItemsController.Search ★     [Authorize] — mọi role đăng nhập
⑧  FoodItemService.SearchAsync ★
       │
       ├─ HasFullFoodAccessAsync(principal)             ← QUYẾT ĐỊNH THẤY BAO NHIÊU MÓN
       │    ├─ KHÔNG phải role Member (Admin/Staff/PT)  → full kho
       │    └─ Là Member → member_profiles ⚠ → memberships ⚠
       │         Active && EndDate ≥ hôm nay ?  → full kho
       │         không có gói                   → CHỈ 20 MÓN ĐẦU (A→Z)
       │              foodItems = foodItems.Where(freeFoodIds.Contains(Id))
       │              ▲ giới hạn universe TRƯỚC khi lọc từ khoá
       │                → món thứ 21 "không tồn tại" với người chưa mua gói
       │
       ├─ nếu có query:  EF.Functions.Collate(Name, "Latin1_General_100_CI_AI").Contains(kw)
       │                 ▲ bỏ dấu + bỏ hoa/thường: "com" ra "Cơm", "thit" ra "Thịt"
       │
       └─ OrderBy(Name) → Skip/Take → PagedResult<FoodItemResponse>
⑨  food_items ★ (+ member_profiles ⚠, memberships ⚠ để phân quyền)
       ▼
③  hiện danh sách → bấm 1 món → onSelectFood(food) → MealLogForm.selectFood()

   ⓧ Nhánh "tra cứu online":  ENABLE_ONLINE_SEARCH = false  (FoodSearchPanel.tsx:133)
      → useFoodOnlineSearch → thử GET /food-items/online-search (BE CHƯA CÓ)
        → fallback world.openfoodfacts.org      ⚠ CẢ NHÁNH ĐANG TẮT
```

---

## LUỒNG 3 — TẠO MÓN TỰ NHẬP + AI ƯỚC LƯỢNG MACRO

```text
①  Không tìm thấy món → bấm "Tạo món mới"
       │
③  CreateCustomFoodDialog ★ (FoodSearchPanel.tsx:739)
       │  const createFood   = useCreateCustomFoodItem()
       │  const estimateFood = useEstimateFoodNutrition()
       │
       ├── ① bấm "AI ước lượng" ────────────────────────────────────────┐
       │   ④ useEstimateFoodNutrition ★ (useMutation)                   │
       │   ⑤ estimateFoodNutrition(token, name) ★                       │
       │      POST /api/v1/foods/estimate-nutrition  { name }           │
       │   ⑦ FoodScanController.EstimateNutrition ★ [Authorize(Member)] │
       │   ⑧ FoodScanService.EstimateNutritionAsync ★                   │
       │        ├─ HasActivePackageAsync → chưa có gói: 403 ⛔          │
       │        ├─ tên < 2 hoặc > 150 ký tự: 400 ⛔                     │
       │        └─ GeminiService.EstimateNutritionAsync ★               │
       │              → Gemini API (schema OBJECT, /100g)               │
       │              → âm hoặc parse lỗi: 502 INVALID_AI_RESPONSE ⛔   │
       │   ⑨ KHÔNG ghi DB — chỉ trả draft điền sẵn vào form ◄───────────┘
       │
       └── ① bấm "Lưu món"
           ④ useCreateCustomFoodItem ★
           ⑤ createCustomFoodItem ★   POST /api/v1/food-items
           ⑦ FoodItemsController.Add ★  [Authorize(Member,Admin,Staff)]
           ⑧ FoodItemService.AddAsync ★
                ├─ tên/đơn vị rỗng, macro âm: 400 ⛔
                ├─ Find-or-Create: đã có tên đó → trả 200 + món cũ
                │    ▲ vì food_items có UNIQUE(Name) — tránh 409 làm vỡ UI
                ├─ INSERT food_items (Source mặc định "Admin")
                └─ AuditService.LogAsync("CREATE_FOOD","FoodItem",id) ★
           ⑨ food_items ★ + audit_logs ★
              ↻ invalidate ["member-nutrition","foods"] → ô tìm kiếm tự refetch
```

---

## LUỒNG 4 — QUÉT ẢNH MÓN ĂN BẰNG AI (spec 009)

```text
①  Member bấm "Quét ảnh món ăn bằng AI" → chọn/chụp ảnh
       │
③  AiFoodScanCard.tsx ★ (195 dòng)
       │  handleFile(file)
       │    └─ chặn tại chỗ: type ∉ {jpeg,png} hoặc size > 5MB → toast, KHÔNG gọi API ⛔
       │  const scan = useMutation({ mutationFn: scanFoodImage })
       │
⑤  scanFoodImage(token, image) ★
       │  FormData.append("image", file) → POST /api/v1/foods/scan-image (multipart)
⑥  apiRequest ⚠ (không set Content-Type để browser tự gắn boundary)
   ══════════════════════════════════════════════════════════════════════════
⑦  FoodScanController.ScanImage ★
       │  [Authorize(Roles=Member)] · [Consumes multipart] · [RequestSizeLimit 6MB]
⑧  FoodScanService.ScanImageAsync ★
       │
       ├─ 1. CỔNG GÓI TẬP  HasActivePackageAsync(actorId)
       │      member_profiles ⚠ → memberships ⚠ (Active && EndDate ≥ hôm nay)
       │      KHÔNG có gói → 403 MEMBERSHIP_REQUIRED ⛔  ← khác luồng 2, ở đây chặn hẳn
       │
       ├─ 2. VALIDATE ẢNH  null / 0 byte / > MaxImageBytes / ContentType sai
       │      → 422 INVALID_FILE ⛔
       │
       ├─ 3. đọc stream → byte[]
       │
       ├─ 4. GeminiService.DetectFoodsAsync ★ ─────────────────────────────┐
       │        POST {BaseUrl}/models/gemini-2.5-flash:generateContent      │
       │          responseSchema = ARRAY[{foodName, confidence,             │
       │                                  estimatedGrams, calories,         │
       │                                  proteinG, carbsG, fatG}]          │
       │          temperature 0.2 · maxOutputTokens 4096                    │
       │          thinkingConfig.thinkingBudget = 0                         │
       │            ▲ không tắt thì token "suy nghĩ" ăn hết budget → rỗng   │
       │        ExtractOutputText → GHÉP MỌI parts[*].text                  │
       │            ▲ output dài Gemini chia nhiều part, lấy 1 part là cụt  │
       │        StripJsonFence → gỡ ```json … ```                           │
       │        lỗi: AI_NOT_CONFIGURED · RECOGNITION_UNAVAILABLE ·          │
       │             RECOGNITION_TIMEOUT · INVALID_AI_RESPONSE ·            │
       │             FOOD_NOT_RECOGNIZED   → 502 ⛔                          │
       │     ◄──────────────────────────── List<DetectedFood> ─────────────┘
       │
       └─ 5. VỚI MỖI MÓN (bỏ trùng tên bằng HashSet):
              FindDatabaseMatchAsync(name) → food_items ★
                khớp chính xác (CI_AI) → nếu không thì Contains, ưu tiên tên ngắn nhất
              ┌── CÓ trong DB  → resultSource="Database", requiresConfirmation=false
              └── KHÔNG có     → resultSource="AI",       requiresConfirmation=true
                                 + draft {name, "g", 100, calories, macro, "AI"}
⑨  food_items ★ (chỉ ĐỌC) · member_profiles ⚠ · memberships ⚠
       ▼
③  hiện danh sách món + độ tin cậy + gram ước lượng
       │
①  bấm "Chọn" ở 1 món
       │
③  selectItem(item) ★
       ├─ item.food có sẵn (đã ở DB)  → dùng luôn
       └─ chỉ có draft → confirmAiFood(token, draft) ★
              ⑦ FoodScanController.ConfirmAiFood ★
              ⑧ FoodScanService.ConfirmAiFoodAsync ★
                   ├─ lại kiểm gói active (403 ⛔) · tên rỗng/macro âm (400 ⛔)
                   ├─ đã có tên đó (CI_AI) → trả món cũ, KHÔNG tạo trùng
                   ├─ INSERT food_items { Source="AI", Unit="g", ServingSize=100 }
                   └─ AuditService.LogAsync("CONFIRM_AI_FOOD") ★
              ⑨ food_items ★ + audit_logs ★
       ▼
③  onSelectFood(food, item.estimatedGrams)
       └─► MealLogForm.selectFood(food, grams)
             setValue("quantity", Math.round(grams))   ← AI điền sẵn số gram
```

---

## LUỒNG 5 — GHI BỮA ĂN (giỏ hàng → 1 hoặc nhiều POST)

Đây là luồng **phức tạp nhất** phần Minh: UI dùng **giỏ**, API gọi theo **nhóm**.

```text
①  Chọn món → nhập gram → bấm "Thêm vào danh sách"
       │
③  MealLogForm.addToCart(values) ★    ⚠ KHÔNG GỌI API — chỉ đẩy vào state `cart`
       │  cart.push({ uid, food, quantity(gram), mealType, logDate })
       │  saveRecentFood(food)          → localStorage "món gần đây"
       │  reset() nhưng GIỮ mealType + logDate → chọn tiếp món khác cho nhanh
       │
       └─ (lặp lại nhiều món…)
       │
①  bấm "Xác nhận"
       │
③  MealLogForm.confirmCart() ★
       │  ├─ chưa có memberId → toast lỗi, dừng ⛔
       │  ├─ GOM theo khoá `${mealType}|${logDate}`
       │  │    ▲ backend nhận 1 mealType/lần → 2 buổi ăn = 2 request
       │  └─ quantity gửi đi = quantity(gram) / 100
       │       ▲▲ QUY ƯỚC QUAN TRỌNG: UI nhập GRAM, API nhận SỐ PHẦN 100g.
       │          Backend tính Calories = CaloriesPerUnit × Quantity,
       │          mà CaloriesPerUnit là calo/100g → nhân đúng.
       │
④  useCreateMemberMealLog ★ .mutateAsync(...)  — gọi tuần tự cho từng nhóm
⑤  createMemberMealLog ★
       │  MEAL_TYPE_TO_BYTE: breakfast→1 lunch→2 dinner→3 snack→4
       │  POST /api/v1/meal-logs
   ══════════════════════════════════════════════════════════════════════
⑦  MealLogsController.Create ★  [Authorize]
⑧  NutritionService.CreateMealLogAsync ★  (luồng nghiệp vụ 7 bước)
       │
       ├─ 1. FindMemberAsync           member_profiles ⚠ → 404 NOT_FOUND ⛔
       ├─ 2. CanAccessAsync            4 nhánh role:
       │        Admin/Staff → OK
       │        Member      → userId claim == profile.UserId ?
       │        PT          → trainer_profiles ⚠ → trainer_assignments ⚠ Active ?
       │        khác        → 403 FORBIDDEN ⛔
       ├─ 3. Items rỗng / Quantity ≤ 0  → 422 INVALID_QUANTITY ⛔
       │     MealType ∉ enum            → 422 VALIDATION_ERROR ⛔
       ├─ 4. GroupBy(FoodItemId) → cộng dồn nếu client gửi trùng món
       ├─ 5. nạp food_items ★ (IsActive) — thiếu món → 404 FOOD_NOT_FOUND ⛔
       ├─ 6. tìm meal_logs ★ theo (MemberId, LogDate, MealType)
       │        ┌── ĐÃ CÓ  → cộng dồn vào MealLogItem cũ (Quantity +=, Calories +=)
       │        └── CHƯA   → tạo MealLog mới
       │        ▲ luật: 1 người · 1 ngày · 1 buổi = ĐÚNG 1 bản ghi
       │     Calories = FoodItem.CaloriesPerUnit × Quantity
       ├─ 7. SaveChanges
       └─ 8. AuditService.LogAsync("CREATE_MEAL_LOG","MealLog",id,{memberId,logDate}) ★
⑨  ĐỌC: member_profiles ⚠ · food_items ★ · meal_logs ★
    GHI: meal_logs ★ · meal_log_items ★ · audit_logs ★
       ▼
④  onSuccess → ↻ invalidate 3 key:
       ["member-nutrition","meal-logs",memberId,logDate]
       ["member-nutrition","summary" ,memberId,logDate]
       ["member-nutrition"]                      ← quét sạch, kể cả history
       ▼
③  NutritionSummaryCard + MealLogList tự refetch → số calo nhảy ngay
```

---

## LUỒNG 6 — ĐẶT MỤC TIÊU CALO BẰNG MÁY TÍNH TDEE

```text
①  Bấm nút "Mục tiêu" → dialog TDEE mở (isTdeeOpen = true)
       │
③  TdeeCalculator.tsx ★ (639 dòng)
       │
       ├─④ useMemberCalorieTarget() ★         ← nạp mục tiêu cũ để điền sẵn
       │     ⑤ getMemberCalorieTarget → GET /members/{id}/calorie-target
       │     ⑦ MemberNutritionController.GetTarget ★
       │     ⑧ NutritionService.GetTargetAsync ★
       │          EffectiveDate ≤ hôm nay, ORDER BY DESC, lấy 1
       │          chưa đặt bao giờ → 404 NO_TARGET
       │            ▲ hook BẮT lỗi này và trả null (retry:false) — không phải bug
       │     ⑨ calorie_targets ★
       │
       ├─① nhập tuổi/giới/cân/cao/mức vận động
       │  ③ calculateTdee()            ← Mifflin-St Jeor, TÍNH Ở CLIENT, không gọi API
       │  ③ handleGoalChange()         → updateProposedCalorie(tdee, goal)
       │  ③ handleDietTemplateChange() → chia tỉ lệ macro theo template
       │  ③ handleManualCalorieChange()
       │
       └─① bấm "Áp dụng"
          ③ handleApply()
          ④ useSetMemberCalorieTarget ★ .mutate({ dailyCalories, proteinG, carbG, fatG })
          ⑤ setMemberCalorieTarget ★  POST /members/{id}/calorie-target
          ⑦ MemberNutritionController.SetTarget ★
          ⑧ NutritionService.SetTargetAsync ★
               ├─ FindMember + CanAccess           404 / 403 ⛔
               ├─ DailyCalories ≤ 0 hoặc macro âm  422 INVALID_TARGET ⛔
               ├─ effectiveDate = request ?? hôm nay (giờ VN)
               ├─ UPSERT theo UNIQUE(MemberId, EffectiveDate):
               │     chưa có → INSERT, trả 201
               │     có rồi  → UPDATE đè, trả 200
               │     ▲ giữ được lịch sử mục tiêu theo ngày
               └─ AuditService.LogAsync("SET_CALORIE_TARGET") ★
          ⑨ calorie_targets ★ + audit_logs ★
             ↻ invalidate ["member-nutrition"] toàn bộ
          ③ onTargetApplied(newTarget) → MealJournalWorkspace.handleTargetApplied()
                                          → cập nhật optimisticCalorieTarget ngay
```

> Ghi chú: `optimisticCalorieTarget` khởi tạo là `null`, **không bịa 2200, không đọc localStorage** —
> backend là nguồn sự thật duy nhất cho mục tiêu calo.

---

## LUỒNG 7 — MÀN TỔNG KẾT CALO

```text
①  Member vào /member/nutrition/summary
②  page.tsx ★ → PermissionGuard ⚠ + MembershipGate ⚠ (KHÔNG allowFreeTier → phải có gói)
③  CalorieSummaryWorkspace.tsx ★ (761 dòng)
       │  const summary = useMemberCalorieSummary(selectedDate)   → API #7
       │  const logs    = useMemberMealLogs(selectedDate)         → API #4
       │  handleTargetApplied() ← TdeeCalculator dialog           → API #5
       │
       │  Các hàm render nội bộ (không gọi API):
       │    InsightMetric · MacroTrack · MacroRatioCard · CategoryCard · NextAction
       │    getMacroPercent() · formatMacroGrams() · formatDisplayDate()
       ▼
   Hiển thị: đã ăn / mục tiêu / còn lại · 3 thanh macro · tỉ lệ P-C-F · list bữa
```

---

## LUỒNG 8 — MEMBER DASHBOARD

```text
①  Member vào /member/dashboard
②  page.tsx ★ → MembershipGate ⚠ (bắt buộc có gói)
③  MemberDashboardContent.tsx ★ (343 dòng)
       │  ④ useMemberCalorieSummary(today) ★     → API #7 (bảng của Minh)
       │  ④ useCurrentMemberProfileId() ★        ← đọc auth store ⚠, KHÔNG gọi API
       │  ③ <BmiCalculator> ★      369 dòng — tính BMI ở client, không API
       │  ③ <WaterTrackerCard> ★   270 dòng — localStorage, KHÔNG có API/bảng DB
       │  ③ HeroChip() · SupportCard()  — render nội bộ
       ▼
   1 request duy nhất. 2 widget còn lại chạy hoàn toàn phía trình duyệt.
```

---

## LUỒNG 9 — ADMIN DASHBOARD (đọc dữ liệu của cả 4 người kia)

```text
①  Admin vào /admin/dashboard
②  app/(admin)/admin/dashboard/page.tsx ★ → <AdminPageFrame> ★ + <AdminDashboardContent> ★
③  AdminDashboardContent.tsx ★ (226 dòng)
       │  const summary = useDashboardSummary()
       │  const c = useChartColors()          ⚠ hook dùng chung
       │  isLoading → 4 <DashboardMetricCard isLoading> ★  (skeleton)
       │  error     → khối đỏ + nút "Thử lại" → summary.refetch()
④  useDashboardSummary ★    queryKey ["admin-dashboard","summary"]
⑤  getDashboardSummary ★    GET /api/v1/dashboard/summary
   ══════════════════════════════════════════════════════════════════════
⑦  DashboardController.GetSummary ★   [Authorize(Roles = Admin)]  ← role khác: 403 ⛔
⑧  DashboardService.GetSummaryAsync ★  — 11 truy vấn, 1 response
       │
       │  chuẩn hoá giờ trước:  nowVn = AppClock.NowVn() (UTC+7)
       │                        todayDate = AppClock.Today()
       │                        VnMonthStartUtc(y,m) = đầu tháng VN quy về UTC
       │                        from > to → 422 INVALID_RANGE ⛔
       │
       ├─ ①  revenue                Σ payments ⚠ Paid, PaidAt ∈ [from,to]
       ├─ ②  activeCount            memberships ⚠ Active && EndDate ≥ hôm nay
       ├─ ③  expiredCount           Expired ∪ (Active nhưng EndDate < hôm nay)
       ├─ ④  todayCheckInCount      check_ins ⚠ trong [todayUtcStart, +1 ngày)
       ├─ ⑤  pendingPaymentAmount   Σ payments ⚠ Pending
       ├─ ⑥  pendingPaymentCount    COUNT payments ⚠ Pending
       ├─ ⑦  revenueByMonth[6]      kéo payments 6 tháng → GroupBy(PaidAt+7h) TRONG BỘ NHỚ
       │                            → bù 0 cho tháng không có doanh thu
       ├─ ⑧  recentlyExpired[10]    memberships ⚠ ⋈ member_profiles ⚠ ⋈ users ⚠
       │                            ⋈ membership_packages ⚠ → GetInitials(FullName)
       ├─ ⑨  ptCheckInsToday        check_ins ⚠ ⋈ trainer_assignments ⚠ Active
       ├─ ⑩  previousMonthRevenue   payments ⚠ tháng trước → FE tính % tăng/giảm
       └─ ⑪  peakHour               check_ins ⚠ 30 ngày → GroupBy((Hour+7)%24)
                                    rỗng → mặc định 17–19h
       │
       │  suy ra (không truy vấn):
       │    facilityLoad% = min(100, todayCheckIn / GymCapacity(50) × 100)
       │    ptSession%    = min(facilityLoad%, ptCheckIn / 50 × 100)
       │    generalArea%  = facilityLoad% − ptSession%
⑨  ĐỌC 6 bảng CỦA NGƯỜI KHÁC: payments ⚠ · memberships ⚠ · membership_packages ⚠ ·
                               check_ins ⚠ · trainer_assignments ⚠ · users ⚠
    KHÔNG ghi gì.
       ▼
③  4 MetricCard + BarChart 6 tháng (recharts) + list hết hạn
   formatCompactVnd(): ≥1 tỷ → "1,2 tỷ" · ≥1 triệu → "850 triệu"
```

> ⚠ **Đây là luồng dễ gãy nhất của Minh.** Không có bảng nào của Minh trong đó.
> N3 đổi enum `PaymentStatus`/`MembershipStatus`, N4 đổi cột `CheckInAt` → dashboard sai số hoặc vỡ build.

---

## LUỒNG 10 — AUDIT LOG (Minh viết, cả 5 người ghi vào)

### 10a. Chiều GHI — 4 người kia gọi service của Minh

```text
  ⚠ N1 AuthService        ⚠ N2 MemberService       ⚠ N3 PaymentService      ⚠ N4 CheckInService
  ⚠ N1 UserService        ⚠ N2 TrainerService      ⚠ N3 MembershipService   ⚠ N4 AssignmentService
        │                        │                        │                        │
        └────────────────────────┴───────────┬────────────┴────────────────────────┘
                                             │  IAuditService.LogAsync(action, entity, id, metadata) ★
                                             ▼
                            ⑧ AuditService.LogAsync ★  (47 dòng)
                                 UserId    ← HttpContext.User claim NameIdentifier / sub
                                 Metadata  ← JsonSerializer.Serialize(object)
                                 CreatedAt ← DateTime.UtcNow
                                 SaveChangesAsync
                                             ▼
                            ⑨ audit_logs ★

   ★ Action do CHÍNH Minh sinh ra:
        CREATE_MEAL_LOG · SET_CALORIE_TARGET · CREATE_FOOD · CONFIRM_AI_FOOD
```

### 10b. Chiều ĐỌC — màn của Minh

```text
①  Admin vào /admin/audit-logs, chỉnh filter, bấm "Áp dụng"
②  app/(admin)/admin/audit-logs/page.tsx ★ → <AdminPageFrame> ★ + <AuditLogsContent> ★
③  AuditLogsContent.tsx ★ (424 dòng)
       │  const [page, setPage] = useState(1)
       │  const [activeFilters, setActiveFilters] = useState({...})
       │  handleApplyFilters(values) → setActiveFilters + setPage(1)
       │  useMemo → queryFilters
       │  ③ <AuditLogFilters> ★ 188 dòng — user / action / from / to / search
       │  ③ <AuditLogTable>   ★ 316 dòng
       │  ③ AuditLogDetailPanel · buildAuditMetrics · getAuditSeverity ·
       │     getActionDescription · ActionIcon · SeverityBadge   (thuần client)
④  useAuditLogs(filters) ★   queryKey ["admin-dashboard","audit-logs",{...filters}]
                              ▲ filter nằm trong key → đổi filter là tự fetch lại
⑤  getAuditLogs ★   GET /api/v1/audit-logs?page=&userId=&action=&from=&to=&search=
   ══════════════════════════════════════════════════════════════════════
⑦  AuditLogsController.GetAuditLogs ★   [Authorize(Roles = Admin)] ⛔ role khác 403
⑧  DashboardService.GetAuditLogsAsync ★
       ├─ page < 1 → 1 ; pageSize kẹp [1..100]
       ├─ lọc dần: UserId · Action (khớp chính xác) · CreatedAt ≥ from · ≤ to
       │            search → Action.Contains ∪ Entity.Contains
       ├─ COUNT tổng
       ├─ ORDER BY CreatedAt DESC → Skip/Take
       └─ nạp tên người thao tác: users ⚠ (IsDeleted = false) → Dictionary
          ▲ 2 truy vấn thay vì JOIN — tránh N+1
⑨  audit_logs ★ (ĐỌC) + users ⚠
       ▼
③  PagedResult<AuditLogEntry> → bảng + phân trang + panel chi tiết
```

---

## LUỒNG 11 — HAI MÀN "CỦA MINH" NHƯNG CODE Ở NHÀ NGƯỜI KHÁC

```text
①  Staff vào /staff/dashboard
②  app/(staff)/staff/dashboard/page.tsx ★  13 dòng — file này của Minh
③  <StaffPageFrame> + <StaffDashboard>     ⚠ features/staff-front-desk/ — N3 (Lộc)
   → toàn bộ logic, API, state đều của N3

①  PT vào /pt/dashboard
②  app/(pt)/pt/dashboard/page.tsx ★        13 dòng — file này của Minh
③  <PtPageFrame> + <PtDashboardContent>    ⚠ features/pt-dashboard/ — N4 (Đam)
   → toàn bộ logic, API, state đều của N4
```

> Hai màn này **tính điểm cho Minh** theo `phan-cong.md` nhưng phần chạy thật nằm ở N3/N4.
> Khai vào Project Tracking thì ghi rõ phạm vi là **route + khung trang**, tránh trùng với 2 bạn.

---

## BẢNG TỔNG HỢP — MỖI TẦNG CÓ GÌ

| Tầng | Của Minh ★ | Dùng chung / của người khác ⚠ |
|---|---|---|
| ② ROUTE | 10 file `page.tsx` (5 + 400 + 541 + 13×4 + 25×3) | `PermissionGuard`, `MembershipGate`, `WorkspaceShell` (N1 + chung) |
| ③ COMPONENT | 22 component (12 nutrition + 10 dashboard) — 6.653 LOC | `components/ui/*`, `useChartColors`, `StaffDashboard` (N3), `PtDashboardContent` (N4) |
| ④ HOOK | 13 hook (11 nutrition + 2 dashboard) | `useAuthSessionStore` (N1) |
| ⑤ API FN | 17 hàm (`member-nutrition.api.ts`, `nutrition-api.ts`, `admin-dashboard.api.ts`) | — |
| ⑥ HTTP | — | `lib/api/http-client.ts` (chung 5 người) |
| ⑦ CONTROLLER | 6 controller · 13 action | `ApiControllerBase` (chung) |
| ⑧ SERVICE | 5 service (`FoodItem`, `Nutrition`, `FoodScan`, `Dashboard`, `Audit`) + `GeminiService` | `ServiceResult`, `ApiResponse`, `PagedResult`, `AppClock` (chung) |
| ⑨ DB | 5 bảng: `food_items`, `meal_logs`, `meal_log_items`, `calorie_targets`, `audit_logs` | 7 bảng chỉ đọc: `member_profiles`, `memberships`, `membership_packages`, `payments`, `check_ins`, `trainer_assignments`, `users`, `trainer_profiles` |

---

## PHỤ THUỘC RA NGOÀI — SỬA CHỖ NÀO THÌ MINH GÃY

```text
                    ┌──────────────────────────────────────────────┐
                    │           PHẦN CỦA MINH (N5)                 │
                    └──────────────────────────────────────────────┘
                       ▲            ▲             ▲            ▲
      ┌────────────────┘            │             │            └────────────────┐
      │                             │             │                             │
┌─────┴──────┐            ┌─────────┴──────┐  ┌───┴────────────┐      ┌─────────┴────────┐
│ N1 — Như   │            │ N2 — Quang Anh │  │ N3 — Lộc       │      │ N4 — Đam         │
├────────────┤            ├────────────────┤  ├────────────────┤      ├──────────────────┤
│ JWT claim  │            │ member_profiles│  │ memberships    │      │ check_ins        │
│  (actorId) │            │  (mọi ownership│  │  ▲ cổng "có gói│      │  ▲ dashboard đếm │
│ users      │            │   check)       │  │    active"     │      │    + giờ cao điểm│
│  ▲ tên ở   │            │ trainer_profiles│ │ payments       │      │ trainer_assignments
│    audit   │            │  ▲ quyền PT    │  │  ▲ toàn bộ     │      │  ▲ quyền PT +    │
│ Permission-│            │                │  │    doanh thu   │      │    % PT session  │
│  Guard     │            │                │  │ membership_pkgs│      │                  │
└────────────┘            └────────────────┘  └────────────────┘      └──────────────────┘

   Gãy nặng nhất nếu:
     · N3 đổi enum MembershipStatus / PaymentStatus   → Dashboard sai số IM LẶNG
     · N3 đổi điều kiện "gói active"                  → cổng AI scan + giới hạn 20 món sai
     · N4 đổi cột CheckInAt / AssignmentStatuses      → Dashboard + quyền PT sai
     · N2 đổi MemberProfile.UserId / IsDeleted        → mọi CanAccessAsync sai
     · N1 đổi claim NameIdentifier                    → AuditService ghi UserId = null

   Chiều ngược lại — Minh sửa thì AI gãy:
     · đổi chữ ký IAuditService.LogAsync  → VỠ BUILD CẢ 4 NGƯỜI (8 service gọi vào)
```

---

## CHECKLIST DEBUG NHANH

| Triệu chứng | Xem ngay ở đâu |
|---|---|
| Tìm món không ra dù có trong DB | `FoodItemService.SearchAsync` → `HasFullFoodAccessAsync` — member chưa có gói chỉ thấy 20 món |
| Gõ "com" không ra "Cơm" | collation `Latin1_General_100_CI_AI` — DB thật có hỗ trợ collation này không |
| Quét ảnh trả 403 | `HasActivePackageAsync` — tài khoản test chưa có membership Active |
| Quét ảnh trả 502 INVALID_AI_RESPONSE | `%TEMP%/gemini_diag.log` (`GeminiService.WriteDiag`) — xem `finishReason` |
| Calo hiển thị gấp/chia 100 lần | `MealLogForm.confirmCart` — `quantity / 100` (UI gram ↔ API phần-100g) |
| Ghi 2 lần cùng buổi mà chỉ thấy 1 dòng | Đúng thiết kế — `CreateMealLogAsync` gộp theo (member, ngày, buổi) |
| Mục tiêu calo trả 404 | Đúng — `NO_TARGET`, hook `useMemberCalorieTarget` bắt và trả `null` |
| Dashboard số 0 hết | Kiểm `payments.PaidAt` có NULL không, và múi giờ: mốc tháng dùng `VnMonthStartUtc` |
| Audit log không có tên người | `users.IsDeleted = true` → `GetAuditLogsAsync` bỏ qua, trả `null` |
| Màn nutrition không refetch sau khi ghi | `useCreateMemberMealLog.onSuccess` — 3 key invalidate có khớp `memberId`/`logDate` không |
