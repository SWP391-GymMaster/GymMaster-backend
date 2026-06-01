# 11 — AI Workflow

> Quy trình làm việc với AI theo mô hình **Hybrid SDD + ADD** (sách Ch.13). Mục tiêu: AI tăng tốc nhưng spec & người vẫn là "nguồn chân lý".

---

# 1. Vòng đời ADD 4 pha cho mỗi feature
| Pha | Tên | Việc người làm | Việc AI làm | Output |
|---|---|---|---|---|
| B1 | **Spec** | Viết/duyệt spec (04/06) | Soạn nháp EARS, gợi error case | Spec Approved |
| B2 | **Plan** | Duyệt plan | Đề xuất task list, file ảnh hưởng | Task breakdown (08) |
| B3 | **Implement** | Review từng bước | Sinh code theo spec + convention | Code + test |
| B4 | **Validate** | Tick Validation Gate (09) | Chạy test, tự rà spec | Merge / quay lại |

> Quy tắc vàng: **"Sai ở đâu, sửa ở Spec đó"** — code lệch acceptance thì kiểm tra & sửa Spec trước, rồi mới regenerate.

# 2. Quy trình theo phase dự án
- **Phase 0 (Foundation):** dùng AI soạn nháp spec/constitution → người chốt. SDD nặng.
- **Phase 1–3 (Build):** mỗi feature chạy vòng B1→B4. Hybrid.
- **Phase 4 (Polish):** AI hỗ trợ viết test, tìm bug, refactor; người kiểm soát security.

# 3. Kỹ thuật prompt nên dùng (chi tiết ở `14`)
- **Clarification-First:** "Liệt kê mọi điểm chưa rõ trước khi code."
- **Plan-then-Execute:** "Trình bày plan, đợi tôi duyệt rồi mới code."
- **Spec-Compliance:** "Chỉ implement đúng các SHALL trong spec này, không thêm."
- **Shadowing/Self-review:** "Đóng vai reviewer, tự tìm lỗi spec-violation trong code bạn vừa viết."

# 4. AI Interaction Log (template ghi lại mỗi lần dùng AI)
| # | Ngày | Task | Prompt tóm tắt | Model | Kết quả | Người verify | Ghi chú |
|---|---|---|---|---|---|---|---|
| 1 | 2026-05-30 | Soạn docs | "Bổ sung 15 file theo sách" | Opus 4.8 | Đã tạo/khôi phục | minhbao | — |
| 2 | 2026-05-30 | Đổi DB | "Chốt dùng SQL Server" | Opus 4.8 | Đã đổi toàn bộ docs | minhbao | Đảo ADR-02, xem D-17 |

# 5. Ranh giới an toàn khi dùng AI
- KHÔNG dán secret/connection string thật vào prompt.
- KHÔNG để AI tự quyết business rule chưa chốt.
- KHÔNG merge code AI mà chưa qua Validation Gate (09 §6).
- AI Vision (UC-26) **chỉ gợi ý**, người xác nhận trước khi lưu.

# 6. Đo hiệu quả (optional)
Theo dõi: % task AI-assisted pass review lần 1, số lần phải sửa spec, thời gian/feature. Dùng để cải thiện prompt library.
