# GymMaster — Documentation

**Phiên bản:** v2.0 · **Ngôn ngữ:** Tiếng Việt · **Cập nhật:** 2026-07-23
**Phương pháp:** Hybrid **SDD + ADD** (Spec-Driven & Agent-Driven Development)
**Trạng thái:** 10/10 feature `Implemented` — backend .NET 10 + FE Next.js đang chạy trên Google Cloud Run

> **Nguyên tắc số một: tài liệu phải khớp code.** Mọi đường dẫn file, tên hàm và số dòng trong bộ tài liệu này đều tham chiếu code có thật, kiểm chứng được. Khi tài liệu và code lệch nhau, **code đúng** → sửa tài liệu, đừng sửa code cho khớp tài liệu.
>
> **Phương pháp lập tài liệu.** Dự án áp dụng Hybrid SDD + ADD (xem [`ai-workflow.md`](06-Management/ai-workflow.md)). Bộ `spec.md` được đồng bộ với code hiện hành ở mốc 2026-07-15; `plan.md`/`tasks.md` ghi lại kiến trúc **as-built** của phần đã triển khai. Việc sử dụng AI trong quá trình làm tài liệu và code được ghi nhận theo AI Interaction Log (`ai-workflow.md` §4) và AI Usage Report của môn học.

---

## Cấu trúc

```text
docs/
├── 01-SRS-Requirements/
│   ├── constraints/          global.md · business.md · safety.md   ← luật cho agent
│   ├── use-cases/            srs-use-cases.md
│   ├── context.md · product-scope.md · requirements.md · roadmap-releases.md
├── 02-SDD-Architecture/
│   ├── database-design/      database-spec.md · database-schema.md
│   └── feat_flow/            phân tích luồng từng feature
├── 03-Interface-Specs/
│   ├── api-postman/
│   └── feature-specs/        001…010/{spec,plan,tasks}.md · BACKLOG.md
├── 04-Test-Specs/            test-plan.md · test-report-minh.md
├── 05-Deployment/            deploy-gcp.md
├── 06-Management/            agents · ai-workflow · decision-log · team-workflow · prompt-library
└── archive/                  bản cũ đã bị thay thế (giữ làm lịch sử)
```

> **Vì sao tên file không còn số?** Bộ tài liệu cũ đánh số phẳng `00→15` trong `docs/init/`. Khi tách ra 6 tầng, các số đó phân tán mỗi nơi một ít (03 sang `use-cases/`, 05 và 15 sang `database-design/`, 06 và 08 vào `archive/`…) → dãy số còn lại bị khuyết, nhìn rời rạc mà không còn mang thông tin gì. Nay đổi sang tên mô tả, thứ tự do **số của thư mục tầng** (`01-`…`06-`) đảm nhiệm. Bảng đối chiếu tên cũ ↔ mới ở [cuối trang](#bảng-đối-chiếu-tên-file-cũ--mới).

## 01 — SRS & Requirements

| File | Nội dung |
|---|---|
| [context.md](01-SRS-Requirements/context.md) | Vấn đề, mục tiêu, stakeholder, glossary, giả định |
| [product-scope.md](01-SRS-Requirements/product-scope.md) | MVP, feature core/secondary, out-of-scope |
| [use-cases/srs-use-cases.md](01-SRS-Requirements/use-cases/srs-use-cases.md) | 5 actor, 30 use case — **nguồn sinh Use Case diagram + RDS** |
| [requirements.md](01-SRS-Requirements/requirements.md) | Yêu cầu chức năng (EARS) + phi chức năng |
| [roadmap-releases.md](01-SRS-Requirements/roadmap-releases.md) | Phase, timeline, story point, release |

### constraints/ — luật để agent làm việc có khuôn

| File | Prefix | Nội dung |
|---|---|---|
| [constraints/global.md](01-SRS-Requirements/constraints/global.md) | `GBL-*` | Ràng buộc kỹ thuật **đang có hiệu lực trong code** (giờ VN, single-source business rule, vertical slice, không background job…) + ghi nhận 2 chỗ `CONSTITUTION.md` lệch code |
| [constraints/business.md](01-SRS-Requirements/constraints/business.md) | `BIZ-*` | 17 luật nghiệp vụ bất biến (1 Active membership/member, 1 PT/member, TTL 30 phút, role bất biến, tier free 20 món…), mỗi luật kèm **vị trí thi hành trong code** |
| [constraints/safety.md](01-SRS-Requirements/constraints/safety.md) | `SAFE-*` | 14 luật an toàn dữ liệu · AI · dịch vụ ngoài (audit append-only, snapshot lịch sử, không tin client cho tiền, AI không tự quyết…) |

> **Thứ tự ưu tiên khi xung đột:** [`CONSTITUTION.md`](../CONSTITUTION.md) (luật gốc, `SEC-*`/`ARCH-*`/`DATA-*`/`AUDIT-*`) → `constraints/` (`GBL-*`/`BIZ-*`/`SAFE-*`) → Design Decisions cấp feature trong `plan.md` (`D-xxx`).
>
> Mục **§3 Constitution Check** của mỗi `plan.md` đối chiếu feature với đúng tập ID này — nên mọi lời khẳng định "tuân thủ" đều tra ngược được về một điều luật có thật.

## 02 — SDD & Architecture

| File | Nội dung |
|---|---|
| [system-design/system-overview.md](02-SDD-Architecture/system-design/system-overview.md) | **Kiến trúc tổng thể** — 10 slice, bản đồ phụ thuộc, vòng đời request, 2 điểm chạm nguy hiểm, kỷ luật ranh giới slice |
| [database-design/database-spec.md](02-SDD-Architecture/database-design/database-spec.md) | Bảng, quan hệ, ưu tiên dữ liệu |
| [database-design/database-schema.md](02-SDD-Architecture/database-design/database-schema.md) | Schema SQL Server + EF Core chi tiết (24 bảng) — **nguồn sinh ERD + SDS** |
| [deliverables/GymMaster_SDS_v1.1.docx](02-SDD-Architecture/deliverables/GymMaster_SDS_v1.1.docx) | Bản SDS nộp môn (.docx) |
| [feat_flow/](02-SDD-Architecture/feat_flow/README.md) | Phân tích luồng từng feature — *đang trống có chủ ý*, `README` giải thích khi nào mới nên tạo (tránh trùng `plan.md` §6) |

> **Ba mức kiến trúc, đừng lẫn:** *tổng thể* → `system-design/` · *từng feature* → `plan.md` của feature trong `03-Interface-Specs/` · *triển khai* → [`05-Deployment/deploy-diagram.md`](05-Deployment/deploy-diagram.md).

## 03 — Interface Specs

**[feature-specs/](03-Interface-Specs/feature-specs/README.md)** — 10 feature, mỗi feature có **3 tài liệu**:

| File | Tầng | Trả lời |
|---|---|---|
| `spec.md` | SDD | Làm **cái gì** — 9 thành phần, EARS, acceptance criteria |
| `plan.md` | SDD/ADD | Làm **bằng cách nào** — kiến trúc, Design Decisions, Constitution Check, traceability FR→code |
| `tasks.md` | ADD | Chia việc — phase, trạng thái, truy vết AC |

| # | Feature | # | Feature |
|---|---|---|---|
| [001](03-Interface-Specs/feature-specs/001-auth-rbac/spec.md) | Authentication & RBAC | [006](03-Interface-Specs/feature-specs/006-progress-tracking/spec.md) | Progress Tracking & 360° |
| [002](03-Interface-Specs/feature-specs/002-member-management/spec.md) | User/Staff/PT/Member Management | [007](03-Interface-Specs/feature-specs/007-nutrition-calorie/spec.md) | Meal Journal & Calorie |
| [003](03-Interface-Specs/feature-specs/003-membership-billing/spec.md) | Membership, Sell, Renew & Payment | [008](03-Interface-Specs/feature-specs/008-dashboard-audit/spec.md) | Dashboard & Audit Log |
| [004](03-Interface-Specs/feature-specs/004-checkin/spec.md) | Check-in | [009](03-Interface-Specs/feature-specs/009-image-food-recognition/spec.md) | Image Food Recognition (AI) |
| [005](03-Interface-Specs/feature-specs/005-pt-training/spec.md) | PT Assignment, Workout & Notes | [010](03-Interface-Specs/feature-specs/010-online-payment-vnpay/spec.md) | Online Payment VNPay |

📌 **[BACKLOG.md](03-Interface-Specs/feature-specs/BACKLOG.md)** — 20 việc còn nợ, xếp P1→P5, mỗi mục có file path + điều kiện hoàn thành. Đây là **input chạy được cho agent** ở pha B3.

## 04 — Test Specs

| File | Nội dung |
|---|---|
| [test-plan.md](04-Test-Specs/test-plan.md) | Chiến lược test, test case, UAT, defect log, **Validation Gate** |
| [test-report-minh.md](04-Test-Specs/test-report-minh.md) | Báo cáo test |

> Test tự động: **15 file xUnit** ở `tests/GymMaster.Api.Tests/` + black-box PowerShell ở `tests/blackbox/`. Độ phủ theo từng acceptance criteria xem mục "Truy vết AC" cuối mỗi `tasks.md`.

## 05 — Deployment

| File | Nội dung |
|---|---|
| [README.md](05-Deployment/README.md) | Index của tầng — tóm tắt hạ tầng, deploy 1 phút, bảng bẫy hay gặp |
| [deploy-gcp.md](05-Deployment/deploy-gcp.md) | Dựng hạ tầng lần đầu: Cloud SQL, nạp schema, deploy, nối FE↔BE, setup Workload Identity Federation |
| [docker.md](05-Deployment/docker.md) | `backend/Dockerfile` multi-stage, `.dockerignore`, build/chạy container ở máy |
| [ci-cd.md](05-Deployment/ci-cd.md) | 2 workflow GitHub Actions — `ci.yml` (build·test·CVE) và `deploy.yml` (bấm tay) |
| [deploy-diagram.md](05-Deployment/deploy-diagram.md) | Sơ đồ kiến trúc + sequence từ code tới production, bảng env vars, Local ↔ Cloud Run |

## 06 — Management

| File | Nội dung |
|---|---|
| [agents.md](06-Management/agents.md) | Persona + rule cho AI/coding agent |
| [ai-workflow.md](06-Management/ai-workflow.md) | **Vòng đời ADD 4 pha B1→B4**, prompt technique, AI Interaction Log |
| [decision-log.md](06-Management/decision-log.md) | ADR D-01→D-23 + **bản đồ ADR ↔ Design Decisions cấp feature** |
| [team-workflow.md](06-Management/team-workflow.md) | Git flow, PR, ceremony, review |
| [prompt-library.md](06-Management/prompt-library.md) | Prompt mẫu cho BA, DB, code, test |
| [danh-sach-feature.md](06-Management/danh-sach-feature.md) | *(sinh tự động)* 46 màn hình FE · 85 endpoint BE · 10 feature |
| [phan-cong.md](06-Management/phan-cong.md) · [phan-cong-ve-anh.md](06-Management/phan-cong-ve-anh.md) | *(sinh tự động)* Phân công 5 người |
| [phan-cong-sds.md](06-Management/phan-cong-sds.md) | Phân công viết SDS |

## archive/

Bản cũ **đã bị thay thế**, giữ lại làm dấu vết quá trình thiết kế — **không phải tài liệu hiện hành**:

| File | Bị thay thế bởi |
|---|---|
| [06_FEATURE_SPECS.md](archive/06_FEATURE_SPECS.md) | `03-Interface-Specs/feature-specs/*/spec.md` — đầy đủ hơn (9 thành phần vs 8), đã đồng bộ code |
| [08_TASKS_BACKLOG.md](archive/08_TASKS_BACKLOG.md) | `03-Interface-Specs/feature-specs/BACKLOG.md` |
| `MANIFEST.md` · `CHANGELOG_vs_old_spec.md` · `DB_DIFF_FOR_DBTEAM.md` · `GymMaster_Full_Docs_Review_Pack.md` | — (tài liệu giai đoạn chuyển đổi) |

---

## Thứ tự đọc khuyến nghị

1. [`../CONSTITUTION.md`](../CONSTITUTION.md) — luật bất biến của dự án (**đọc trước khi code**)
2. [`../CLAUDE.md`](../CLAUDE.md) + [`06-Management/agents.md`](06-Management/agents.md) — ngữ cảnh & quy tắc cho AI agent
3. `01-SRS-Requirements/` — `context.md` → `product-scope.md` → `use-cases/` → `requirements.md` → `constraints/` — hiểu bài toán và luật chơi
4. `03-Interface-Specs/feature-specs/<feature>/` — spec → plan → tasks của feature đang làm
5. `04`–`06` — tra cứu khi cần

## Hybrid SDD + ADD — hai tầng nối nhau thế nào

| Pha ([ai-workflow.md](06-Management/ai-workflow.md) §1) | Người | AI | Tài liệu chi phối |
|---|---|---|---|
| **B1 Spec** | duyệt spec | soạn nháp EARS, gợi error case | `feature-specs/*/spec.md` · `requirements.md` |
| **B2 Plan** | duyệt plan | đề xuất task + file ảnh hưởng | `feature-specs/*/plan.md` · `CONSTITUTION.md` |
| **B3 Implement** | review từng bước | sinh code theo spec | **`feature-specs/BACKLOG.md`** |
| **B4 Validate** | tick Validation Gate | chạy test, tự rà spec | `test-plan.md` §6 · mục "Truy vết AC" |

**Quy tắc vàng**: *"Sai ở đâu, sửa ở Spec đó"* — code lệch acceptance criteria thì sửa `spec.md` trước, rồi mới regenerate.

**Phân vai giữa hai nơi**: `01`/`02`/`06` nói **cấp dự án**; `03-Interface-Specs/` nói **cấp feature**. Khi mâu thuẫn: `03` đúng hơn về **hành vi code hiện tại**, `01`/`06` đúng hơn về **ý định và quy trình**.

## Quy trình sinh tài liệu từ code

Tài liệu trong repo này **sinh ngược từ code thật**, không viết tay từ đầu — nhờ vậy mới giữ được cam kết "khớp code". Chạy từ thư mục gốc `GymMaster-backend/`:

```bash
PY=$(cat graphify-out/.graphify_python)

# 1. Kiểm kê màn hình + endpoint quét từ code
$PY .claude/skills/swp391-docs/scripts/inventory.py --csv out/inventory.csv

# 2. Sinh tài liệu quản lý (ghi đè, không sửa tay)
$PY .claude/skills/swp391-docs/scripts/gen_list.py        # -> 06-Management/danh-sach-feature.md
$PY .claude/skills/swp391-docs/scripts/gen_phancong.py    # -> 06-Management/phan-cong.md

# 3. Sinh diagram từ Entities/*.cs và srs-use-cases.md
$PY .claude/skills/swp391-diagrams/scripts/erd_mermaid.py    out/erd.mmd
$PY .claude/skills/swp391-diagrams/scripts/class_mermaid.py  package out/packages.mmd
$PY .claude/skills/swp391-diagrams/scripts/usecase_drawio.py out/UseCase.drawio

# 4. Sinh tài liệu nộp môn RDS/SDS -> out/docs/     [out/ bị gitignore]
```

Skill tương ứng: `/swp391-docs` (tài liệu môn học) · `/swp391-diagrams` (sơ đồ) · `/graphify` (hỏi đáp về codebase).

**Feature mới** làm đúng chiều xuôi bằng Spec Kit: `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`. Thư mục sẽ tự tạo dưới `docs/03-Interface-Specs/feature-specs/011-<tên>/`.

---

## Bảng đối chiếu tên file cũ ↔ mới

Tái cấu trúc 2026-07-23: bỏ `docs/init/`, bỏ số thứ tự phẳng, chuyển `specs/` vào `docs/`.

| Tên cũ | Vị trí mới |
|---|---|
| `docs/init/00_INDEX.md` | `docs/README.md` *(file này)* |
| `docs/init/01_CONTEXT.md` | `01-SRS-Requirements/context.md` |
| `docs/init/02_PRODUCT_SCOPE.md` | `01-SRS-Requirements/product-scope.md` |
| `docs/init/03_SRS_USE_CASES.md` | `01-SRS-Requirements/use-cases/srs-use-cases.md` |
| `docs/init/04_REQUIREMENTS.md` | `01-SRS-Requirements/requirements.md` |
| `docs/init/05_DATABASE_SPEC.md` | `02-SDD-Architecture/database-design/database-spec.md` |
| `docs/init/06_FEATURE_SPECS.md` | `archive/` — thay bằng `03-Interface-Specs/feature-specs/*/spec.md` |
| `docs/init/07_ROADMAP_RELEASES.md` | `01-SRS-Requirements/roadmap-releases.md` |
| `docs/init/08_TASKS_BACKLOG.md` | `archive/` — thay bằng `03-Interface-Specs/feature-specs/BACKLOG.md` |
| `docs/init/09_TEST_PLAN.md` | `04-Test-Specs/test-plan.md` |
| `docs/init/10_AGENTS.md` | `06-Management/agents.md` |
| `docs/init/11_AI_WORKFLOW.md` | `06-Management/ai-workflow.md` |
| `docs/init/12_DECISION_LOG.md` | `06-Management/decision-log.md` |
| `docs/init/13_TEAM_WORKFLOW.md` | `06-Management/team-workflow.md` |
| `docs/init/14_PROMPT_LIBRARY.md` | `06-Management/prompt-library.md` |
| `docs/init/15_DATABASE_SCHEMA.md` | `02-SDD-Architecture/database-design/database-schema.md` |
| `specs/` *(thư mục gốc)* | `03-Interface-Specs/feature-specs/` |
| `docs/DEPLOY_GCP.md` | `05-Deployment/deploy-gcp.md` |
| `docs/TEST_REPORT_MINH.md` | `04-Test-Specs/test-report-minh.md` |
| `docs/DANH_SACH_FEATURE.md` | `06-Management/danh-sach-feature.md` |
| `docs/PHAN_CONG.md` · `PHAN_CONG_VE_ANH.md` | `06-Management/phan-cong.md` · `phan-cong-ve-anh.md` |

**Mới thêm** (không có bản cũ): `01-SRS-Requirements/constraints/{global,business,safety}.md` · `03-Interface-Specs/feature-specs/BACKLOG.md` · `feature-specs/*/plan.md` + `tasks.md`.

Toolchain đã cập nhật theo: `.specify/scripts/` (Spec Kit), 7 script Python trong `.claude/skills/swp391-*`.
