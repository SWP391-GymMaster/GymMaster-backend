# 07 — ROADMAP & RELEASES

**Status:** Approved | **Mô hình:** Hybrid SDD+ADD, sprint 2 tuần | **Team:** 5 người

## 1. Phase tổng quan
| Phase | Tuần | Trọng tâm | Phương pháp |
|---|---|---|---|
| Phase 0 — Foundation | 1–2 | Constitution, AGENTS/CLAUDE, spec, scaffold | SDD |
| Phase 1 — Core Ops | 3–6 | Auth, Member, Package, Membership, Payment, Check-in | Hybrid |
| Phase 2 — Training | 7–9 | PT Assignment, Workout, Note, Progress, 360 Profile | ADD |
| Phase 3 — Nutrition+Dashboard | 10–12 | Meal Journal, Calorie, Dashboard, Audit | ADD |
| Phase 4 — Polish & Deliver | 13–15 | Test, security, deploy, docs, demo | Hybrid |

## 2. Release plan
| Version | Mốc | Nội dung |
|---|---|---|
| v0.1 | Tuần 2 | Scaffold + auth chạy được, DB migration đầu |
| v0.2 | Tuần 6 | Core operation: bán gói, payment, check-in, dashboard sơ bộ |
| v0.3 | Tuần 9 | Training module đầy đủ + Member 360 |
| v0.4 | Tuần 12 | Nutrition + dashboard hoàn chỉnh + audit |
| v1.0 | Tuần 15 | Bản demo: test ≥80%, security clean, deploy staging |

## 3. Sprint breakdown & story points
| Sprint | Tuần | Mục tiêu | SP ước tính |
|---|---|---|---|
| S0 | 1–2 | Foundation + scaffold | 13 |
| S1 | 3–4 | Auth + Member + Package | 21 |
| S2 | 5–6 | Membership + Payment + Check-in + Dashboard sơ bộ | 21 |
| S3 | 7–8 | PT Assignment + Workout Plan + Note | 21 |
| S4 | 9 | Progress + Member 360 Profile | 13 |
| S5 | 10–11 | Meal Journal + Calorie Summary | 18 |
| S6 | 12 | Dashboard hoàn chỉnh + Audit Log | 13 |
| S7 | 13–14 | Testing + Security + Performance | 18 |
| S8 | 15 | Deploy + Docs + Demo | 8 |

## 4. Milestone gate (không qua thì không sang phase sau)
| Milestone | Tuần | Tiêu chí pass |
|---|---|---|
| Spec Approved | 2 | 15 file + Constitution + CLAUDE đã Approved |
| Auth working | 4 | Login JWT + RBAC, coverage ≥80% module auth |
| Core ops done | 6 | Bán gói→payment→check-in→dashboard chạy end-to-end |
| Training done | 9 | PT flow + 360 profile pass acceptance |
| Feature complete | 12 | Toàn bộ MVP-01..15 pass acceptance |
| Security clean | 14 | Không Critical/High vuln; coverage ≥80% |
| Delivery | 15 | Staging deploy + report + slide + demo rehearsal |

## 5. Rủi ro & dự phòng
| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Scope creep (nutrition AI) | Cao | Giữ ENH-01 ngoài MVP; chỉ làm sau v1.0 |
| Tích hợp Azure/Vercel trễ | TB | Demo localhost backup; deploy sớm từ tuần 13 |
| Lệch spec↔code | Cao | Validation Gate mỗi PR; cập nhật spec trước khi đổi code |
| Thành viên chưa quen ASP.NET | TB | Pair + prompt library + skill docs |
