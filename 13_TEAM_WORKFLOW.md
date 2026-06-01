# 13 — Team Workflow

> Quy trình làm việc nhóm 5 người, sprint 2 tuần, kết hợp Git + Validation Gate (sách Ch.13/16).

---

# 1. Git Workflow
- Nhánh chính: `main` (luôn deploy được), `develop` (tích hợp).
- Nhánh feature: `feature/<epic>-<mô-tả-ngắn>` (vd `feature/EP05-sell-membership`).
- Nhánh fix: `fix/<mô-tả>`, hotfix: `hotfix/<mô-tả>`.
- **Không commit thẳng vào `main`/`develop`** — luôn qua PR.
- Commit theo **Conventional Commits**: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`.

# 2. Pull Request Rules
- PR nhỏ (≤ ~400 dòng đổi nếu được).
- Mô tả PR gồm: spec ref (UC/FR/F), tóm tắt thay đổi, cách test, screenshot nếu UI.
- **≥1 approval** mới merge; CI phải xanh.
- Reviewer dùng checklist Pre-commit (`10` §4) + Validation Gate (`09` §6).
- Squash merge để lịch sử gọn.

# 3. Branch Protection / CI
| Gate | Yêu cầu |
|---|---|
| Build | `dotnet build` pass |
| Test | `dotnet test` pass, coverage phần mới ≥80% |
| Lint | 0 error |
| Secret scan | gitleaks: không secret |
| Review | ≥1 approval |

# 4. Phân vai (gợi ý cho team 5 người)
| Vai | Trách nhiệm chính |
|---|---|
| Team Lead / BA | Spec, backlog, duyệt PR, giữ CONSTITUTION |
| Backend Dev x2 | API, service, EF migration, test |
| Frontend Dev | Next.js UI, tích hợp API |
| QA / Tester | Test plan (09), UAT, defect log |

*(Vai có thể kiêm nhiệm; mọi người đều review được.)*

# 5. Ceremonies (sprint 2 tuần)
| Sự kiện | Khi nào | Mục đích |
|---|---|---|
| Sprint Planning | Đầu sprint | Chọn task từ backlog (08), xác nhận DoR |
| Daily standup | Mỗi ngày 15' | Hôm qua / hôm nay / blocker |
| Backlog refinement | Giữa sprint | Làm rõ spec, ước lượng SP |
| Sprint Review/Demo | Cuối sprint | Demo theo acceptance criteria |
| Retrospective | Cuối sprint | Cải thiện quy trình + prompt library |

# 6. Định nghĩa hoàn thành chung
Task chỉ "Done" khi qua **DoD (08 §8)** + **Validation Gate 4 lớp (09 §6)**. PT vận hành: "spec sai sửa spec trước, code sau".

# 7. Quản lý spec & đồng bộ
- Mọi thay đổi business rule → cập nhật spec (03/04/06) + Decision Log (12) **trong cùng PR**.
- Tránh Context Amnesia: trước khi build feature, đọc `CLAUDE.md` + spec liên quan.
- File docs cũng review qua PR như code.
