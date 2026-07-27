# Tổng quan phần của Minh (N5) — GymMaster

**Người phụ trách:** Minh · git `Minhdicodedao` + `minhbao <minhbaoca@gmail.com>`
**Miền nghiệp vụ:** Dinh dưỡng & Calo · Dashboard & Audit Log · Trang giới thiệu
**Spec sở hữu:** 007 Nutrition-Calorie · 008 Dashboard-Audit · 009 Image Food Recognition (Gemini)
**Khối lượng:** 10 màn FE · 13 endpoint BE (phân công ghi 12 — xem §11) · 5 bảng DB · ~10.900 LOC
**Ngày viết:** 26/07/2026 · **Cập nhật:** 27/07/2026 — dựng từ source code thật, không phải từ tài liệu.

> Tài liệu này đi **từ lớn đến nhỏ**: toàn dự án → phần của Minh trong đó → tầng kiến trúc →
> thư mục → file → hàm → dòng dữ liệu. Mỗi mục đều trỏ tới file thật trong repo.

---

## MỤC LỤC

| § | Nội dung |
|---|---|
| 1 | Vị trí của Minh trong toàn dự án (bản đồ 5 người) |
| 2 | Kiến trúc hệ thống — đường đi của 1 request |
| 3 | Cây source code **Backend** của Minh |
| 4 | Cây source code **Frontend** của Minh |
| 5 | Database — 5 bảng thuộc Minh (ERD) |
| 6 | 13 endpoint — bảng chi tiết |
| 7 | 10 màn hình — bảng chi tiết |
| 8 | Sơ đồ luồng nghiệp vụ (5 sequence) |
| 9 | Luật nghiệp vụ trích từ code |
| 10 | Test đã có |
| 11 | Ranh giới sở hữu & khoảng trống |
| 12 | Tài liệu phải nộp (6 hình SDS + RDS/SDS mục II–III) |
| 13 | Bằng chứng git |

---

## 1. VỊ TRÍ CỦA MINH TRONG TOÀN DỰ ÁN

GymMaster = hệ thống quản lý phòng gym 1 chi nhánh, ~1000 hội viên, 4 role
(**Admin · Staff · PT · Member**). Chia **lát cắt dọc**: mỗi người ôm trọn một miền
nghiệp vụ *gồm cả frontend lẫn backend*.

```text
                        ┌──────────────────────────────────────────────┐
                        │            DỰ ÁN GymMaster (SWP391)          │
                        │   46 màn FE · 85 endpoint · 23 bảng · 5 người│
                        └──────────────────────────────────────────────┘
                                            │
        ┌───────────────┬───────────────┬───┴───────────┬───────────────────────┐
        │               │               │               │                       │
   ┌────▼────┐     ┌────▼────┐     ┌────▼────┐     ┌────▼────┐     ┏━━━━━━━━━━━━▼━━━━━━━━━━━┓
   │   N1    │     │   N2    │     │   N3    │     │   N4    │     ┃          N5            ┃
   │  Như    │     │Quang Anh│     │  Lộc    │     │  Đam    │     ┃        ★ MINH ★        ┃
   ├─────────┤     ├─────────┤     ├─────────┤     ├─────────┤     ┣━━━━━━━━━━━━━━━━━━━━━━━━┫
   │ Xác thực│     │ Hồ sơ   │     │ Gói tập │     │ Tập     │     ┃ Dinh dưỡng & Calo      ┃
   │ Tài khoản│    │ Hội viên│     │Membership│    │ luyện   │     ┃ AI quét ảnh món (Gemini)┃
   │ Quản trị│     │ & PT    │     │Thanh toán│    │Tiến độ  │     ┃ Dashboard & Audit Log  ┃
   │ tài khoản│    │         │     │  VNPay  │     │Check-in │     ┃ Trang giới thiệu       ┃
   ├─────────┤     ├─────────┤     ├─────────┤     ├─────────┤     ┣━━━━━━━━━━━━━━━━━━━━━━━━┫
   │ 12 màn  │     │  7 màn  │     │  8 màn  │     │  9 màn  │     ┃  10 màn                ┃
   │ 20 API  │     │ 20 API  │     │ 16 API  │     │ 17 API  │     ┃  13 API (list ghi 12)  ┃
   └─────────┘     └─────────┘     └─────────┘     └─────────┘     ┗━━━━━━━━━━━━━━━━━━━━━━━━┛
     spec 001        spec 002        spec 003        spec 004        ┃ spec 007 · 008 · 009  ┃
       002             006             010             005           ┗━━━━━━━━━━━━━━━━━━━━━━━┛
```

### 1.1 Phần của Minh nằm ở đâu trong chuỗi nghiệp vụ chính

```text
   Đăng ký ──► Tạo hội viên ──► Mua gói ──► Check-in ──► Phân công PT ──► Giáo án ──► Tiến độ
    (N1)          (N2)          (N3)        (N4)          (N4)            (N4)       (N2/N4)
      │             │             │           │             │              │           │
      └─────────────┴─────────────┴───────────┴─────────────┴──────────────┴───────────┘
                                            │
                              ┏━━━━━━━━━━━━━▼━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
                              ┃      NHẬT KÝ ĂN + TỔNG KẾT CALO         ┃  ← Minh
                              ┃  (bám vào Member, không sửa Member)     ┃
                              ┗━━━━━━━━━━━━━┳━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
                                            │  mọi hành động ghi AuditLog
                              ┏━━━━━━━━━━━━━▼━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
                              ┃   AUDIT LOG  ──►  ADMIN DASHBOARD       ┃  ← Minh
                              ┃  (đọc dữ liệu của cả 4 người còn lại)   ┃
                              ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
```

**Hai đặc điểm riêng của phần Minh (khác 4 người còn lại):**

1. `IAuditService` do Minh sở hữu nhưng **cả 5 người đều gọi** — mọi mutating action trong
   hệ thống ghi log qua nó. Sửa `AuditService` là ảnh hưởng toàn dự án.
2. `DashboardService` **chỉ đọc, không ghi** — nó tổng hợp `payments` (N3), `memberships` (N3),
   `check_ins` (N4), `trainer_assignments` (N4), `users` (N1). Người khác đổi schema là dashboard vỡ.

---

## 2. KIẾN TRÚC HỆ THỐNG — ĐƯỜNG ĐI CỦA 1 REQUEST

```text
┌─────────────────────────────────────────────────────────────────────────────────┐
│  TRÌNH DUYỆT                                                                    │
│  Next.js 16 (App Router) + React 19 + TypeScript + TanStack Query + Tailwind     │
│                                                                                 │
│   app/(member)/member/nutrition/meal-journal/page.tsx      ← route mỏng          │
│              │ render                                                           │
│              ▼                                                                  │
│   features/member-nutrition/components/MealJournalWorkspace.tsx   ← UI           │
│              │ hook                                                             │
│              ▼                                                                  │
│   features/member-nutrition/api/member-nutrition.queries.ts   ← useQuery/Mutation│
│              │ gọi                                                              │
│              ▼                                                                  │
│   features/member-nutrition/api/member-nutrition.api.ts       ← fetch + DTO      │
│              │                                                                  │
│              ▼                                                                  │
│   lib/api/http-client.ts  (apiRequest)  ── dùng chung cả 5 người                │
└──────────────┬──────────────────────────────────────────────────────────────────┘
               │  HTTPS + Bearer JWT
               ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  ASP.NET Core 10 Web API  (Google Cloud Run — asia-southeast1)                   │
│                                                                                 │
│   Program.cs → UseAuthentication → UseAuthorization ([Authorize(Roles=…)])       │
│              │                                                                  │
│              ▼                                                                  │
│   Features/Nutrition/MealLogsController.cs         ← controller MỎNG (chỉ điều phối)│
│              │  ToActionResult(result)                                          │
│              ▼                                                                  │
│   Features/Nutrition/NutritionService.cs           ← TOÀN BỘ nghiệp vụ           │
│              ├─ CanAccessAsync()   → kiểm quyền theo dữ liệu (ownership)        │
│              ├─ validate           → ServiceResult.Failure(code, msg, status)   │
│              ├─ DbContext          → EF Core LINQ, KHÔNG có tầng Repository     │
│              └─ IAuditService.LogAsync()  → ghi audit_logs                      │
│              │                                                                  │
│              ▼                                                                  │
│   Common/ServiceResult<T>  →  Common/ApiResponse<T> { success, data, error, meta }│
└──────────────┬──────────────────────────────────────────────────────────────────┘
               │  EF Core 10
               ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  SQL Server (Cloud SQL `gymmaster-sql-sg`)  —  23 bảng, Minh sở hữu 5            │
│  food_items · meal_logs · meal_log_items · calorie_targets · audit_logs          │
└─────────────────────────────────────────────────────────────────────────────────┘

   Riêng luồng AI thì rẽ thêm 1 nhánh ra ngoài:
   FoodScanService ──► Infrastructure/GeminiService ──HTTPS──► Google Generative Language API
                                                               (model gemini-2.5-flash)
```

**Kiểu kiến trúc:** Vertical Slice (`Features/<Tên>/` tự chứa controller + interface + service + DTO).
Không có tầng Repository — service gọi thẳng `DbContext` (quyết định D-002 toàn dự án).

---

## 3. CÂY SOURCE CODE — BACKEND CỦA MINH

```text
backend/GymMaster.API/
│
├── Features/Nutrition/                          ★ 12 file · 1.326 LOC · spec 007 + 009
│   │
│   ├── ── Kho món ăn (spec 007) ───────────────────────────────────────────────
│   ├── FoodItemsController.cs        41   route "api/v1/food-items" — 2 action
│   ├── IFoodItemService.cs           17   hợp đồng: SearchAsync · AddAsync
│   ├── FoodItemService.cs           196   ⚑ tìm món accent-insensitive (collation
│   │                                       Latin1_General_100_CI_AI) · giới hạn 20 món
│   │                                       cho member chưa có gói · Find-or-Create khi
│   │                                       trùng tên (trả 200 thay vì 409)
│   │
│   ├── ── Nhật ký ăn + calo (spec 007) ────────────────────────────────────────
│   ├── MealLogsController.cs         38   route "api/v1/meal-logs" — POST + GET
│   ├── MemberNutritionController.cs  59   route "api/v1/members/{id}/…" — 4 action calo
│   ├── INutritionService.cs          40   hợp đồng 6 method
│   ├── NutritionService.cs          492   ⚑ LỚN NHẤT — mục tiêu calo, ghi bữa (gộp trùng
│   │                                       món), tổng kết ngày, lịch sử 7 ngày,
│   │                                       CanAccessAsync 4 role
│   ├── NutritionDtos.cs              77   record request/response của feature
│   │
│   ├── ── Quét ảnh AI (spec 009) ──────────────────────────────────────────────
│   ├── FoodScanController.cs         54   route "api/v1/foods" — 3 action, [Member] only
│   ├── IFoodScanService.cs           23   hợp đồng: Scan · Estimate · Confirm
│   ├── FoodScanService.cs           240   ⚑ gác cổng "phải có gói active" · validate ảnh
│   │                                       ≤5MB JPG/PNG · đối chiếu món AI với DB ·
│   │                                       lưu món mới Source="AI"
│   └── FoodScanDtos.cs               49   FoodScanItem · FoodNutritionDraft · ScannedFood
│
├── Features/Dashboard/                          ★ 7 file · 444 LOC · spec 008
│   ├── DashboardController.cs        29   route "api/v1/dashboard" — [Authorize(Admin)]
│   ├── AuditLogsController.cs        35   route "api/v1/audit-logs" — [Authorize(Admin)]
│   ├── IDashboardService.cs          21   hợp đồng: GetSummaryAsync · GetAuditLogsAsync
│   ├── DashboardService.cs          263   ⚑ 11 truy vấn tổng hợp trong 1 endpoint:
│   │                                       doanh thu · membership active/expired ·
│   │                                       check-in hôm nay · thanh toán chờ ·
│   │                                       doanh thu 6 tháng · top 10 hết hạn ·
│   │                                       tải vận hành · giờ cao điểm (UTC+7)
│   ├── DashboardDtos.cs              43   DashboardSummaryResponse (15 field) + 3 item DTO
│   ├── IAuditService.cs               6   ◄── 4 NGƯỜI CÒN LẠI CŨNG GỌI INTERFACE NÀY
│   └── AuditService.cs               47   ghi audit_logs · actor lấy từ JWT claim
│
├── Infrastructure/
│   └── GeminiService.cs             401   ★ client Gemini Vision: structured output
│                                          (responseSchema) · thinkingBudget=0 ·
│                                          ghép nhiều part text · gỡ ```json fence ·
│                                          map 5 mã lỗi AI
│   └── IFoodImageAnalyzer.cs               (hợp đồng để mock trong unit test)
│
├── Options/
│   └── GeminiOptions.cs              25   ★ BaseUrl · Model=gemini-2.5-flash ·
│                                          ApiKey (User Secrets) · MaxImageBytes=5MB ·
│                                          TimeoutSeconds=20
│
├── Entities/                                    ★ 5 entity thuộc Minh
│   ├── FoodItem.cs                        Name(unique) · Unit · CaloriesPerUnit ·
│   │                                      Protein/Carb/Fat · ServingSize · Source · IsActive
│   ├── MealLog.cs                         MemberId · LogDate · MealType · List<Items>
│   ├── MealLogItem.cs                     MealLogId · FoodItemId · Quantity · Calories
│   ├── CalorieTarget.cs                   MemberId · EffectiveDate · DailyCalories + macro
│   ├── AuditLog.cs                        UserId? · Action · Entity · EntityId · Metadata(json)
│   └── NutritionEnums.cs                  enum MealType : byte {Breakfast=1…Snack=4}
│
├── Data/GymMasterDbContext.cs              (dùng chung) — cấu hình 5 bảng của Minh ở dòng 325–378
├── Common/                                 (dùng chung 5 người) ServiceResult · ApiResponse ·
│                                           PagedResult · ApiControllerBase · AppClock
└── Program.cs                              (dùng chung) — DI: AddHttpClient<GeminiService>,
                                            AddScoped<INutritionService,…> v.v.
```

**Tổng backend Minh: ~2.196 LOC** (1.326 Nutrition + 444 Dashboard + 401 Gemini + 25 Options).

### 3.1 Chú giải chi tiết 19 file C# trong `Features` của Minh

Quy tắc đọc tên file:

```text
Controller  = cửa HTTP: nhận request, đọc route/query/body, gọi service, đổi kết quả thành HTTP response.
I...Service = hợp đồng: khai báo service phải cung cấp những hàm nào; không chứa nghiệp vụ.
Service     = nơi xử lý nghiệp vụ thật: quyền, validation, tính toán, truy vấn và ghi database.
Dtos        = hình dạng dữ liệu đi qua API; không truy vấn database và không chứa nghiệp vụ.
```

#### `Features/Dashboard/` — 7 file

| File | Được gọi ở đâu | Trách nhiệm chính | Khi cần debug |
|---|---|---|---|
| `DashboardController.cs` | `GET /api/v1/dashboard/summary` từ màn Admin Dashboard | Chặn role khác Admin; nhận `from/to`; gọi `IDashboardService.GetSummaryAsync`; chuyển `ServiceResult` thành HTTP response | API Dashboard không vào service, sai route, 401/403 |
| `AuditLogsController.cs` | `GET /api/v1/audit-logs` từ màn Audit Log | Chặn role khác Admin; nhận `userId/action/from/to/search/page/pageSize`; gọi `GetAuditLogsAsync` | Filter từ frontend không tới backend, phân trang sai đầu vào |
| `IDashboardService.cs` | Hai controller phía trên gọi | Hợp đồng gồm `GetSummaryAsync` và `GetAuditLogsAsync`; giúp controller không phụ thuộc trực tiếp class triển khai | Lỗi DI hoặc đổi chữ ký hàm Dashboard |
| `DashboardService.cs` | Được inject qua `IDashboardService` | Nghiệp vụ đọc: tính doanh thu, membership active/expired, check-in, tải cơ sở, doanh thu 6 tháng, giờ cao điểm; đồng thời lọc/phân trang Audit Log và ghép tên user | Số Dashboard sai, timezone sai, filter Audit Log sai |
| `DashboardDtos.cs` | Service tạo; controller trả; frontend nhận | Định nghĩa `DashboardSummaryResponse`, `CheckInByDayItem`, `RevenueByMonthItem`, `ExpiredMembershipItem`, `AuditLogResponse` | Backend có dữ liệu nhưng frontend thiếu/sai field |
| `IAuditService.cs` | Các service có thao tác ghi của cả 5 thành viên gọi | Hợp đồng một hàm `LogAsync(action, entity, entityId, metadata)`; metadata không được chứa dữ liệu nhạy cảm | Một service khác không gọi được Audit hoặc vỡ build do đổi chữ ký |
| `AuditService.cs` | `FoodItemService`, `FoodScanService`, `NutritionService` và service của người khác | Lấy actor từ claim JWT; serialize metadata thành JSON; tạo `AuditLog`; lưu `audit_logs` | Audit không sinh bản ghi, `UserId=null`, metadata sai |

Điểm dễ nhầm: `AuditService` chịu trách nhiệm **ghi** log, còn
`DashboardService.GetAuditLogsAsync` chịu trách nhiệm **đọc/lọc** log.

#### `Features/Nutrition/` — 12 file

| File | Được gọi ở đâu | Trách nhiệm chính | Khi cần debug |
|---|---|---|---|
| `FoodItemsController.cs` | `GET /api/v1/food-items`, `POST /api/v1/food-items` | Nhận tìm kiếm/phân trang hoặc body tạo món; áp quyền; gọi `IFoodItemService` | Route kho món, model binding, 401/403 |
| `IFoodItemService.cs` | `FoodItemsController` gọi | Hợp đồng `SearchAsync` và `AddAsync` | Lỗi DI hoặc đổi chữ ký tìm/tạo món |
| `FoodItemService.cs` | Được inject qua `IFoodItemService` | Tìm món active không phân biệt dấu; member free chỉ thấy 20 món; validate món tự nhập; find-or-create khi trùng; tạo món và ghi `CREATE_FOOD` | Tìm không ra món, giới hạn free sai, tạo trùng, calo/macro không hợp lệ |
| `FoodScanController.cs` | Ba endpoint dưới `/api/v1/foods`: `scan-image`, `estimate-nutrition`, `confirm-ai-food` | Chỉ cho Member; nhận file/body; gọi `IFoodScanService`; không tự gọi Gemini | Upload không vào service, sai content type, 401/403 |
| `IFoodScanService.cs` | `FoodScanController` gọi | Hợp đồng ba thao tác AI: quét ảnh, ước lượng theo tên, xác nhận lưu món | Lỗi DI hoặc đổi chữ ký luồng AI |
| `FoodScanService.cs` | Được inject qua `IFoodScanService` | Kiểm gói active; validate JPG/PNG ≤5MB; gọi `IFoodImageAnalyzer`; đối chiếu kết quả AI với `food_items`; trả draft hoặc lưu món `Source="AI"`; ghi `CONFIRM_AI_FOOD` | 403 membership, 422 file, 502 Gemini, AI nhận đúng nhưng match DB sai |
| `FoodScanDtos.cs` | `FoodScanController/Service` và frontend AI | Dữ liệu riêng của AI: `ScannedFood`, `FoodNutritionDraft`, `FoodScanItem`, `FoodScanResponse`, request estimate/confirm | Frontend không phân biệt món DB và món cần xác nhận |
| `MealLogsController.cs` | `POST/GET /api/v1/meal-logs` | Nhận request ghi bữa hoặc query `memberId/date`; gọi `INutritionService` | Không tạo/lấy được bữa dù service đúng |
| `MemberNutritionController.cs` | Bốn endpoint dưới `/api/v1/members/{id}` | Điều phối đặt/lấy mục tiêu, tổng kết một ngày và lịch sử calo | Sai member id, date/from/to không bind, sai endpoint |
| `INutritionService.cs` | Hai controller dinh dưỡng gọi | Hợp đồng 6 hàm: set/get target, create/get meal logs, summary, history | Lỗi DI hoặc thay đổi contract dinh dưỡng |
| `NutritionService.cs` | Được inject qua `INutritionService` | Nghiệp vụ chính: kiểm tra Member/Admin/Staff/PT; upsert mục tiêu; tạo/gộp bữa và món; tính calo/macro; lấy tổng kết/lịch sử; ghi `SET_CALORIE_TARGET` và `CREATE_MEAL_LOG` | 403 ownership, gộp bữa sai, calo/macro sai, mục tiêu theo ngày sai |
| `NutritionDtos.cs` | Controller nhận request; service tạo response; frontend dùng | DTO món ăn, mục tiêu, item trong bữa, meal log, tổng kết calo/macro | Request bind sai hoặc response thiếu field |

#### Tra nhanh: lỗi nào mở file nào trước

| Triệu chứng | File mở đầu tiên | File tiếp theo |
|---|---|---|
| Endpoint trả 404 route hoặc body/query không nhận | `*Controller.cs` tương ứng | DTO request tương ứng |
| Trả 401/403 | Attribute `[Authorize]` ở controller | `CanAccessAsync` / `HasActivePackageAsync` trong service |
| Trả 400/422 | Service tương ứng | DTO request + entity liên quan |
| Số Dashboard sai | `DashboardService.cs` | entity/schema của bảng được đọc |
| Tìm/tạo món sai | `FoodItemService.cs` | `FoodItem.cs`, mapping `GymMasterDbContext` |
| AI lỗi | `FoodScanService.cs` | `GeminiService.cs`, `GeminiOptions.cs` |
| Nhật ký hoặc tổng calo sai | `NutritionService.cs` | `MealLog.cs`, `MealLogItem.cs`, `FoodItem.cs`, `CalorieTarget.cs` |
| Audit không có bản ghi | Nơi gọi `IAuditService.LogAsync` | `AuditService.cs`, `AuditLog.cs` |

---

## 4. CÂY SOURCE CODE — FRONTEND CỦA MINH

```text
GymMaster-frontend/src/
│
├── app/                                         ← route (Next.js App Router), file mỏng
│   ├── page.tsx                            5   ★ redirect("/welcome")
│   ├── (auth)/welcome/page.tsx           541   ★ trang chào — 3 slide onboarding +
│   │                                            4 tài khoản demo + feature grid
│   ├── (auth)/about/page.tsx             400   ★ giới thiệu — 3 section: hero ·
│   │                                            workspaces · innovations
│   ├── (admin)/admin/dashboard/page.tsx   13   ★ → AdminPageFrame + AdminDashboardContent
│   ├── (admin)/admin/audit-logs/page.tsx  13   ★ → AdminPageFrame + AuditLogsContent
│   ├── (member)/member/dashboard/page.tsx 25   ★ Guard + MembershipGate + MemberDashboardContent
│   ├── (member)/member/nutrition/meal-journal/page.tsx  25  ★ MembershipGate allowFreeTier
│   ├── (member)/member/nutrition/summary/page.tsx       25  ★ MembershipGate
│   ├── (staff)/staff/dashboard/page.tsx   13   ⚠ màn của Minh nhưng render component
│   └── (pt)/pt/dashboard/page.tsx         13   ⚠ của N3/N4 — xem §11
│
├── features/member-nutrition/                   ★ 21 file · 6.229 LOC — feature NẶNG NHẤT của Minh
│   │
│   ├── api/
│   │   ├── member-nutrition.api.ts       322   gọi 10 endpoint + fallback Open Food Facts
│   │   │                                       + 3 hàm AI (scan · estimate · confirm)
│   │   ├── member-nutrition.queries.ts   194   11 hook TanStack Query, key factory,
│   │   │                                       invalidate sau khi ghi bữa
│   │   └── nutrition-api.ts              193   (lớp gọi phụ trợ)
│   │
│   ├── components/                             ← 12 component, 5.189 LOC
│   │   ├── MealJournalWorkspace.tsx      234   khung màn Nhật ký ăn: chọn ngày ·
│   │   │                                       list ↔ add · deep-link ?view=add&type=lunch
│   │   ├── MealLogForm.tsx               597   form ghi bữa: chọn món → nhập GRAM →
│   │   │                                       tính calo tại chỗ → submit
│   │   ├── FoodSearchPanel.tsx           963   ⚑ TO NHẤT — tìm món trong DB, tạo món
│   │   │                                       custom, ước lượng macro bằng AI,
│   │   │                                       tra online (ENABLE_ONLINE_SEARCH=false)
│   │   ├── AiFoodScanCard.tsx            195   ★ chụp/tải ảnh → hiện danh sách món AI
│   │   │                                       nhận diện → "Chọn" đưa sang form
│   │   ├── MealLogList.tsx               230   danh sách bữa trong ngày theo 4 buổi
│   │   ├── MealDetailSheet.tsx           193   sheet chi tiết 1 bữa
│   │   ├── NutritionSummaryCard.tsx      395   thẻ tổng kết calo + macro
│   │   ├── CalorieSummaryWorkspace.tsx   761   màn Tổng kết calo: vòng calo, macro,
│   │   │                                       biểu đồ 7 ngày, đặt mục tiêu
│   │   ├── TdeeCalculator.tsx            639   tính TDEE (Mifflin-St Jeor) → gợi ý mục tiêu
│   │   ├── BmiCalculator.tsx             369   tính BMI + phân loại
│   │   ├── WaterTrackerCard.tsx          270   theo dõi nước (local-only, không có API)
│   │   └── MemberDashboardContent.tsx    343   nội dung Bảng điều khiển hội viên
│   │
│   ├── schemas/     meal-log.schema.ts 24 · custom-food.schemas.ts 36   (zod)
│   ├── types/       member-nutrition.types.ts 98
│   ├── utils/       nutrition-formatters.ts 41  (getTodayDate, format calo/macro)
│   └── data/        nutrition-assets.ts 44 · nutrition-fallback-data.ts 88
│
├── features/admin-dashboard/                    ★ 10 file · 1.464 LOC
│   ├── api/admin-dashboard.api.ts         57   getDashboardSummary · getAuditLogs
│   ├── api/admin-dashboard.queries.ts     40   useDashboardSummary · useAuditLogs
│   ├── components/AdminDashboardContent.tsx   226  ★ 4 metric card + BarChart recharts
│   │                                               + format VND rút gọn (tỷ/triệu)
│   ├── components/DashboardMetricCard.tsx     108  thẻ số liệu (có skeleton loading)
│   ├── components/AuditLogsContent.tsx        424  ★ màn nhật ký audit: filter + phân trang
│   ├── components/AuditLogTable.tsx           316  bảng log
│   ├── components/AuditLogFilters.tsx         188  lọc theo user/action/khoảng ngày/từ khoá
│   ├── components/AdminPageFrame.tsx           24  khung trang admin (dùng cho 2 màn)
│   ├── constants/admin-routes.ts               22
│   └── types/admin-dashboard.types.ts          59  phản chiếu DTO backend
│
└── tests/
    ├── member-nutrition/       6 file test (form, list, summary, TDEE, AI estimate…)
    └── admin-dashboard/        3 file test (metric card, audit table, page frame)
```

**Tổng frontend Minh: ~8.740 LOC** (6.229 nutrition + 1.464 dashboard + 1.047 trang).
**TỔNG CỘNG cả BE + FE ≈ 10.900 LOC.**

---

## 5. DATABASE — 5 BẢNG THUỘC MINH

```text
                     ┌──────────────────┐
                     │  member_profiles │  ◄── N2 sở hữu (Minh CHỈ ĐỌC, không sửa)
                     │  Id (PK)         │
                     └────┬────────┬────┘
                          │        │
        ┌─────────────────┘        └──────────────────┐
        │ 1                                         1 │
        │                                             │
        │ N                                         N │
┌───────▼─────────────────┐              ┌────────────▼──────────────┐
│  calorie_targets        │★             │  meal_logs                │★
├─────────────────────────┤              ├───────────────────────────┤
│ Id           bigint PK  │              │ Id            bigint PK   │
│ MemberId     bigint FK  │              │ MemberId      bigint FK   │
│ EffectiveDate date      │              │ LogDate       date        │
│ DailyCalories dec(8,2)  │              │ MealType      tinyint     │  1=Sáng 2=Trưa
│ ProteinG     dec(8,2)?  │              │ CreatedAt     datetime2   │  3=Tối 4=Phụ
│ CarbG        dec(8,2)?  │              └────────────┬──────────────┘
│ FatG         dec(8,2)?  │                           │ 1
│ CreatedAt    datetime2  │                           │
├─────────────────────────┤                           │ N
│ UNIQUE(MemberId,        │              ┌────────────▼──────────────┐
│        EffectiveDate)   │              │  meal_log_items           │★
└─────────────────────────┘              ├───────────────────────────┤
                                         │ Id          bigint PK     │
   ┌─────────────────────────┐           │ MealLogId   bigint FK     │
   │  audit_logs             │★          │ FoodItemId  bigint FK ────┼──┐
   ├─────────────────────────┤           │ Quantity    dec(8,2)      │  │
   │ Id        bigint PK     │           │ Calories    dec(8,2)      │  │
   │ UserId    bigint? FK    │           └───────────────────────────┘  │
   │ Action    nvarchar      │              ⚠ CHỈ lưu Quantity+Calories │
   │ Entity    nvarchar      │                macro suy ra từ FoodItem  │
   │ EntityId  bigint        │                                       N │
   │ Metadata  nvarchar json │                                         │ 1
   │ CreatedAt datetime2     │                            ┌────────────▼──────────────┐
   └─────────────────────────┘                            │  food_items               │★
     ▲ ghi bởi CẢ 5 NGƯỜI                                 ├───────────────────────────┤
     │ đọc bởi DashboardService (Minh)                    │ Id             bigint PK   │
                                                          │ Name           nvarchar(150)│ UNIQUE
                                                          │ Unit           nvarchar(30) │
                                                          │ CaloriesPerUnit dec(8,2)   │
                                                          │ ProteinG/CarbG/FatG dec(8,2)?│
                                                          │ ServingSize    dec(8,2)=100 │
                                                          │ Source         nvarchar(20) │ "Admin"|"AI"
                                                          │ IsActive       bit          │
                                                          │ CreatedAt      datetime2    │
                                                          └───────────────────────────┘
```

**Index đã đặt** (`Data/GymMasterDbContext.cs:325–378`):

| Bảng | Index | Vì sao |
|---|---|---|
| `food_items` | `UNIQUE(Name)` | chặn trùng món; là lý do phải Find-or-Create |
| `meal_logs` | `(MemberId, LogDate)` | truy vấn chính: nhật ký của 1 người trong 1 ngày |
| `calorie_targets` | `UNIQUE(MemberId, EffectiveDate)` | 1 người 1 ngày chỉ 1 mục tiêu → cho phép upsert |

**Bảng của người khác mà Minh CHỈ ĐỌC:** `payments`·`memberships` (N3) · `check_ins`·`trainer_assignments` (N4) ·
`users`·`member_profiles`·`trainer_profiles` (N1/N2). Dashboard đọc hết 7 bảng này.

---

## 6. 13 ENDPOINT — BẢNG CHI TIẾT

### 6.1 Nutrition — kho món ăn

| # | Method + Route | Quyền | Service method | FR | Ghi chú nghiệp vụ |
|---|---|---|---|---|---|
| 1 | `GET /api/v1/food-items?query=&page=&pageSize=` | Authenticated | `FoodItemService.SearchAsync` | FR-FOOD-01 | Tìm **không phân biệt dấu/hoa thường**. Member chưa có gói chỉ thấy **20 món đầu A→Z** |
| 2 | `POST /api/v1/food-items` | Member/Admin/Staff | `FoodItemService.AddAsync` | FR-FOOD-02 | Trùng tên → trả **200 + món cũ** (Find-or-Create), không 409 |

### 6.2 Nutrition — nhật ký bữa ăn

| # | Method + Route | Quyền | Service method | FR | Ghi chú |
|---|---|---|---|---|---|
| 3 | `POST /api/v1/meal-logs` | Authenticated + ownership | `NutritionService.CreateMealLogAsync` | FR-MEAL-01/02/03 | Cùng (member, ngày, buổi) → **gộp vào log cũ**; cùng món → cộng dồn quantity |
| 4 | `GET /api/v1/meal-logs?memberId=&date=` | Authenticated + ownership | `NutritionService.GetMealLogsAsync` | FR-MEAL-01 | Mặc định `date` = hôm nay (giờ VN) |

### 6.3 Nutrition — mục tiêu & tổng kết calo

| # | Method + Route | Quyền | Service method | FR | Ghi chú |
|---|---|---|---|---|---|
| 5 | `POST /api/v1/members/{id}/calorie-target` | Authenticated + ownership | `SetTargetAsync` | FR-CAL-TGT-01 | **Upsert** theo `EffectiveDate`: mới → 201, có rồi → 200 |
| 6 | `GET /api/v1/members/{id}/calorie-target` | Authenticated + ownership | `GetTargetAsync` | FR-CAL-TGT-02 | Lấy mục tiêu **hiệu lực gần nhất ≤ hôm nay**; chưa có → 404 `NO_TARGET` |
| 7 | `GET /api/v1/members/{id}/calorie-summary?date=` | Authenticated + ownership | `GetSummaryAsync` | FR-CAL-01 | Trả consumed/target/remaining cho **calo + 3 macro** |
| 8 | `GET /api/v1/members/{id}/calorie-history?from=&to=` | Authenticated + ownership | `GetHistoryAsync` | FR-CAL-01 | Mặc định **7 ngày gần nhất**; ngày không ăn vẫn trả 0 (không hụt điểm biểu đồ) |

### 6.4 Nutrition — AI quét ảnh (spec 009)

| # | Method + Route | Quyền | Service method | FR | Ghi chú |
|---|---|---|---|---|---|
| 9 | `POST /api/v1/foods/scan-image` (multipart) | **Member + có gói active** | `FoodScanService.ScanImageAsync` | FR-IMG-01/02 | Ảnh JPG/PNG ≤5MB. Gemini tách **từng thành phần** + ước lượng gram |
| 10 | `POST /api/v1/foods/estimate-nutrition` | **Member + có gói active** | `EstimateNutritionAsync` | FR-IMG-04 | Nhập **tên** → AI ước lượng macro/100g, **chưa lưu DB** |
| 11 | `POST /api/v1/foods/confirm-ai-food` | **Member + có gói active** | `ConfirmAiFoodAsync` | FR-IMG-03 | Lưu món `Source="AI"`, `Unit="g"`, `ServingSize=100`. Trùng tên → trả món cũ |

### 6.5 Dashboard & Audit (spec 008)

| # | Method + Route | Quyền | Service method | FR | Ghi chú |
|---|---|---|---|---|---|
| 12 | `GET /api/v1/dashboard/summary?from=&to=` | **Admin** | `DashboardService.GetSummaryAsync` | FR-DASH-01/02/03 | **11 truy vấn** gộp 1 response 15 field |
| 13 | `GET /api/v1/audit-logs?userId=&action=&from=&to=&search=&page=&pageSize=` | **Admin** | `GetAuditLogsAsync` | FR-AUD-02 | Phân trang, `pageSize` kẹp 1–100, mới nhất trước |

### 6.6 Mã lỗi feature này định nghĩa

```text
NUTRITION                          AI SCAN                        DASHBOARD
─────────────────────────────      ────────────────────────       ────────────────────
NOT_FOUND          404             MEMBERSHIP_REQUIRED   403      INVALID_RANGE     422
FORBIDDEN          403             INVALID_FILE          422
INVALID_TARGET     422             VALIDATION_ERROR      400
INVALID_QUANTITY   422             FOOD_NOT_RECOGNIZED   502
VALIDATION_ERROR   422             RECOGNITION_TIMEOUT   502
FOOD_NOT_FOUND     404             RECOGNITION_UNAVAILABLE 502
NO_TARGET          404             INVALID_AI_RESPONSE   502
                                   AI_NOT_CONFIGURED     502
```

---

## 7. 10 MÀN HÌNH — BẢNG CHI TIẾT

| # | Route | Actor | Component gốc | Gọi endpoint |
|---|---|---|---|---|
| 1 | `/member/nutrition/meal-journal` | Member | `MealJournalWorkspace` (+ MealLogForm, FoodSearchPanel, AiFoodScanCard, MealLogList, TdeeCalculator) | 1,2,3,4,7,9,10,11 |
| 2 | `/member/nutrition/summary` | Member | `CalorieSummaryWorkspace` (+ NutritionSummaryCard) | 4,5,6,7,8 |
| 3 | `/member/dashboard` | Member | `MemberDashboardContent` (+ BmiCalculator, WaterTrackerCard) | 6,7 |
| 4 | `/admin/dashboard` | Admin | `AdminDashboardContent` (+ DashboardMetricCard, recharts BarChart) | 12 |
| 5 | `/admin/audit-logs` | Admin | `AuditLogsContent` (+ AuditLogTable, AuditLogFilters) | 13 |
| 6 | `/staff/dashboard` | Staff | `StaffDashboard` ⚠ nằm ở `features/staff-front-desk/` (N3) | — |
| 7 | `/pt/dashboard` | PT | `PtDashboardContent` ⚠ nằm ở `features/pt-dashboard/` (N4) | — |
| 8 | `/` | — | `redirect("/welcome")` | không |
| 9 | `/welcome` | Anonymous | trang tĩnh 541 dòng — 3 slide onboarding + 4 tài khoản demo | không |
| 10 | `/about` | Anonymous | trang tĩnh 400 dòng — hero · workspaces · innovations | không |

### 7.1 Sơ đồ điều hướng phần Minh

```text
   /  ──redirect──►  /welcome  ──►  /login (N1)
                        │
                        └──► /about

   [MEMBER đăng nhập]                          [ADMIN đăng nhập]
        │                                            │
        ▼                                            ▼
   /member/dashboard ★                         /admin/dashboard ★
        │  MembershipGate (bắt buộc có gói)          │  4 metric card + biểu đồ 6 tháng
        │                                            │
        ├──► /member/nutrition/meal-journal ★        └──► /admin/audit-logs ★
        │      MembershipGate allowFreeTier              filter + phân trang
        │      (chưa có gói VẪN VÀO được,
        │       nhưng chỉ thấy 20 món & không quét AI)
        │
        └──► /member/nutrition/summary ★
               MembershipGate (bắt buộc có gói)
```

---

## 8. SƠ ĐỒ LUỒNG NGHIỆP VỤ

### 8.1 Ghi một bữa ăn (luồng chính, `POST /meal-logs`)

```text
 Member        MealLogForm      NutritionService                DB
   │                │                  │                        │
   │ chọn món+gram  │                  │                        │
   ├───────────────►│                  │                        │
   │                │ POST /meal-logs  │                        │
   │                ├─────────────────►│                        │
   │                │                  │ FindMemberAsync        │
   │                │                  ├───────────────────────►│
   │                │                  │◄─── member profile ────┤
   │                │                  │                        │
   │                │                  │ CanAccessAsync(4 role) │
   │                │                  │  Admin/Staff → OK      │
   │                │                  │  Member → userId khớp? │
   │                │                  │  PT → có assignment    │
   │                │                  │        Active không?   │
   │                │                  │───┐                    │
   │                │  403 FORBIDDEN   │◄──┘ sai                │
   │                │◄─────────────────┤                        │
   │                │                  │ validate Items ≠ rỗng, │
   │                │                  │ Quantity > 0, MealType │
   │                │                  │  hợp lệ → 422 nếu sai  │
   │                │                  │                        │
   │                │                  │ GroupBy FoodItemId     │  ← gộp món trùng
   │                │                  │ nạp FoodItems IsActive │
   │                │                  ├───────────────────────►│
   │                │                  │  thiếu → 404 FOOD_NOT_FOUND
   │                │                  │                        │
   │                │                  │ tìm MealLog cùng       │
   │                │                  │ (member, ngày, buổi)   │
   │                │                  ├───────────────────────►│
   │                │                  │  CÓ  → cộng dồn item   │
   │                │                  │  KHÔNG → tạo log mới   │
   │                │                  │                        │
   │                │                  │ Calories = CaloriesPerUnit × Quantity
   │                │                  │ SaveChanges            │
   │                │                  ├───────────────────────►│
   │                │                  │ AuditService.LogAsync  │
   │                │                  │   "CREATE_MEAL_LOG"    │
   │                │                  ├───────────────────────►│ audit_logs
   │                │  201 MealLog     │                        │
   │                │◄─────────────────┤                        │
   │  invalidate mealLogs + summary → 2 hook tự refetch          │
```

### 8.2 Quét ảnh món ăn bằng AI (`POST /foods/scan-image`)

```text
 Member    AiFoodScanCard   FoodScanController   FoodScanService   GeminiService   Gemini API
   │            │                  │                    │                │             │
   │ chọn ảnh   │                  │                    │                │             │
   ├───────────►│ check ≤5MB       │                    │                │             │
   │            │ multipart POST   │                    │                │             │
   │            ├─────────────────►│ [Authorize(Member)]│                │             │
   │            │                  ├───────────────────►│                │             │
   │            │                  │                    │ HasActivePackageAsync         │
   │            │                  │                    │  Membership Active & EndDate≥today
   │            │  403 MEMBERSHIP_REQUIRED  ◄───────────┤  không có → CHẶN              │
   │            │                  │                    │                │             │
   │            │                  │                    │ validate JPG/PNG ≤5MB         │
   │            │  422 INVALID_FILE ◄───────────────────┤  sai → CHẶN    │             │
   │            │                  │                    │                │             │
   │            │                  │                    │ DetectFoodsAsync(bytes)       │
   │            │                  │                    ├───────────────►│             │
   │            │                  │                    │                │ POST :generateContent
   │            │                  │                    │                │  responseSchema=ARRAY
   │            │                  │                    │                │  thinkingBudget=0
   │            │                  │                    │                ├────────────►│
   │            │                  │                    │                │◄─ JSON ─────┤
   │            │                  │                    │                │ ghép mọi part.text
   │            │                  │                    │                │ gỡ ```json fence
   │            │                  │                    │◄─ List<Detected>│            │
   │            │                  │                    │                              │
   │            │                  │                    │ VỚI MỖI MÓN:                  │
   │            │                  │                    │  FindDatabaseMatchAsync       │
   │            │                  │                    │   exact (CI_AI) → Contains    │
   │            │                  │                    │  ┌─ CÓ  → source="Database",  │
   │            │                  │                    │  │        requiresConfirm=false│
   │            │                  │                    │  └─ KHÔNG → source="AI",      │
   │            │                  │                    │            draft + requiresConfirm=true
   │            │  200 { items[] } │◄───────────────────┤                              │
   │◄───────────┤ hiện danh sách + confidence + gram ước lượng                          │
   │            │                                                                       │
   │ bấm "Chọn" │ POST /foods/confirm-ai-food  → lưu FoodItem Source="AI" → 201        │
   │            │ rồi đưa món sang MealLogForm với gram AI đã ước lượng                 │
```

### 8.3 Tổng kết calo trong ngày (`GET /calorie-summary`)

```text
   GET /api/v1/members/{id}/calorie-summary?date=2026-07-26
        │
        ├─► FindMember + CanAccess                       (404 / 403)
        │
        ├─► meal_logs ⋈ meal_log_items ⋈ food_items      lọc MemberId + LogDate
        │      SELECT Calories, Quantity, ProteinG, CarbG, FatG
        │
        ├─► consumed        = Σ Calories
        │   consumedProtein = Σ (FoodItem.ProteinG × Quantity)    ← macro KHÔNG lưu ở
        │   consumedCarb    = Σ (FoodItem.CarbG    × Quantity)      meal_log_items,
        │   consumedFat     = Σ (FoodItem.FatG     × Quantity)      luôn suy ra từ food_items
        │
        ├─► calorie_targets: EffectiveDate ≤ ngày, lấy MỚI NHẤT   (có thể null)
        │
        └─► CalorieSummaryResponse {
              date, consumed, target, remaining = target − consumed,
              consumedProtein/Carb/Fat, targetProtein/Carb/Fat,
              remainingProtein/Carb/Fat            ← null hết nếu chưa đặt mục tiêu
            }
```

### 8.4 Admin Dashboard (`GET /dashboard/summary`) — 11 truy vấn

```text
  ┌──────────────────────── DashboardService.GetSummaryAsync ────────────────────────┐
  │  Chuẩn hoá giờ: nowVn = AppClock.NowVn() (UTC+7); mọi mốc tháng quy về UTC       │
  │  vì PaidAt/CheckInAt lưu UTC.  from > to → 422 INVALID_RANGE                     │
  ├──────────────────────────────────────────────────────────────────────────────────┤
  │  1  revenue                  Σ payments.Amount   Status=Paid, PaidAt ∈ [from,to] │
  │  2  activeCount              memberships Active & EndDate ≥ hôm nay              │
  │  3  expiredCount             Expired HOẶC (Active nhưng EndDate < hôm nay)       │
  │  4  todayCheckInCount        check_ins trong khung [00:00, 24:00) giờ VN         │
  │  5  pendingPaymentAmount     Σ payments Pending                                   │
  │  6  pendingPaymentCount      COUNT payments Pending                               │
  │  7  revenueByMonth[6]        gom theo THÁNG VN (PaidAt+7h), bù 0 cho tháng trống │
  │  8  recentlyExpired[10]      top 10 hết hạn gần nhất + initials tên              │
  │  9  ptCheckInsToday          check-in của member có TrainerAssignment Active     │
  │ 10  previousMonthRevenue     doanh thu tháng trước → FE tính % tăng/giảm         │
  │ 11  peakHour                 gom check-in 30 ngày theo giờ VN, lấy giờ đông nhất │
  │                              (mặc định 17–19h nếu chưa có dữ liệu)               │
  ├──────────────────────────────────────────────────────────────────────────────────┤
  │  Suy ra: facilityLoad% = min(100, checkInHômNay / GymCapacity(50) × 100)         │
  │          ptSession%    = min(facilityLoad%, ptCheckIn / 50 × 100)                │
  │          generalArea%  = facilityLoad% − ptSession%                              │
  └──────────────────────────────────────────────────────────────────────────────────┘
                              │  1 response · 15 field
                              ▼
             AdminDashboardContent → 4 MetricCard + BarChart 6 tháng + list hết hạn
```

### 8.5 Audit log — Minh viết, cả 5 người dùng

```text
   N1 AuthService        N2 MemberService      N3 PaymentService     N4 CheckInService
        │                       │                     │                     │
        └───────────────────────┴──────────┬──────────┴─────────────────────┘
                                           │  IAuditService.LogAsync(action, entity, id, meta)
                                           ▼
                          ┌────────────────────────────────────┐
                          │  AuditService (Minh)               │
                          │  UserId ← JWT claim NameIdentifier │
                          │  Metadata ← JsonSerializer         │
                          │  CreatedAt ← UtcNow                │
                          └────────────────┬───────────────────┘
                                           ▼
                                    audit_logs (bảng của Minh)
                                           │
                                           ▼
                          DashboardService.GetAuditLogsAsync (Minh)
                            filter userId/action/from/to/search + phân trang
                            + JOIN users để hiện tên người thao tác
                                           │
                                           ▼
                             /admin/audit-logs (AuditLogsContent)

   Action do CHÍNH Minh ghi: SET_CALORIE_TARGET · CREATE_MEAL_LOG · CREATE_FOOD · CONFIRM_AI_FOOD
```

---

## 9. LUẬT NGHIỆP VỤ TRÍCH TỪ CODE

| # | Luật | Cài ở đâu | Con số |
|---|---|---|---|
| BR-01 | Member chưa có gói active **chỉ tìm được 20 món đầu (A→Z)**; món thứ 21 trở đi "không tồn tại" với họ | `FoodItemService.cs:15–56` | `FreeFoodLimit = 20` |
| BR-02 | Admin/Staff/PT **luôn** thấy toàn bộ kho món | `HasFullFoodAccessAsync` | — |
| BR-03 | Tìm món **không phân biệt dấu và hoa/thường** — gõ "com" ra "Cơm" | collation `Latin1_General_100_CI_AI` | — |
| BR-04 | Tên món **unique**; thêm trùng tên → trả món cũ 200, **không** báo lỗi | `AddAsync` + index unique | — |
| BR-05 | Cùng (hội viên, ngày, buổi) → **1 bản ghi meal_log duy nhất**, món trùng cộng dồn | `CreateMealLogAsync:158–200` | — |
| BR-06 | Mỗi hội viên mỗi ngày **1 mục tiêu calo** — đặt lại là ghi đè | unique `(MemberId, EffectiveDate)` | — |
| BR-07 | Mục tiêu áp dụng theo `EffectiveDate ≤ ngày xem`, lấy bản mới nhất (giữ được lịch sử) | `GetTargetForDateAsync` | — |
| BR-08 | Lịch sử calo mặc định **7 ngày**, ngày không ăn trả 0 | `GetHistoryAsync:330–372` | `AddDays(-6)` |
| BR-09 | Macro **không lưu** ở `meal_log_items`, luôn tính `FoodItem.macro × Quantity` | `ToResponse(MealLog)` | — |
| BR-10 | Quét ảnh AI **bắt buộc có gói active** — free tier bị chặn 403 | `FoodScanService.HasActivePackageAsync` | — |
| BR-11 | Ảnh chỉ nhận **JPG/PNG ≤ 5MB**, request limit 6MB | `ScanImageAsync` + `[RequestSizeLimit]` | 5MB / 6MB |
| BR-12 | Gemini timeout **20s** (kẹp trong 5–60s) | `GeminiOptions` + `GeminiService` ctor | 20s |
| BR-13 | Món do AI tạo: `Source="AI"`, `Unit="g"`, `ServingSize=100` | `ConfirmAiFoodAsync` | — |
| BR-14 | Ownership 4 role: Admin/Staff xem tất cả · Member chỉ mình · **PT chỉ hội viên đang được phân công Active** | `NutritionService.CanAccessAsync` | — |
| BR-15 | Sức chứa phòng gym cố định để tính % tải | `DashboardService.GymCapacity` | 50 |
| BR-16 | Mốc "hôm nay"/"tháng này" theo **giờ VN (UTC+7)**, dữ liệu lưu UTC | `AppClock` + `VnMonthStartUtc` | +7h |
| BR-17 | `pageSize` audit log kẹp **1–100**, mặc định 20 | `GetAuditLogsAsync:201` | 1–100 |

---

## 10. TEST ĐÃ CÓ

### Backend — `tests/GymMaster.Api.Tests/` (37 test thuộc Minh)

```text
NutritionServiceTests.cs      502 LOC   14 test   mục tiêu calo, ghi bữa, gộp trùng,
                                                  tổng kết, lịch sử, ownership 4 role
FoodScanServiceTests.cs       330 LOC   11 test   chặn không gói, ảnh sai định dạng/quá cỡ,
                                                  match DB vs AI, confirm trùng tên
FoodItemServiceTests.cs       200 LOC    6 test   tìm accent-insensitive, giới hạn 20 món,
                                                  Find-or-Create
DashboardServiceTests.cs      237 LOC    5 test   summary, khoảng ngày sai, phân trang audit
GeminiServiceTests.cs          80 LOC    1 test   parse response Gemini
                            ─────────  ────────
                             1.349 LOC   37 test
```

### Frontend — `src/tests/`

```text
member-nutrition/   meal-log-form · meal-log-list · nutrition-summary-card ·
                    calorie-summary-workspace · tdee-calculator-upgrade ·
                    custom-food-ai-estimate                              (6 file)
admin-dashboard/    dashboard-metric-card · audit-log-table · admin-page-frame (3 file)
pwa/                water-tracker-local-only                              (1 file)
```

---

## 11. RANH GIỚI SỞ HỮU & KHOẢNG TRỐNG

### 11.1 Minh sở hữu tuyệt đối — sửa thoải mái

```text
BE:  Features/Nutrition/**          Features/Dashboard/**
     Infrastructure/GeminiService.cs · IFoodImageAnalyzer.cs
     Options/GeminiOptions.cs
     Entities/{FoodItem, MealLog, MealLogItem, CalorieTarget, AuditLog, NutritionEnums}.cs
FE:  features/member-nutrition/**   features/admin-dashboard/**
     app/(auth)/{welcome,about}/page.tsx · app/page.tsx
```

### 11.2 Đụng người khác — phải báo trước

| Chỗ | Ai sở hữu | Vì sao Minh đụng vào |
|---|---|---|
| `IAuditService` / `AuditService` | **Minh viết** nhưng 4 người kia đều gọi | đổi chữ ký `LogAsync` → 4 người vỡ build |
| `Features/Members/` | N2 (Quang Anh) | Nutrition đọc `MemberProfile` để kiểm ownership |
| `memberships`, `payments` | N3 (Lộc) | Dashboard tổng hợp doanh thu; FoodScan kiểm gói active |
| `check_ins`, `trainer_assignments` | N4 (Đam) | Dashboard đếm check-in, tính giờ cao điểm; Nutrition kiểm quyền PT |
| `Common/*`, `Program.cs`, `http-client.ts`, `components/ui/` | dùng chung 5 người | báo nhóm trước khi sửa |
| `/staff/dashboard`, `/pt/dashboard` | **màn tính điểm cho Minh** nhưng component ở `features/staff-front-desk/` (N3) và `features/pt-dashboard/` (N4) | ⚠ ghi rõ trong Project Tracking để khỏi trùng với N3/N4 |

### 11.3 Khoảng trống đã xác minh (nói thẳng khi thầy hỏi)

| Vấn đề | Trạng thái thật |
|---|---|
| `GET /api/v1/food-items/online-search` | **Backend CHƯA implement.** Chỉ có mock MSW ở `src/mocks/handlers/nutrition.handlers.ts:195`. FE gọi thử, thất bại thì rơi về Open Food Facts công khai — mà nhánh đó cũng đang tắt bằng `ENABLE_ONLINE_SEARCH = false` (`FoodSearchPanel.tsx:133`). **Làm nốt thì tính thêm 1 function.** |
| Endpoint thứ 13 | `POST /foods/estimate-nutrition` **có trong code, thiếu trong `phan-cong.md`** (bảng ghi 12). Nhớ thêm vào Project Tracking. |
| `GeminiService.WriteDiag` | ghi log chẩn đoán ra `%TEMP%/gemini_diag.log` — tiện debug, nhưng là I/O đồng bộ trong đường request, nên gỡ trước khi nộp bản chạy thật. |
| Quét mã vạch (barcode) | **đã bỏ khỏi phạm vi** (commit `1a03eda`, `65cd05f`, `59fd921`, `b1152d0`). Đừng nhắc trong tài liệu. |
| `WaterTrackerCard` | chỉ lưu **local**, không có API/DB — nên khai là tính năng phụ, đừng đưa vào bảng function tính điểm như một function BE. |
| Thông điệp lỗi backend | tiếng Việt **không dấu** ("Khong tim thay hoi vien.") trong khi UI có dấu — thầy có thể hỏi, đây là quy ước toàn dự án chứ không riêng Minh. |

---

## 12. TÀI LIỆU PHẢI NỘP

### 12.1 6 hình SDS (chốt 22/07/2026 — `phan-cong-sds.md`)

| No. | Hình | Loại |
|---:|---|---|
| 01 | **System Architecture / Component** — sơ đồ toàn hệ thống | tổng quan |
| 02 | **Frontend Package** — cấu trúc package FE | tổng quan |
| 23 | **Class — Nutrition / Meal Journal** | class |
| 24 | **Sequence — Nutrition / Meal Journal** | sequence |
| 25 | **Class — Gemini Food Recognition** | class |
| 26 | **Sequence — Gemini Food Recognition** | sequence |

**Ràng buộc kỹ thuật khi vẽ** (để hình khớp code thật):

```text
✓ Nutrition dùng ĐÚNG 4 lớp:  FoodItemsController · MealLogsController ·
                              MemberNutritionController · NutritionService
✓ AI dùng ĐÚNG 3 lớp:         FoodScanController · FoodScanService · GeminiService
✓ Macro tổng hợp TỪ FoodItem — meal_log_items CHỈ có Quantity và Calories
✗ food_items KHÔNG có NormalizedName, KHÔNG có trạng thái "AIConfirmed"
✓ Giá trị nguồn AI là  Source = "AI"
✓ Sequence phải có nhánh lỗi: 403 MEMBERSHIP_REQUIRED · 422 INVALID_FILE · 502 AI_*
```

Ngoài ra Minh là người **chèn ảnh vào SDS, cập nhật caption/mục lục (Ctrl+A → F9), render PDF và kiểm tra lần cuối**.

### 12.2 Checklist tài liệu cá nhân

- [ ] **RDS mục II** — mỗi use case 1 bảng 15 dòng + bảng Business Rules (dùng §9 làm nguồn)
- [ ] **RDS mục III** — bảng field UI + Database Access + câu LINQ thật (chép từ service, §6)
- [ ] **SDS mục II** — class diagram · đặc tả method · sequence diagram · DB queries
- [ ] **Project Tracking** — **10 dòng function**, cột In Charge ghi `Minh` (cân nhắc thêm dòng 13 endpoint)
- [ ] **Issues Report** — cột `Functions/Screens` khớp **y hệt** tên ở Project Tracking
- [ ] **AI Usage Report** — theo tuần; cột *Validation* và *Risks* là chỗ ăn điểm

### 12.3 Ước lượng điểm (tham khảo, không phải điểm thật)

```text
Individual Results = LOC × Quality theo function
   60 (đơn giản) / 120 (trung bình) / 240 (phức tạp) × Quality 100% / 75% / 50%
   Ngưỡng tối đa: ≥ 720 cả dự án

   Minh ước lượng ~1.260  →  gần GẤP ĐÔI ngưỡng, an toàn.
   Nặng nhất ở: MealJournal (form + tìm món 1.560 LOC) và Admin Dashboard.
```

---

## 13. BẰNG CHỨNG GIT

Minh commit bằng **2 identity** — muốn lấy đủ lịch sử phải tra cả hai:

```bash
git log --author=Minhdicodedao --author=minhbao --oneline
```

| Repo | Số commit |
|---|---:|
| `GymMaster-backend` | 31 |
| `GymMaster-frontend` | 18 |
| **Tổng** | **49** |

### Các commit tiêu biểu

```text
BACKEND
  8041aee  feat(be): them tinh nang Quet anh mon an bang AI (Gemini) — additive
  7808b26  feat(be): AI quet tach thanh phan + uoc luong gram
  84e79f3  fix(be): tim mon an khong phan biet dau (accent-insensitive)
  28f4d5b  feat(be): gioi han 20 mon co dinh cho member chua co goi tap
  2cabd08  feat(nutrition): estimate food macros from a name
  ef73ecf  test(nutrition): expand white-box coverage
  0f0efda  test(dashboard): cover summary and audit branches
  4f1baca  test(nutrition): cover text-based Gemini estimates
  e983c10  test(api): add connection black-box and performance suites
  2a2f9df  docs: add corrected GymMaster SDS
  88ef524  docs: add SDS diagram allocation
  60bee93  docs(final): create final document package

FRONTEND
  73f722c  feat(fe): them UI Quet anh mon an bang AI (Gemini) — additive
  4709518  feat(fe): nhat ky an nhap theo GRAM + AI prefill gram thanh phan
  4d6012d  feat(fe): mo tier mien phi cho nhat ky an + banner nhac mua goi
  f8abc04  feat(nutrition): estimate custom food macros with AI
  e923297  fix(nutrition): restrict food search to database results
  4d05abf  fix(nutrition): keep meal quantity input focused
  20a0cb6  fix(fe-about): cập nhật tổng quan
  aa087a8  test(nutrition): verify focus and AI estimate in Chromium
```

> ⚠ Nhiều việc của Minh vào `main` qua **merge commit do `BanhMiChao` tác giả**
> (`Merge branch 'Minh': ...`). Khi đối chiếu đóng góp, đừng để bị quy nhầm cho Như.

---

## PHỤ LỤC — TRA CỨU NHANH

```text
HỆ THỐNG ĐANG CHẠY (production — người dùng thật vào đây)
  GCP project : gymmaster-500004        region: asia-southeast1
  Frontend    : Cloud Run  gymmaster-os      (Next.js, node:22-alpine)
  Backend     : Cloud Run  gymmaster-api     port 8080
                https://gymmaster-api-741815287158.asia-southeast1.run.app
  Database    : Cloud SQL for SQL Server  gymmaster-sql-sg
  Deploy      : GitHub Actions → Deploy to Cloud Run → Run workflow (bấm tay, không tự động)
  ⚠ /openapi/v1.json chỉ sống ở LOCAL — trên cloud luôn 404, không phải lỗi deploy
  ⚠ NEXT_PUBLIC_API_BASE_URL bake lúc BUILD (Dockerfile ARG), sửa env Cloud Run không ăn thua

CHẠY LOCAL (chỉ khi lập trình / debug)
  Backend :  cd GymMaster-backend/backend/GymMaster.API && dotnet run     → :5042
  Frontend:  cd GymMaster-frontend && npm run dev                          → :3000
  Login   :  http://localhost:3000/login
  OpenAPI :  http://localhost:5042/openapi/v1.json   (KHÔNG có /swagger)

TÀI KHOẢN DEMO
  admin@gymmaster.local / Admin123!      staff@gymmaster.local  / Staff123!
  pt@gymmaster.local    / Pt123!         member@gymmaster.local / Member123!

CẤU HÌNH AI (User Secrets, KHÔNG commit)
  dotnet user-secrets set "Gemini:ApiKey" "..."

FILE ĐỌC KHI CẦN NGỮ CẢNH
  Phân công code   : docs/06-Management/phan-cong.md
  Phân công hình   : docs/06-Management/phan-cong-sds.md
  Spec của Minh    : docs/03-Interface-Specs/feature-specs/007-nutrition-calorie/
                     docs/03-Interface-Specs/feature-specs/008-dashboard-audit/
                     docs/03-Interface-Specs/feature-specs/009-image-food-recognition/
  Schema DB        : docs/02-SDD-Architecture/database-design/database-schema.md
  Luật dự án       : CONSTITUTION.md · CLAUDE.md
```
