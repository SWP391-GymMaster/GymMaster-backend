# SWP391 / ISP392 — luật chơi của môn học

Tóm tắt từ `Student Guides Document.pdf` (6 trang). Đọc file gốc trong `templates/` nếu cần chi tiết.

## Vòng đời dự án

| Phase | Thời lượng | Nội dung |
|---|---|---|
| **Initiation** | 1 iteration / 1 tuần (6 slot) | Lập nhóm, dựng môi trường, requirement + design tổng thể, làm POC — **mỗi thành viên ít nhất 1 screen** |
| **Construction** | 3 iteration × 2 tuần (12 slot) | Build phần mềm, viết doc, demo cho thầy chấm |
| **Closing** | 3 tuần (18 slot) | Hoàn thiện code + doc, final release, video demo, slide, thuyết trình |

Nhóm 4–6 người (chuẩn 5). Có team leader.

## Chấm điểm

| Mốc | Trọng số |
|---|---|
| Iteration 1 | 20% |
| Iteration 2 | 20% |
| Iteration 3 | 20% |
| **Final (Iter4)** | **40%** |

Trong mỗi mốc Iter1–3:
- **Release Package: 20%** — Project Tracking · SRS/RDS · SDS · Codes & DB Script · Issues Report
- **Team Co-ordinating & Presenting: 20%**
- **Individual Results: 60%** = tổng `LOC × Quality`, phạm vi iteration đó
  - LOC: **60** (simple) / **120** (medium) / **240** (complex) cho mỗi function hoàn thành
  - Quality: **100%** (high) / **75%** (medium) / **50%** (low) — thầy đánh giá
  - **≥180** để đạt tối đa

Ở Final (Iter4):
- **Final release package: 40%** — Final Project Tracking · Final Release Document · SRS · SDS ·
  Codes & DB Script · Issues Report · **Demo Video**
- **Individual Results: 40%** — phạm vi **cả dự án**, cần **≥720** để đạt tối đa
- **Team Co-ordinating & Presenting: 20%**

## Deliverable theo mốc

| Mốc | Nộp gì |
|---|---|
| **Iter1** | Tracking (sheet Product + Iter1) · RDS (overall + spec Iter1) · SDS (overall + design Iter1) |
| **Iter2–4** | Tracking (Product cập nhật + IterX) · RDS (**mới cho IterX + cập nhật iter trước**) · SDS (tương tự) · Issues Report |
| **Final** | Tracking (Product cuối, **thay đổi bôi ĐỎ** + sheet Iter5) · RDS cuối cả dự án · SDS cuối · Issues Report cả dự án · Final Release Document |

**RDS/SDS là tài liệu sống** — bồi qua từng iteration, không viết lại. *Record of Changes*
(Version · Date · A/M/D · In charge · Change Description) là bằng chứng làm đều.

## Slide thuyết trình (≤ 15 slide)

- Giới thiệu dự án: thành viên, sản phẩm
- **Use case diagram cho TỪNG Role/Actor**
- Application Design: package diagram, database schema & design, UI design
- Actual screen flow cho các luồng chính
- Kết quả: Done, Not done, **Lessons Learnt**

## Video demo

Giới thiệu các luồng chính (workflow) của ứng dụng. Mỗi luồng nêu mục đích + chức năng của
từng màn hình và cách chúng tích hợp với nhau.

## Điểm vênh với GymMaster — phải hỏi thầy

1. **GitLab vs GitHub** — guide bắt *"All the submits to the teacher need to baselined/tagged
   via GitLab"*, Issues Report mẫu cũng có URL `gitlab.com`. Dự án đang ở **GitHub**.
2. **Oracle Java Coding Standards** — dự án là **C#/.NET 10**.
3. **MySQL naming convention** (ghi trong SDS) — dự án dùng **SQL Server**.
4. **NetBeans 8.2** (ghi trong Final Release: *"be able to open & run your source codes with
   the NetBeans 8.2"*) — dự án cần .NET SDK + Node.
5. **MD5** trong business rule mẫu — dự án dùng **BCrypt cost 12**. Không được chép mẫu.
6. Guide gọi **"Template2_SRS Document"**, file thật là **"Template2_RDS Document"**
   (Requirement **& Design** Specification — gộp cả requirement lẫn design).

## Tài sản GymMaster đã có sẵn

| Template cần | Đã có ở |
|---|---|
| Actors | `docs/01-SRS-Requirements/use-cases/srs-use-cases.md` §1 (5 actor) |
| Use Cases | `docs/01-SRS-Requirements/use-cases/srs-use-cases.md` §2 (30 UC) |
| Requirements | `docs/01-SRS-Requirements/requirements.md` (EARS notation) |
| DB Schema | `docs/02-SDD-Architecture/database-design/database-schema.md` (24 bảng) |
| Feature specs | `docs/03-Interface-Specs/feature-specs/001-010/spec.md` (Given-When-Then, error code) |
| Decision log | `docs/06-Management/decision-log.md` (D-01→D-23) |
| Test plan | `docs/04-Test-Specs/test-plan.md` |
| Package/class map | `graphify-out/graph.html` + `GRAPH_REPORT.md` |
| Screens flow | `../GymMaster-frontend/docs/design/25_SCREEN_FLOW.md`, `26_SWIMLANE_MAIN_FLOWS.md` |
| Route map | `../GymMaster-frontend/docs/design/08_ROUTE_MAP_NAVIGATION.md` |
| SQL script | `database/*.sql` |
