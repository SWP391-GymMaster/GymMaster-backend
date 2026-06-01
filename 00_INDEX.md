# 00 — INDEX | GymMaster Documentation

**Phiên bản:** v1.0 | **Ngôn ngữ:** Tiếng Việt | **Trạng thái:** Spec hoàn chỉnh — 4 roles, SQL Server stack

Bộ tài liệu này là **SPEC chính** (source of truth) cho dự án GymMaster, soạn theo phương pháp **Hybrid SDD + ADD** (Spec-Driven & Agent-Driven Development).

## Thứ tự đọc khuyến nghị
1. `CONSTITUTION.md` — luật bất biến của dự án (đọc TRƯỚC khi code).
2. `CLAUDE.md` + `10_AGENTS.md` — ngữ cảnh & quy tắc cho AI agent.
3. `01_CONTEXT.md` → `08_TASKS_BACKLOG.md` — tầng SDD (đọc tuần tự).
4. `09`–`15` — test, workflow, decision, prompt, schema (tra cứu khi cần).

## Danh mục file
| File | Nội dung | Tầng |
|---|---|---|
| `CONSTITUTION.md` | Luật 3 lớp (Hard/Arch/Eng) + AI policy | Core |
| `CLAUDE.md` | Bộ nhớ ngữ cảnh dự án cho AI | Core |
| `01_CONTEXT.md` | Problem, goals, stakeholders, glossary, assumptions | SDD |
| `02_PRODUCT_SCOPE.md` | MVP, core/secondary features, out-of-scope | SDD |
| `03_SRS_USE_CASES.md` | Actors, use cases, chi tiết UC | SDD |
| `04_REQUIREMENTS.md` | Functional (EARS) + Non-functional | SDD |
| `05_DATABASE_SPEC.md` | Bảng, quan hệ, ưu tiên dữ liệu | SDD |
| `06_FEATURE_SPECS.md` | Spec 8 thành phần cho module core | SDD |
| `07_ROADMAP_RELEASES.md` | Phase, timeline, story point, release | SDD |
| `08_TASKS_BACKLOG.md` | Epic, task, DoR/DoD | SDD/ADD |
| `09_TEST_PLAN.md` | Test strategy, cases, UAT, defect log | QA |
| `10_AGENTS.md` | Persona + rules cho AI/coding agents | ADD |
| `11_AI_WORKFLOW.md` | Workflow dùng AI cho team (Hybrid) | ADD |
| `12_DECISION_LOG.md` | Decision log / ADR | ADD |
| `13_TEAM_WORKFLOW.md` | Git flow, PR, ceremony, review | ADD |
| `14_PROMPT_LIBRARY.md` | Prompt mẫu cho BA, DB, code, test | ADD |
| `15_DATABASE_SCHEMA.md` | SQL Server + EF Core schema chi tiết | SDD |

## Quy ước trạng thái Spec
`Draft` → `Review` → `Approved` → `Implemented`. Chỉ code khi spec ở trạng thái **Approved**.
