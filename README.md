# GymMaster Backend

GymMaster backend implementation for the gym management system specified in `docs/init/`.

This repository is backend-only: ASP.NET Core 10 Web API, EF Core 10 + SQL Server, JWT auth + RBAC, membership/billing, check-in, PT training, nutrition, dashboard/audit, VNPay payments, and Gemini-based food image recognition. The Next.js UI lives in the separate `GymMaster-frontend` repo.

## Source of Truth

Read in this order:

1. `CONSTITUTION.md` — non-negotiable project law (stack, architecture, quality gates).
2. `CLAUDE.md` — project memory: current structure, patterns, gotchas, lessons learned.
3. `docs/init/` — full-system product scope, roles, use cases, requirements, roadmap, DB schema.
4. `specs/001-010/` — per-feature specs: endpoints, error codes, acceptance criteria.
5. Current code — what is actually implemented today.

Spec kit was synced backwards from the real code on 2026-07-15, so `specs/` and `docs/init/` describe what the code actually does.

## Quick Start

```bash
dotnet build backend/GymMaster.API/GymMaster.API.csproj
dotnet test tests/GymMaster.Api.Tests/GymMaster.Api.Tests.csproj    # 71 tests

cd backend/GymMaster.API && dotnet run                              # http://localhost:5042
```

OpenAPI: `http://localhost:5042/openapi/v1.json` (.NET 10 native — there is **no** `/swagger`).

Secrets (connection string, JWT key, VNPay, Gemini, Cloudinary, SMTP) live in **User Secrets**, never committed:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project backend/GymMaster.API
```

Seeded demo accounts: `admin@gymmaster.local` / `Admin123!` (also `staff@`, `pt@`, `member@`).

## Repository Layout

```text
backend/GymMaster.API/
├── Features/          Auth · Account · Users · Members · Trainers · Billing
│                      CheckIns · Training · Nutrition · Dashboard
│                      (controller + service + interface + DTO per feature)
├── Common/            ServiceResult<T>, ApiResponse<T>, PagedResult<T>,
│                      ApiControllerBase, AppClock, PersonValidation
├── Infrastructure/    Cloudinary, Gemini, EmailSender, VnPayLibrary
├── Entities/          25 EF entities (shared kernel)
├── Data/              GymMasterDbContext, DatabaseSeeder
└── Options/           strongly-typed config

database/              SQL scripts (schema is managed by hand, not EF Migrations)
docs/init/             00–15 spec kit + MANIFEST
docs/archive/          historical snapshots
specs/                 001–010 feature specs
tests/                 xUnit test project
graphify-out/          codebase knowledge graph — open graph.html in a browser
```

Structure mirrors the frontend repo (`src/features/` + `lib/` + `types/`).

## Deployment

Google Cloud Run + Cloud SQL. See `docs/DEPLOY_GCP.md`.

## Important Rules

- Never return entities directly — every request/response goes through a DTO.
- Services return `ServiceResult<T>` for business errors; throw only for system faults.
- Read `userId`/`role` from JWT claims, never from the request body.
- Every important mutating action writes an `AuditLog`.
- All endpoints are versioned under `/api/v1/...`.
