# Tiến độ làm tài liệu SWP391 — cập nhật 2026-07-17 (lần 2)

> File này để agent (và người) đọc lại sau khi mất ngữ cảnh chat.
> Sản phẩm nằm ở `out/docs/` và `out/diagrams/` — **`out/` bị gitignore**, chỉ có trên máy local.
> Muốn dựng lại toàn bộ: chạy lại chuỗi lệnh ở mục "Dựng lại từ đầu" bên dưới.

## Trạng thái 5 file nộp

> **2026-07-17: đã GỠ tính năng thông báo (UC-25) khỏi phạm vi.** BE `c61cadc`, FE `54475f0`.
> Nó là vỏ rỗng (không bảng DB, không service, controller trả `[]`). Mọi số liệu dưới đây
> đã tính lại: **47→46 màn**, **30→29 use case**, 60→59 dòng Tracking.

| # | File | Trạng thái | Ghi chú |
|---|---|---|---|
| 1 | `GYM_Project Tracking.xlsx` | ✅ xong | 59 dòng (46 screen + 3 non-UI + 10 API), Iter1–4 theo **ngày git thật** |
| 2 | `GYM_RDS.docx` | ✅ 3 phần, còn chỗ trống | **161 bảng**, 47 ảnh, 19.6MB |
| 3 | `GYM_SDS.docx` | ✅ xong, còn chỗ trống | 15 package, 23 bảng, 10 feature |
| 4 | `GYM_Issues Report.xlsx` | ✅ xong | 215 issue từ git log thật, map 125/215 vào Functions/Screens |
| 5 | `GYM_Final Release.docx` | ✅ xong | Bìa gói + Installation Guide thật |
| + | `AI Usage Report.xlsx` | ✅ xong | 10 dòng, đều là việc CÓ THẬT kèm link commit |

## Chỗ trống CÒN LẠI (đã đánh dấu `[CAN BO SUNG]` ngay trong file, không bịa)

| Chỗ | Hiện có | Vì sao thiếu |
|---|---|---|
| **SQL Commands** (RDS III) | **XONG: 43 LINQ + 3 no-API** | Backend dùng EF Core LINQ, không có SQL thô. Thay vì để trống, ghi **câu LINQ thật** (đọc/read của màn) kèm file nguồn — SQL thực do EF sinh lúc chạy. 3 màn còn lại (About/Welcome/Landing) là trang tĩnh không gọi API. Bản đồ `FEATURE_LINQ` trong `fill_rds_design.py`. Nếu thầy bắt SQL nguyên văn: bật log `Microsoft.EntityFrameworkCore.Database.Command`, chạy app, gọi endpoint, chép. |
| **Main Flow** (RDS phần II) | **9/29 UC** | `docs/01-SRS-Requirements/use-cases/srs-use-cases.md` chỉ viết chi tiết 9 UC (UC-01, 04, 07, 08, 09, 10, 17, 22, 26). **20 UC còn lại** cần viết tay hoặc suy từ code. UC-24 (Barcode) là Deferred — **không có code để suy**, phải để `[CAN BO SUNG]`. |
| **Bảng field** (RDS III) | **XONG: 37 form + 9 no-form** | 37 màn có form → field lấy từ Zod schema thật. 9 màn còn lại là dashboard / trang xem / landing — thật sự **không có form**, ghi thẳng "Màn chỉ hiển thị dữ liệu, không có Zod schema" (trung thực, KHÔNG phải lỗ hổng). Đừng ép bảng field vào màn không có form. Fix ở commit `83acf7a` (dùng bản đồ `SCHEMA_OF` + `who_owns.FE_FEATURE` thay vì đoán tên thư mục). |
| **Ảnh mockup** (RDS III) | **42/46** | 4 route chưa có trong `visual-screenshots.spec.ts`. |
| **Business Rules** (RDS II) | thừa | Mỗi UC đang liệt kê **toàn bộ** FR của spec, đánh dấu `[CAN CAT BOT]`. Người đọc phải cắt. |
| **Class/Sequence diagram** (SDS) | ghi `[Chèn ảnh]` | Phải qua draw.io xuất PNG rồi chèn tay. |

## Quyết định đã chốt (đừng làm lại)

1. **Đơn vị dòng của Project Tracking = SCREEN + non-UI function, KHÔNG phải REST endpoint.**
   Thầy chấm `LOC × Quality` theo function nghiệp vụ (60/120/240, ≥720 cả dự án) → 240 tối đa nghĩa là
   720 ≈ 3 function phức tạp → thầy đếm ở mức nghiệp vụ. 89 endpoint thuộc về **RDS mục III**.
2. **Cột `Actual` lấy từ git, KHÔNG bịa.** Thực tế: Iter1=40 screen, Iter2=1, Iter3=6, Iter4=0.
   Dự án dồn hết vào 2 tuần đầu. Commit có ngày công khai — thầy đối chiếu được.
3. **Iter4 lấp bằng cột `Updated`**, dùng việc CÓ THẬT trong cửa sổ Iter4 (12/07→): refactor 91 file
   sang feature-based, đồng bộ spec kit, vá CVE-2026-49451, thêm CI. Guide cho phép Iter4 gồm
   *"Updates for iter1-3 functions/screens"*.
4. **Không dùng PlantUML** — draw.io đã gỡ từ cuối 2025. Use case dùng `.drawio` XML trực tiếp,
   còn lại dùng Mermaid.
5. **Business Rules: liệt kê thừa thay vì lọc.** Đã thử lọc theo từ khoá và HỎNG: "Login" chỉ khớp
   `FR-RBAC-03` (chẳng liên quan), còn `FR-AUTH-02` (đúng nghĩa login) bị loại vì viết
   "gửi email + mật khẩu đúng", không có chữ "login". Thừa thì thấy mà cắt, sai thì không ai biết.

## Bẫy đã gặp — đừng vấp lại

- **Chỉ số bảng hardcode → lỗi IM LẶNG (nặng nhất, 2026-07-17).** `fill_rds_design.py` từng ghim
  `d.tables[74]`/`[75]` làm bảng mẫu. Số bảng phụ thuộc **số use case của phần II**, nên chỉ cần
  bớt 1 UC là index trượt → `tpl = None` → **không sinh bảng nào**, mà biến đếm `no_field` cũng
  không tăng nên script **vẫn in "46/46"**. Phát hiện bằng cách **đọc ngược .docx ra đếm bảng**
  (69 thay vì ~160). Bản RDS "118 bảng" cũ **cũng đã sai sẵn**: nó nhân bản nhầm từ bảng Business
  Rules và **chưa bao giờ có bảng Database Access** — không ai nghi vì 118 trông hợp lý.
  → Đã sửa: tìm bảng mẫu theo **tiêu đề** (`Field Name|Field Type|Description` và
  `Table|CRUD|Description`), lấy **trước khi xoá** vùng mẫu, thiếu thì `sys.exit` chứ không im.
  **Bài học: đừng tin số script tự in ra — đọc ngược file đã sinh mà đếm.**
- **Ba script cùng parse một bảng UC** (`fill_rds_overview`, `fill_rds_usecases`,
  `usecase_drawio`). Sửa trạng thái UC phải sửa **cả ba**, không thì số liệu lệch nhau
  (đã gặp: I.1.2 báo 30 UC trong khi phần II chỉ sinh 29).
- **Ô gộp trong Word:** `row.cells` trả 4 phần tử nhưng ô gộp dùng **chung một `<w:tc>`**. Ghi
  `cells[1]` rồi xoá `cells[2:]` là tự xoá mất chữ vừa ghi → **cả 30 bảng UC rỗng trắng**.
  Dùng `uniq_cells()` lọc theo `id(_tc)`.
- **Template chứa dự án mẫu của người khác:** `Patron` 114 lần, `Payroll` 23, `Cafeteria` 12,
  `GAMS` ở tiêu đề, ảnh có chữ "Teacher". Luôn chạy `strip_samples.py` sau khi điền.
- **Vòng xoá cuốn luôn dòng tên dự án** → phải chèn lại, không thì file mất tên.
- **`print()` tiếng Việt có dấu làm crash** console Windows (cp1252). Dùng `.encode('ascii','replace')`.
- **Markdown lọt vào Word:** `**đậm**`, `` `code` `` hiện nguyên xi. Dùng `demd()`.
- **`git log --diff-filter=A` bỏ sót file thêm qua merge commit**; `--follow` thì lần nhầm sang file khác.
  Lấy commit **cũ nhất chạm file** là chuẩn nhất.
- **FE `.gitignore` có `/docs/`** (block `agent-harness:managed`) → 43 ảnh UI **không được commit**,
  chỉ có trên máy local. File `.md` còn trong git vì đã track từ trước.

## Dựng lại từ đầu

```bash
cd d:/GymMaster/GymMaster-backend
PY=$(cat graphify-out/.graphify_python)
UV=".../uv.exe"          # xem CLAUDE.md
S=.claude/skills/swp391-docs/scripts
D=.claude/skills/swp391-diagrams/scripts
T=.claude/skills/swp391-docs/templates

mkdir -p out/docs out/diagrams

# 1. Liệt kê function thật
$PY $S/inventory.py --csv out/inventory.csv

# 2. Diagram
$PY $D/usecase_drawio.py out/diagrams/UseCase.drawio
$PY $D/erd_mermaid.py    out/diagrams/erd_full.mmd
$PY $D/class_mermaid.py  package out/diagrams/packages.mmd

# 3. Project Tracking  (iters_map.csv sinh từ git: commit CŨ NHẤT chạm page.tsx)
$UV run --with openpyxl python $S/fill_tracking.py out/inventory.csv \
   "$T/Template1_Project Tracking.xlsx" "out/docs/GYM_Project Tracking.xlsx" --in-charge "BanhMiChao"
$UV run --with openpyxl python $S/fill_iterations.py "out/docs/GYM_Project Tracking.xlsx" \
   out/iters_map.csv --in-charge "BanhMiChao" \
   --iter4-note "Refactor backend sang feature-based (91 file), dong bo spec kit, va CVE-2026-49451, them CI/CD, go tinh nang thong bao khoi pham vi"
$UV run --with openpyxl python $S/fill_in_charge.py "out/docs/GYM_Project Tracking.xlsx" \
   out/inventory.csv --add-backend

# 4. RDS — PHẢI theo đúng thứ tự này
$UV run --with python-docx --with openpyxl python $S/fill_rds_overview.py \
   "$T/Template2_RDS Document.docx" out/docs/GYM_RDS.docx out/inventory.csv
$UV run --with python-docx python $S/fill_rds_usecases.py out/docs/GYM_RDS.docx
$UV run --with python-docx --with openpyxl python $S/fill_rds_design.py \
   out/docs/GYM_RDS.docx out/inventory.csv
$UV run --with python-docx python $S/strip_samples.py out/docs/GYM_RDS.docx \
   --title "GymMaster" --subtitle "Gym Management Web System"

# 5. SDS
$UV run --with python-docx python $S/fill_sds.py "$T/Template3_SDS Document.docx" out/docs/GYM_SDS.docx
$UV run --with python-docx python $S/strip_samples.py out/docs/GYM_SDS.docx \
   --title "GymMaster" --subtitle "Software Design Specification"

# 6. Issues Report + Final Release + AI Usage
$UV run --with openpyxl python $S/fill_issues.py "$T/Template4_Issues Report.xlsx" \
   "out/docs/GYM_Issues Report.xlsx" "out/docs/GYM_Project Tracking.xlsx"
$UV run --with python-docx python $S/fill_final_release.py \
   "$T/Template5_Final Release Document.docx" "out/docs/GYM_Final Release.docx"
$UV run --with python-docx python $S/strip_samples.py "out/docs/GYM_Final Release.docx" \
   --title "GymMaster" --subtitle "Final Release Document"
$UV run --with openpyxl python $S/fill_ai_usage.py \
   "$T/Template0__SWP391_AI_Usage_Report_ Template.xlsx" "out/docs/GYM_AI Usage Report.xlsx"

# 7. Kiểm tra
$UV run --with openpyxl python $S/check_consistency.py "out/docs/GYM_Project Tracking.xlsx"
```

**Sau khi sinh xong, LUÔN đọc ngược file ra đếm** — script từng in "46/46" trong khi file có
**0 bảng** (xem mục Bẫy). Kỳ vọng hiện tại: RDS **161 bảng / 47 ảnh**, Tracking **59 dòng**,
Issues **215 dòng**.

Ảnh UI (42 ảnh, cho RDS phần III): chạy ở repo **frontend**:
```bash
cd ../GymMaster-frontend && npx playwright test src/tests/e2e/visual-screenshots.spec.ts
```

## Ví dụ "ăn điểm" cho AI Usage Report

Cột **Student's Validation/Modification** và **Risks/Limitations** là chỗ thầy chấm. Việc thật đã xảy ra:

- graphify chỉ `AuthServiceResult` là god node degree 185 → **đã grep lại 38 file để xác minh** trước
  khi tin → đổi tên `ServiceResult`. *Risk: AI có thể chỉ sai, phải kiểm chứng.*
- `dotnet list --vulnerable` **luôn trả exit 0** kể cả khi có lỗ hổng → tin exit code là CI báo xanh
  giả → phải grep output. **Đã test 2 chiều** bằng cách hạ package về bản dính lỗi.
- AI sinh 30 bảng UC nhưng **rỗng trắng** vì bẫy ô gộp Word → **phải đọc ngược file .docx** ra kiểm tra
  mới phát hiện. *Risk: không kiểm chứng thì nộp file trắng.*
- AI lọc Business Rules theo từ khoá → **sai** (Login khớp FR-RBAC-03) → **bỏ, quay lại liệt kê đủ**.
