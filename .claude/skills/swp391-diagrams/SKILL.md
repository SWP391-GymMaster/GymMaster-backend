---
name: swp391-diagrams
description: "Sinh diagram cho tài liệu SWP391 (RDS/SDS/slide) TỪ CODE VÀ SPEC THẬT của GymMaster — use case, ERD, sequence, class, package, screens flow. Xuất ra .drawio (mở/sửa trực tiếp trong draw.io) hoặc Mermaid. Dùng khi cần vẽ hoặc cập nhật bất kỳ sơ đồ nào cho doc môn học."
---

# /swp391-diagrams

Sinh diagram **từ code và spec thật**, không vẽ tay, không bịa. Spec/code đổi → chạy lại → diagram tự đúng.

## Nguyên tắc (quan trọng nhất)

1. **Không bao giờ tự nghĩ ra nội dung diagram.** Mọi actor, use case, bảng, class, bước sequence phải đọc được từ file thật trong repo. Nếu script không tìm thấy, **báo cho user**, đừng điền bừa.
2. **Luôn nói rõ diagram lấy từ đâu** (file nào, bao nhiêu node) để user kiểm chứng.
3. **Đối chiếu chéo khi có thể**: ERD sinh từ `Entities/*.cs` phải khớp `docs/init/15_DATABASE_SCHEMA.md`. Lệch nhau → báo user, đó là dấu hiệu code và spec đã trôi xa nhau.

## Chuẩn bị

Cần Python. Repo đã có interpreter sẵn từ graphify:

```bash
PY=$(cat graphify-out/.graphify_python)
```

Nếu file đó không có, chạy skill `graphify` trước, hoặc dùng `uv run --with <pkg> python`.

Mọi lệnh chạy từ **gốc repo backend**.

## Chọn công cụ theo loại diagram

| Diagram | Định dạng | Script | Ghi chú |
|---|---|---|---|
| Use Case | **.drawio** | `usecase_drawio.py` | Mermaid KHÔNG có use case diagram |
| ERD / Database Schema | Mermaid `erDiagram` | `erd_mermaid.py` | draw.io tự dàn layout |
| Sequence | Mermaid `sequenceDiagram` | `sequence_mermaid.py` | |
| Class | Mermaid `classDiagram` | `class_mermaid.py class` | |
| Package | Mermaid `classDiagram` + `namespace` | `class_mermaid.py package` | Mermaid không có notation package thật |
| Screens Flow | Mermaid `flowchart` | **chưa có script — viết tay** | nguồn: `../GymMaster-frontend/docs/design/25_SCREEN_FLOW.md` + `26_SWIMLANE_MAIN_FLOWS.md` |

**KHÔNG dùng PlantUML.** draw.io đã gỡ PlantUML khỏi app.diagrams.net từ cuối 2025
(<https://www2.drawio.com/blog/plantuml-to-mermaid>). Dán vào sẽ không nhập được.

## 1. Use Case diagram → .drawio

```bash
$PY .claude/skills/swp391-diagrams/scripts/usecase_drawio.py out/UseCase.drawio
```

Đọc `docs/init/03_SRS_USE_CASES.md` mục "2. Use Case Overview". Sinh **một tab cho mỗi actor**
(Admin/Staff/PT/Member/System) — đúng yêu cầu slide *"Use case diagram for each Role/Actor"*.
Tự hiểu `All` → 4 role, `Admin/Staff` → tách 2 actor.

User mở bằng draw.io: **File → Open** (không phải Insert). Mở ra sửa được từng hình.

## 2. ERD → Mermaid

```bash
$PY .claude/skills/swp391-diagrams/scripts/erd_mermaid.py out/erd.mmd
$PY .claude/skills/swp391-diagrams/scripts/erd_mermaid.py out/erd_billing.mmd --tables Membership,MembershipPackage,Payment,MemberProfile
```

Đọc `backend/GymMaster.API/Entities/*.cs`. Tự nhận PK/FK, kể cả khi tên không trùng entity
(`MemberId` → `MemberProfile`, `CreatedBy` → `User` — xem `FK_ALIAS` trong script).

**23 bảng vẽ chung một hình sẽ rối không đọc nổi.** Với RDS nên cắt theo feature bằng `--tables`.

## 3. Sequence diagram → Mermaid

```bash
$PY .claude/skills/swp391-diagrams/scripts/sequence_mermaid.py CheckInsController Create out/seq_checkin.mmd
```

Lần theo Controller action → Service method → **cả helper private trong cùng service** → các bảng DbContext.
Việc lần helper là bắt buộc: `CheckInService.CreateAsync` nhìn trực tiếp chỉ thấy bảng `CheckIns`,
nhưng `MemberProfiles`/`Memberships` nằm trong `ValidateMembershipAsync`/`ResolveMemberAsync`.

## 4. Class diagram → Mermaid

```bash
$PY .claude/skills/swp391-diagrams/scripts/class_mermaid.py class out/cls_billing.mmd --feature Billing
```

Đọc `Features/<Ten>/`. Lấy **chỉ method public** (private không thuộc về class diagram),
property của DTO, và quan hệ `..|>` (implements) / `..>` (constructor injection).
Interface lấy method không cần từ khoá `public` — C# ngầm định public.

Không truyền `--feature` thì quét cả `Features/` — **23 class một hình thì không đọc nổi**,
nên với SDS hãy làm từng feature.

## 5. Package diagram → Mermaid

```bash
$PY .claude/skills/swp391-diagrams/scripts/class_mermaid.py package out/packages.mmd
```

Sinh 15 package: 10 feature + `Common`/`Infrastructure`/`Entities`/`Data`/`Options`.
Mỗi package hiện tối đa 6 class cho dễ đọc.

## Đưa vào draw.io

- **.drawio**: File → Open File → chọn file. Sửa trực tiếp.
- **Mermaid**: Arrange → Insert → Advanced → **Mermaid** → dán nội dung `.mmd`.

Xuất ảnh dán vào RDS/SDS: File → Export as → **PNG**, tick *Transparent Background*, zoom 200%
(doc in ra không bị vỡ).

## Sau khi sinh, luôn báo user

- Diagram lấy từ file nào, bao nhiêu node/bảng/bước.
- Chỗ nào script **không suy ra được** và cần user tự thêm tay.
- Nhắc: diagram **phải mở kiểm tra bằng mắt** — XML hợp lệ không có nghĩa là hình vẽ đẹp/đúng.

## Giới hạn đã biết

- Use case: chưa vẽ quan hệ `<<include>>` / `<<extend>>` giữa UC với nhau (template có nhắc).
  Cần thì thêm tay trong draw.io.
- Package diagram: Mermaid không có notation package thật, chỉ giả lập bằng `namespace`.
- Sequence: chỉ lần helper trong **cùng** service, sâu 2 tầng. Gọi chéo service khác chỉ hiện
  ở tầng service, không đi sâu.
- ERD: quan hệ suy từ navigation property + FK naming. Bảng nối n-n không có navigation
  sẽ bị bỏ sót — đối chiếu `docs/init/15_DATABASE_SCHEMA.md` để chắc.
