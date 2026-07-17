# Tiến độ làm tài liệu SWP391 — cập nhật 2026-07-17

> File này để agent (và người) đọc lại sau khi mất ngữ cảnh chat.
> Sản phẩm nằm ở `out/docs/` và `out/diagrams/` — **`out/` bị gitignore**, chỉ có trên máy local.
> Muốn dựng lại toàn bộ: chạy lại chuỗi lệnh ở mục "Dựng lại từ đầu" bên dưới.

## Trạng thái 5 file nộp

| # | File | Trạng thái | Ghi chú |
|---|---|---|---|
| 1 | `GYM_Project Tracking.xlsx` | ✅ xong | 50 dòng (47 screen + 3 non-UI), Iter1–4 điền theo **ngày git thật** |
| 2 | `GYM_RDS.docx` | ✅ 3 phần, còn chỗ trống | 118 bảng, 48 ảnh, 20MB |
| 3 | `GYM_SDS.docx` | ✅ xong, còn chỗ trống | 15 package, 23 bảng, 10 feature |
| 4 | `GYM_Issues Report.xlsx` | ⬜ **chưa làm** | Nguồn: git log (236 commit, có ngày + tác giả) |
| 5 | `GYM_Final Release.docx` | ⬜ **chưa làm** | Làm cuối cùng, chỉ là bìa gói |
| + | `AI Usage Report.xlsx` | ⬜ **chưa làm** | Ghi theo tuần, xem mục "ví dụ ăn điểm" dưới |

## Chỗ trống CÒN LẠI (đã đánh dấu `[CAN BO SUNG]` ngay trong file, không bịa)

| Chỗ | Hiện có | Vì sao thiếu |
|---|---|---|
| **SQL Commands** (RDS III + SDS II.d) | **0/47** | Backend dùng **EF Core LINQ, không có SQL thô**. Cách lấy SQL thật: bật log `Microsoft.EntityFrameworkCore.Database.Command`, chạy app, gọi endpoint, chép SQL EF sinh ra. |
| **Main Flow** (RDS phần II) | **9/30 UC** | `docs/init/03_SRS_USE_CASES.md` chỉ viết chi tiết 9 UC (UC-01, 04, 07, 08, 09, 10, 17, 22, 26). 21 UC còn lại cần viết tay hoặc suy từ code. |
| **Bảng field** (RDS III) | **7/47 màn** | Phần lớn màn là trang danh sách, không có form → không có Zod schema. |
| **Ảnh mockup** (RDS III) | **43/47** | 4 route chưa có trong `visual-screenshots.spec.ts`. |
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

# 3. Project Tracking
$UV run --with openpyxl python $S/fill_tracking.py out/inventory.csv \
   "$T/Template1_Project Tracking.xlsx" "out/docs/GYM_Project Tracking.xlsx" --in-charge "BanhMiChao"
# (fill_iterations.py cần iters_map.csv — sinh từ git, xem git log của commit bcca327)

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

# 6. Kiểm tra
$UV run --with openpyxl python $S/check_consistency.py "out/docs/GYM_Project Tracking.xlsx"
```

Ảnh UI (43 ảnh, cho RDS phần III): chạy ở repo **frontend**:
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
