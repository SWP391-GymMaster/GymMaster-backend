---
name: swp391-docs
description: "Làm tài liệu nộp môn SWP391/ISP392 cho GymMaster: RDS (Requirement & Design Spec), SDS, Project Tracking, Issues Report, Final Release, AI Usage Report. Điền theo TỪNG function một, lấy nội dung từ code và spec thật. Dùng khi cần viết, cập nhật hoặc kiểm tra bất kỳ template nào của môn học."
---

# /swp391-docs

Điền bộ template SWP391 **từ code và spec thật**, làm **từng function một**.

## Luật bất di bất dịch

1. **KHÔNG sinh cả tài liệu trong một lượt.** 22 bảng sinh một lèo thì tên function sẽ trôi,
   số mục sai, không ai review nổi. Làm **1 function → user duyệt → function tiếp theo**.
2. **KHÔNG bịa nội dung.** Mọi endpoint, actor, bảng, business rule phải truy được về file thật.
   Không tìm thấy thì **hỏi user**, đừng đoán.
3. **Tên function là hợp đồng.** Một tên duy nhất dùng xuyên suốt Project Tracking ↔ RDS ↔
   Issues Report ↔ SDS. Lệch một chữ là cả gói rời rạc, thầy chấm sẽ thấy ngay.
4. **Trích dẫn nguồn** khi báo cáo cho user: nội dung này lấy từ file nào, dòng nào.

## Bối cảnh môn học

- 4 iteration. Iter1–3: **20%** mỗi mốc; Iter4 (Final): **40%**.
- Mỗi mốc: Release Package 20% · Team Co-ordinating & Presenting 20% ·
  **Individual Results 60%** = tổng `LOC × Quality` (Iter4 là 40%).
  - LOC: **60 / 120 / 240** theo độ phức tạp simple / medium / complex.
  - Quality: **100% / 75% / 50%** do thầy chấm.
  - Cần **≥180** mỗi iteration, **≥720** cả dự án để đạt tối đa.
- **RDS/SDS là tài liệu SỐNG**: không viết lại mỗi iteration, mà bồi thêm vào chính nó.
  Bảng *Record of Changes* ở đầu file là bằng chứng làm đều — **luôn cập nhật khi sửa**.

Chi tiết đầy đủ: `references/mon-hoc.md`.

## Vênh giữa template và dự án — cảnh báo user, đừng im lặng sửa

| Template nói | GymMaster thực tế |
|---|---|
| GitLab (bắt buộc tag/baseline khi nộp) | **GitHub** |
| Oracle **Java** Coding Standards | **C# / .NET 10** |
| **MySQL** naming convention | **SQL Server** |
| Sample business rule: mật khẩu **MD5** | **BCrypt cost 12** — ĐỪNG chép mẫu MD5 |
| Gọi là "SRS Document" | File thật tên **RDS** = Requirement **& Design** Spec |
| Cài đặt bằng **NetBeans 8.2** | .NET SDK + Node |

Những chỗ này user cần **hỏi thầy**, không tự quyết.

## Thứ tự bắt buộc

### Bước 1 — Chốt danh sách function (LÀM TRƯỚC MỌI THỨ)

```bash
PY=$(cat graphify-out/.graphify_python)
$PY .claude/skills/swp391-docs/scripts/inventory.py --csv out/inventory.csv
```

Quét ra **endpoint backend** (từ `[Http*]` + `[Route]` + `[Authorize]` trong `Features/`)
và **screen frontend** (từ cây `src/app`). Hiện tại: ~89 endpoint, ~47 screen.

Script hiểu đúng semantics ASP.NET: không có `[Authorize]` ở cả action lẫn class ⇒ **Anonymous**
(không phải "Authenticated"); `[HttpGet("/...")]` có `/` đầu là route **tuyệt đối**, ghi đè `[Route]`.

```bash
uv run --with openpyxl python .claude/skills/swp391-docs/scripts/fill_tracking.py \
  out/inventory.csv ".claude/skills/swp391-docs/templates/Template1_Project Tracking.xlsx" \
  "out/docs/GYM_Project Tracking.xlsx" --in-charge "Ten Ban"
```

→ Ghi vào **sheet `Project`** — Student Guides gọi là "sheet Product" nhưng file `.xlsx` thật
đặt tên là `Project`.

**Đơn vị dòng = SCREEN + NON-UI FUNCTION, KHÔNG phải từng REST endpoint.**
Thầy chấm `LOC × Quality` theo function nghiệp vụ (60/120/240, cần ≥720 cả dự án) — nếu 240 là
tối đa thì 720 ≈ 3 function phức tạp, tức thầy đếm ở mức nghiệp vụ như mẫu *"User Login"*.
Đổ 89 endpoint thô vào là **sai đơn vị**, làm loãng phần chấm cá nhân. 89 endpoint thuộc về
**RDS mục III** (Database Access + SQL) của từng màn hình.

Kết quả: **50 dòng** = 47 screen + 3 non-UI (VNPay IPN, auto-cancel 30′, lazy Expire).
Tên nghiệp vụ lấy từ `../GymMaster-frontend/docs/design/08_ROUTE_MAP_NAVIGATION.md`
(bảng `NAMES` trong script); route mới chưa có tên sẽ được script báo ra để đặt tay.

**Danh sách này là nguồn sự thật.** Mọi file sau phải dùng đúng tên ở đây.

### Bước 2 — RDS phần I (Overview), một lần cho cả dự án

| Mục RDS | Lấy từ |
|---|---|
| I.1.1 Actors | `docs/init/03_SRS_USE_CASES.md` §1 |
| I.1.2 Use Cases + diagram | `03_SRS_USE_CASES.md` §2 → skill `swp391-diagrams` |
| I.2.1 Screens Flow | `../GymMaster-frontend/docs/design/25_SCREEN_FLOW.md` |
| I.2.2 Screen Descriptions | `../GymMaster-frontend/docs/design/08_ROUTE_MAP_NAVIGATION.md` |
| I.2.3 Screen Authorization | cột `actor` của `inventory.csv` |
| I.2.4 Non-UI Functions | VNPay IPN, `MembershipLifecycle` (auto-cancel 30′, lazy Expired) |
| I.3.1 Database Design | `docs/init/15_DATABASE_SCHEMA.md` (24 bảng) + ERD |
| I.3.2 Code Packages | `Features/` (10 package) + `graphify-out/GRAPH_REPORT.md` |

### Bước 3 — RDS phần II + III, LẶP TỪNG FUNCTION

Với **mỗi** function trong Project Tracking:

1. Đọc spec: `specs/00X-<ten>/spec.md` → lấy FR, error code, acceptance criteria.
2. Đọc code: `Features/<Feature>/<X>Controller.cs` + `<X>Service.cs` → lấy luồng thật.
3. Điền **II.x — Functional Description** (bảng 15 dòng):
   `UC ID and Name · Created By · Date Created · Primary/Secondary Actor · Trigger ·
   Description · Preconditions · Postconditions · Normal Flow · Alternative Flows ·
   Exceptions · Priority · Frequency of Use · Business Rules · Other Information · Assumptions`
   - Đánh số **bắt buộc**: Normal Flow `X.0`, Alternative `X.Y`, Exception `X.Y.EZ`
     (vd `5.0.E2` = exception thứ 2 của normal flow UC-5).
   - **Exceptions lấy từ error code thật trong code**, vd `DAILY_LIMIT_REACHED` (409),
     `PACKAGE_PT_REQUIRED` (409), `ALREADY_HAS_ACTIVE`.
4. Điền **II.x.b — Business Rules** (ID | Rule | Description) — lấy từ FR-xx trong spec.
5. Điền **III.x — Design Spec**: UI Design (Field Name | Field Type | Description) +
   Database Access (Table | CRUD | Description) + **SQL Commands thật**.
6. Ghi số mục vừa dùng (vd `II.3`, `III.5`) ngược vào **cột SRS/SDS** của sheet IterX.
7. **Dừng lại cho user duyệt.** Xong mới sang function kế.

### Bước 4 — SDS (cho function mình phụ trách)

Package diagram → Database Design → mỗi feature: **Class Diagram + Class Specifications
(mô tả từng method: input, output, xử lý bên trong) + Sequence Diagram + SQL**.
Diagram gọi skill `swp391-diagrams`.

### Bước 5 — Issues Report

`Title | Description | Issue ID | URL | State | Assignee | Created At | Due Date |
Milestone | Labels | Functions/Screens`

Cột **Functions/Screens phải khớp y hệt** tên trong Project Tracking. Label theo mẫu:
`Task, 3_Done` · `Defect, 2_Doing` · `WP, 1_To Do`.

### Bước 6 — AI Usage Report (`Template0`)

Ghi theo tuần: `SDLC Phase | Task | AI Tool | AI Output | Student's Validation/Modification |
Evidence | Quantitative Measure | Value Added (1-5) | Risks/Limitations`.

Cột **Validation/Modification** và **Risks** là chỗ ăn điểm — thầy muốn thấy bạn *kiểm chứng lại*
chứ không chép mù. Ví dụ thật đã xảy ra trong dự án này:
- graphify chỉ `AuthServiceResult` là god node degree 185 → **đã grep lại 38 file để xác minh**
  trước khi tin → đổi tên thành `ServiceResult`.
- `dotnet list --vulnerable` **luôn trả exit 0** kể cả khi có lỗ hổng → nếu tin exit code thì CI
  báo xanh giả → phải grep output. Phát hiện khi test thử 2 chiều.

### Bước 7 — Final Release (chỉ ở Iter4)

Liệt kê package: `XYZ_DB_final.sql`, `XYZ_SRS_final.docx`, `XYZ_SDS_final.docx`,
`XYZ_Final Product Backlog.xlsx`, `XYZ_Issues Report.xlsx` + link tag source + link video.
`XYZ` = mã nhóm + mã dự án (vd `G1-GYM`). Cộng Installation Guide + User Manual.

## Kiểm tra tính nhất quán (chạy trước khi nộp)

```bash
$PY .claude/skills/swp391-docs/scripts/check_consistency.py
```

Bắt: tên function lệch giữa Project Tracking ↔ Issues Report, cột SRS/SDS trỏ vào mục
không tồn tại, function có trong code mà thiếu trong tracking.

## Template gốc

`templates/` giữ nguyên bản của thầy — **điền vào bản sao, đừng sửa bản gốc**.
Điền `.docx`/`.xlsx` bằng `python-docx` / `openpyxl` để giữ đúng style chấm điểm:

```bash
uv run --with python-docx --with openpyxl python <script>
```

## Giới hạn đã biết — đọc trước khi hứa với user

- **Chỉ sheet `Project` có script điền** (`fill_tracking.py`). **RDS/SDS `.docx` KHÔNG có script** —
  agent tự điền theo quy trình ở trên bằng `python-docx`. Cố ý: điền RDS cần đọc hiểu spec+code
  từng function, không máy móc được. Đừng nói với user là "có script điền RDS tự động".
- **Sheet `Iter1–4` phải user tự chia** — chia function nào vào iteration nào là lịch sử dự án,
  script không suy ra được. `fill_tracking.py` chỉ đụng sheet `Project`.
- `inventory.py` không suy được **độ phức tạp** (simple/medium/complex) để tính LOC — user tự chấm.
- Không suy được **In Charge** — user tự điền.
- Screen description lấy từ đường dẫn route, không phải mô tả nghiệp vụ — cần user viết lại cho người đọc.
- `check_consistency.py` so khớp tên **chính xác từng ký tự**. Tên có khoảng trắng thừa sẽ báo lệch.
