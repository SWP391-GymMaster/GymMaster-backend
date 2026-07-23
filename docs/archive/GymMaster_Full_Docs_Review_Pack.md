# GymMaster — Full Docs Review Pack

**Phiên bản:** v0.2-approved-stack  
**Ngôn ngữ:** Tiếng Việt  
**Trạng thái:** Updated — 4 roles & approved tech stack

---

## Mục tiêu

Bộ tài liệu này dùng làm bộ SPEC chính cho dự án GymMaster. Phiên bản này đã chốt mô hình 4 role gồm **Admin, Staff, PT, Member** và tech stack chính thức cho frontend, backend, database, authentication, deployment và testing.

---

## Cấu trúc file

| File | Nội dung |
|---|---|
| `docs/01-SRS-Requirements/01_CONTEXT.md` | Bối cảnh, problem statement, goals, stakeholders, assumptions |
| `docs/01-SRS-Requirements/02_PRODUCT_SCOPE.md` | MVP, core features, secondary features, out-of-scope |
| `docs/01-SRS-Requirements/use-cases/03_SRS_USE_CASES.md` | Actors, use cases, use case details |
| `docs/01-SRS-Requirements/04_REQUIREMENTS.md` | Functional và non-functional requirements |
| `docs/02-SDD-Architecture/05_DATABASE_SPEC.md` | Bảng chính/phụ, relationship, database priority |
| `docs/archive/06_FEATURE_SPECS.md` | Spec ngắn cho từng module core |
| `docs/01-SRS-Requirements/07_ROADMAP_RELEASES.md` | Phase, timeline, story point, release version |
| `docs/archive/08_TASKS_BACKLOG.md` | Epic, task backlog, Definition of Ready/Done |
| `docs/04-Test-Specs/09_TEST_PLAN.md` | Test strategy, test cases, UAT, defect log |
| `docs/06-Management/10_AGENTS.md` | Quy tắc cho AI/coding agents |
| `docs/06-Management/11_AI_WORKFLOW.md` | Workflow dùng AI cho cả team |
| `docs/06-Management/12_DECISION_LOG.md` | Decision log / ADR pending |
| `docs/06-Management/13_TEAM_WORKFLOW.md` | Git flow, PR, meeting, review |
| `docs/06-Management/14_PROMPT_LIBRARY.md` | Prompt mẫu cho BA, DB, code, test, report |
| `docs/02-SDD-Architecture/15_DATABASE_SCHEMA.md` | SQL Server + Entity Framework Core schema documentation |

---

## Trạng thái khuyến nghị hiện tại

| Hạng mục | Đề xuất |
|---|---|
| Product direction | Core Gym Operation & Member Progress |
| Nutrition | Manual Calorie Tracking & Meal Journal |
| Image Food Recognition Assist | Enhancement sau secondary: phân tích ảnh để gợi ý tên món/nguyên liệu, không tự định lượng calories |
| Barcode lookup | Secondary |
| Roles | Approved: 4 roles — Admin, Staff, PT, Member |
| Frontend | Next.js |
| Backend | C# / ASP.NET Core 10 Web API (.NET 10) |
| Database | SQL Server (Cloud SQL) |
| Deployment | **Google Cloud Run (FE + BE) + Cloud SQL** (đã đổi từ Vercel/Azure — 2026-07-15) |

---

## Technology Stack

| Layer | Công nghệ |
|---|---|
| Frontend | Next.js |
| Backend | C# / ASP.NET Core 10 Web API (.NET 10) |
| Database | SQL Server |
| ORM | Entity Framework Core 10 - Code First Migrations |
| Authentication | JWT Bearer Token + BCrypt |
| Token Policy | Access Token 15 phút, Refresh Token 7 ngày |
| AI Vision | **Google Gemini Vision** (`gemini-2.5-flash`) — đã đổi từ Google Cloud Vision |
| Online Payment | **VNPay** sandbox |
| File Storage (avatar) | **Cloudinary** — đã đổi từ Azure Blob |
| Email | SMTP (Gmail App Password) |
| Frontend Deploy | **Google Cloud Run** — đã đổi từ Vercel |
| Backend Deploy | **Google Cloud Run + Cloud SQL** — đã đổi từ Azure App Service |
| Version Control | GitHub Monorepo |
| API Testing | Postman / Thunder Client |

