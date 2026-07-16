# 08 — Tasks Backlog

# GymMaster — Backlog & Task Plan

---

# 1. Epic Overview
| Epic ID | Epic | Phase |
|---|---|---|
| EP-01 | Documentation & Planning | Phase 0 |
| EP-02 | Project Setup | Phase 0 |
| EP-03 | Authentication | Phase 1 |
| EP-04 | User/Staff/Member/PT Management | Phase 1 |
| EP-05 | Membership & Payment | Phase 1 |
| EP-06 | Check-in | Phase 1 |
| EP-07 | PT Assignment | Phase 1 |
| EP-08 | Workout & Notes | Phase 2 |
| EP-09 | Progress Tracking | Phase 2 |
| EP-10 | Calorie Tracking | Phase 2 |
| EP-11 | Dashboard & Audit Log | Phase 3 |
| EP-12 | Testing & Final Demo | Phase 3 |
| EP-13 | Image Food Recognition Assist | Enhancement |

---

# 2. Phase 0 Tasks
| Task ID | Task | Owner | SP | Spec Ref | Status |
|---|---|---|---:|---|---|
| T-001 | Review & approve product scope | TBD | 3 | 02 | Todo |
| T-002 | Decide roles: 4 roles | Team | 2 | 12/D-01 | Done |
| T-003 | Decide tech stack | Team | 5 | 12/D-02..06 | Done |
| T-004 | Finalize use case list | TBD | 5 | 03 | Todo |
| T-005 | Draft ERD | TBD | 5 | 05,15 | Todo |
| T-006 | Create GitHub repo + branch policy | TBD | 2 | 13 | Todo |
| T-007 | Setup solution skeleton (layered) | TBD | 5 | CLAUDE | Todo |
| T-008 | Initial wireframes | TBD | 8 | 02 | Todo |
| T-009 | Setup CI (build+test+lint) | TBD | 3 | 13 | Todo |
| T-010 | Seed data demo | TBD | 2 | 05 | Todo |

---

# 3. Phase 1 Tasks
| Task ID | Task | Owner | SP | Spec Ref | Status |
|---|---|---|---:|---|---|
| T-101 | Login/logout + JWT + refresh token | TBD | 8 | 04 A1, F? | Todo |
| T-102 | Role-based navigation + RBAC middleware | TBD | 5 | FR-RBAC-01/02 | Todo |
| T-103 | User & Staff management | TBD | 8 | UC-03/03A | Todo |
| T-104 | Member management + search | TBD | 8 | FR-MEM-01..03 | Todo |
| T-105 | PT management | TBD | 5 | UC-05 | Todo |
| T-106 | Package management | TBD | 8 | FR-PKG-01 | Todo |
| T-107 | Sell package flow + tính EndDate | TBD | 10 | F1, FR-MS-01 | Todo |
| T-108 | Renewal flow | TBD | 8 | FR-MS-03 | Todo |
| T-109 | Check-in flow + verify membership | TBD | 8 | F2, FR-CHK-01/02 | Todo |
| T-110 | PT assignment (1 active) | TBD | 8 | F3, FR-PT-01/02 | Todo |
| T-111 | Audit log service | TBD | 5 | FR-AUD-01 | Todo |

---

# 4. Phase 2 Tasks
| Task ID | Task | Owner | SP | Spec Ref | Status |
|---|---|---|---:|---|---|
| T-201 | Member 360° profile | TBD | 10 | UC-14 | Todo |
| T-202 | PT assigned member list | TBD | 5 | UC-11 | Todo |
| T-203 | Workout plan | TBD | 10 | FR-WP-01 | Todo |
| T-204 | Workout exercises | TBD | 8 | FR-WP-02 | Todo |
| T-205 | Trainer notes | TBD | 8 | FR-NOTE-01 | Todo |
| T-206 | Progress tracking | TBD | 10 | FR-PROG-01 | Todo |
| T-207 | Calorie target | TBD | 5 | FR-CAL? | Todo |
| T-208 | Food database | TBD | 8 | 05 | Todo |
| T-209 | Add custom food | TBD | 5 | S-01 | Todo |
| T-210 | Meal journal | TBD | 8 | F4, FR-MEAL-01 | Todo |
| T-211 | Daily calorie summary | TBD | 5 | FR-CAL-01 | Todo |
| T-212 | Calorie history | TBD | 5 | UC-21 | Todo |

---

# 5. Phase 3 Tasks
| Task ID | Task | Owner | SP | Spec Ref | Status |
|---|---|---|---:|---|---|
| T-301 | Revenue dashboard | TBD | 8 | F5 | Todo |
| T-302 | Payment status dashboard | TBD | 5 | FR-DASH-02 | Todo |
| T-303 | Check-in statistics | TBD | 5 | FR-DASH-04 | Todo |
| T-304 | Audit log screen | TBD | 5 | FR-AUD-02 | Todo |
| T-305 | Write test cases | TBD | 8 | 09 | Todo |
| T-306 | UAT checklist | TBD | 5 | 09 | Todo |
| T-307 | Seed data | TBD | 5 | 05 | Todo |
| T-308 | Demo script | TBD | 5 | 09 | Todo |
| T-309 | Fix bugs from UAT | TBD | 8 | 09 | Todo |
| T-310 | Final deployment | TBD | 5 | 07 | Todo |

---

# 6. Enhancement Tasks — Image Food Recognition Assist
*(Chỉ làm sau khi core + secondary ổn định.)*
| Task ID | Task | SP | Status |
|---|---|---:|---|
| T-401 | Research recognition service | 3 | Todo |
| T-402 | Design meal image upload UI | 5 | Todo |
| T-403 | Image upload metadata | 5 | Todo |
| T-404 | Suggestion response parser | 8 | Todo |
| T-405 | Map suggestions to FoodItems | 8 | Todo |
| T-406 | User confirmation/edit step | 8 | Todo |
| T-407 | Fallback to manual log | 5 | Todo |
| T-408 | Test success/fail/manual fallback | 5 | Todo |

---

# 7. Definition of Ready (DoR)
- [ ] Mô tả rõ + có spec ref (file + section) ở trạng thái Approved.
- [ ] Có acceptance criteria testable.
- [ ] Có owner + priority.
- [ ] Dependencies đã xong/xác định.
- [ ] Không còn open question blocker.

# 8. Definition of Done (DoD)
- [ ] Code chạy được local, `dotnet build` + lint pass, không TODO.
- [ ] Có validation input chính; happy path + ≥1 error case được test.
- [ ] Unit test business logic, coverage ≥80% phần mới.
- [ ] **EARS requirement liên quan được implement** (gắn tag `// FR-...` trong code).
- [ ] Không hardcode secret/PII trong code & log.
- [ ] Swagger cập nhật cho endpoint mới; AuditLog cho action mutating quan trọng.
- [ ] Commit rõ ràng (Conventional Commits); screenshot/video nếu là UI.
- [ ] PR review pass (≥1 approval); cập nhật docs nếu đổi business rule.

> **Quy tắc (sách Ch.6):** mỗi task ≤ 4h. Nếu > 4h → chia nhỏ. "Sai ở đâu sửa ở Spec đó" — nếu code lệch acceptance, kiểm tra spec trước khi sửa code.
