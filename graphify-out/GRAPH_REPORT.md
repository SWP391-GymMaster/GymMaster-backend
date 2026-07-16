# Graph Report - backend  (2026-07-16)

## Corpus Check
- Corpus is ~34,828 words - fits in a single context window. You may not need a graph.

## Summary
- 1229 nodes · 3399 edges · 42 communities (36 shown, 6 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 218 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Authentication & JWT
- Membership Lifecycle
- VNPay Payment Gateway
- User Account Admin
- Gym Check-in
- Nutrition & Meal Logging
- Self-service Account Profile
- Dashboard, Audit & Notifications
- Trainer Management
- Payment Records
- Project File & Namespace Map
- PT Assignment
- Progress & Member 360
- Workout Plans
- Trainer Notes
- Member API Endpoints
- Member Profile Service
- Membership Packages
- Food Catalog
- External Service Options
- Data Layer & Entities
- Gemini Vision Client
- AI Food Scan Service
- Launch Settings
- Food Scan API
- EF DbContext Mapping
- Food Scan DTOs
- Workout Entities
- NuGet Dependencies
- Food Analyzer Contract
- Meal Log Entities
- Email Sender
- Database Seeder
- Role Entities
- Trainer Assignment Entity
- AppClock
- AuditService
- Avatar Storage Exception
- Membership Package Entity
- Password Reset Token
- Refresh Token Entity
- Gemini Options

## God Nodes (most connected - your core abstractions)
1. `AuthServiceResult` - 185 edges
2. `GymMaster.API.Services` - 75 edges
3. `GymMaster.API.DTOs` - 72 edges
4. `GymMaster.API.Entities` - 62 edges
5. `GymMasterDbContext` - 51 edges
6. `AuthService` - 41 edges
7. `User` - 30 edges
8. `ApiControllerBase` - 28 edges
9. `UserService` - 27 edges
10. `GymMaster.API.Controllers` - 26 edges

## Surprising Connections (you probably didn't know these)
- `AccountController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Controllers/AccountController.cs → GymMaster.API/Controllers/ApiControllerBase.cs
- `AssignmentsController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Controllers/AssignmentsController.cs → GymMaster.API/Controllers/ApiControllerBase.cs
- `AuthController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Controllers/AuthController.cs → GymMaster.API/Controllers/ApiControllerBase.cs
- `CheckInsController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Controllers/CheckInsController.cs → GymMaster.API/Controllers/ApiControllerBase.cs
- `FoodItemsController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Controllers/FoodItemsController.cs → GymMaster.API/Controllers/ApiControllerBase.cs

## Import Cycles
- None detected.

## Communities (42 total, 6 thin omitted)

### Community 0 - "Authentication & JWT"
Cohesion: 0.06
Nodes (41): Authorize, CancellationToken, HttpGet, HttpPost, IActionResult, Task, AuthController, ApiError (+33 more)

### Community 1 - "Membership Lifecycle"
Cohesion: 0.07
Nodes (43): CancellationToken, HttpGet, IActionResult, Task, MemberMembershipsController, Authorize, CancellationToken, HttpGet (+35 more)

### Community 2 - "VNPay Payment Gateway"
Cohesion: 0.06
Nodes (35): AllowAnonymous, Authorize, CancellationToken, HttpGet, HttpPost, IActionResult, IReadOnlyDictionary, Task (+27 more)

### Community 3 - "User Account Admin"
Cohesion: 0.08
Nodes (28): CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut, IActionResult, Task, UsersController (+20 more)

### Community 4 - "Gym Check-in"
Cohesion: 0.09
Nodes (33): CheckInRow, EndUtc, Expression, Authorize, CancellationToken, DateOnly, HttpGet, HttpPost (+25 more)

### Community 5 - "Nutrition & Meal Logging"
Cohesion: 0.09
Nodes (36): CancellationToken, DateOnly, HttpGet, HttpPost, IActionResult, Task, MealLogsController, CancellationToken (+28 more)

### Community 6 - "Self-service Account Profile"
Cohesion: 0.08
Nodes (30): CancellationToken, HttpGet, HttpPost, HttpPut, IActionResult, IFormFile, RequestSizeLimit, Task (+22 more)

### Community 7 - "Dashboard, Audit & Notifications"
Cohesion: 0.05
Nodes (38): ControllerBase, ApiControllerBase, CancellationToken, DateTime, HttpGet, IActionResult, Task, AuditLogsController (+30 more)

### Community 8 - "Trainer Management"
Cohesion: 0.10
Nodes (24): Authorize, CancellationToken, HttpGet, HttpPost, HttpPut, IActionResult, Task, TrainersController (+16 more)

### Community 9 - "Payment Records"
Cohesion: 0.07
Nodes (32): code, CancellationToken, HttpGet, IActionResult, Task, MemberPaymentsController, CancellationToken, DateOnly (+24 more)

### Community 10 - "Project File & Namespace Map"
Cohesion: 0.10
Nodes (3): GymMaster.API.Services, GymMaster.API.DTOs, GymMaster.API.Controllers

### Community 11 - "PT Assignment"
Cohesion: 0.09
Nodes (29): Authorize, CancellationToken, HttpGet, HttpPost, IActionResult, Task, AssignmentsController, CancellationToken (+21 more)

### Community 12 - "Progress & Member 360"
Cohesion: 0.10
Nodes (25): CancellationToken, HttpGet, HttpPost, IActionResult, Task, MemberProgressController, AssignedPt360, CheckIn360 (+17 more)

### Community 13 - "Workout Plans"
Cohesion: 0.11
Nodes (25): Authorize, CancellationToken, HttpDelete, HttpPut, IActionResult, Task, WorkoutPlansController, CreateTrainerNoteRequest (+17 more)

### Community 14 - "Trainer Notes"
Cohesion: 0.11
Nodes (24): Authorize, CancellationToken, HttpDelete, HttpPut, IActionResult, Task, TrainerNotesController, TrainerNoteResponse (+16 more)

### Community 15 - "Member API Endpoints"
Cohesion: 0.21
Nodes (15): IActionResult, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut, IActionResult (+7 more)

### Community 16 - "Member Profile Service"
Cohesion: 0.13
Nodes (14): CreateMemberRequest, CreateMemberResponse, MemberResponse, DateTime, MemberProfile, CancellationToken, Task, IAuditService (+6 more)

### Community 17 - "Membership Packages"
Cohesion: 0.12
Nodes (21): Authorize, CancellationToken, HttpGet, HttpPost, HttpPut, IActionResult, Task, PackagesController (+13 more)

### Community 18 - "Food Catalog"
Cohesion: 0.13
Nodes (18): Authorize, CancellationToken, HttpGet, HttpPost, IActionResult, Task, FoodItemsController, CreateFoodItemRequest (+10 more)

### Community 19 - "External Service Options"
Cohesion: 0.11
Nodes (10): GymMaster.API.Options, string, CloudinaryOptions, string, GoogleAuthOptions, CheckInRow, CancellationToken, Stream (+2 more)

### Community 21 - "Gemini Vision Client"
Cohesion: 0.18
Nodes (10): FinishReason, CancellationToken, ILogger, IReadOnlyList, Task, GeminiService, HttpClient, JsonDocument (+2 more)

### Community 22 - "AI Food Scan Service"
Cohesion: 0.27
Nodes (9): ScannedFood, DateTime, FoodItem, CancellationToken, ClaimsPrincipal, IFormFile, string, Task (+1 more)

### Community 23 - "Launch Settings"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 24 - "Food Scan API"
Cohesion: 0.24
Nodes (10): Consumes, Authorize, CancellationToken, HttpPost, IActionResult, IFormFile, RequestSizeLimit, Task (+2 more)

### Community 25 - "EF DbContext Mapping"
Cohesion: 0.18
Nodes (8): DbContext, DbSet, GymMasterDbContext, DateTime, AuditLog, DateTime, ProgressLog, ModelBuilder

### Community 26 - "Food Scan DTOs"
Cohesion: 0.24
Nodes (8): ConfirmAiFoodRequest, FoodNutritionDraft, FoodScanItem, FoodScanResponse, CancellationToken, ClaimsPrincipal, IFormFile, Task

### Community 27 - "Workout Entities"
Cohesion: 0.18
Nodes (8): ExerciseCatalog, WorkoutExercise, byte, DateOnly, DateTime, WorkoutPlan, WorkoutPlanStatuses, ICollection

### Community 28 - "NuGet Dependencies"
Cohesion: 0.20
Nodes (9): net10.0, BCrypt.Net-Next (4.2.0), CloudinaryDotNet (1.29.2), Google.Apis.Auth (1.74.0), MailKit (4.17.0), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.8), Microsoft.AspNetCore.OpenApi (10.0.8), Microsoft.EntityFrameworkCore.SqlServer (10.0.8) (+1 more)

### Community 29 - "Food Analyzer Contract"
Cohesion: 0.24
Nodes (6): CancellationToken, IReadOnlyList, Task, DetectedFood, FoodImageAnalysisResult, IFoodImageAnalyzer

### Community 30 - "Meal Log Entities"
Cohesion: 0.22
Nodes (6): DateOnly, DateTime, List, MealLog, MealLogItem, MealType

### Community 31 - "Email Sender"
Cohesion: 0.22
Nodes (6): string, EmailOptions, CancellationToken, ILogger, Task, EmailSender

### Community 32 - "Database Seeder"
Cohesion: 0.50
Nodes (3): Task, DatabaseSeeder, IServiceProvider

### Community 33 - "Role Entities"
Cohesion: 0.29
Nodes (5): List, string, Role, RoleNames, UserRole

### Community 34 - "Trainer Assignment Entity"
Cohesion: 0.33
Nodes (5): byte, DateOnly, DateTime, AssignmentStatuses, TrainerAssignment

### Community 35 - "AppClock"
Cohesion: 0.40
Nodes (3): DateOnly, DateTime, AppClock

### Community 36 - "AuditService"
Cohesion: 0.40
Nodes (4): CancellationToken, Task, AuditService, IHttpContextAccessor

## Knowledge Gaps
- **36 isolated node(s):** `ApiError`, `CheckInByDayItem`, `RevenueByMonthItem`, `ExpiredMembershipItem`, `FoodNutritionDraft` (+31 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AuthServiceResult` connect `Gym Check-in` to `Authentication & JWT`, `Membership Lifecycle`, `VNPay Payment Gateway`, `User Account Admin`, `Nutrition & Meal Logging`, `Self-service Account Profile`, `Dashboard, Audit & Notifications`, `Trainer Management`, `Payment Records`, `PT Assignment`, `Progress & Member 360`, `Workout Plans`, `Trainer Notes`, `Member API Endpoints`, `Member Profile Service`, `Membership Packages`, `Food Catalog`, `AI Food Scan Service`, `Food Scan DTOs`?**
  _High betweenness centrality (0.422) - this node is a cross-community bridge._
- **Why does `GymMasterDbContext` connect `EF DbContext Mapping` to `Authentication & JWT`, `Membership Lifecycle`, `VNPay Payment Gateway`, `User Account Admin`, `Gym Check-in`, `Nutrition & Meal Logging`, `Self-service Account Profile`, `Dashboard, Audit & Notifications`, `Trainer Management`, `Payment Records`, `PT Assignment`, `Progress & Member 360`, `Workout Plans`, `Trainer Notes`, `Member Profile Service`, `Membership Packages`, `Food Catalog`, `Data Layer & Entities`, `AI Food Scan Service`, `Workout Entities`, `Meal Log Entities`, `Database Seeder`, `Role Entities`, `Trainer Assignment Entity`, `AuditService`, `Membership Package Entity`, `Password Reset Token`, `Refresh Token Entity`?**
  _High betweenness centrality (0.121) - this node is a cross-community bridge._
- **Why does `GymMaster.API.Services` connect `Project File & Namespace Map` to `Authentication & JWT`, `VNPay Payment Gateway`, `AppClock`, `Gym Check-in`, `Avatar Storage Exception`, `Self-service Account Profile`, `Trainer Management`, `Member Profile Service`, `External Service Options`, `Data Layer & Entities`, `Food Analyzer Contract`, `Email Sender`?**
  _High betweenness centrality (0.091) - this node is a cross-community bridge._
- **What connects `ApiError`, `CheckInByDayItem`, `RevenueByMonthItem` to the rest of the system?**
  _36 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Authentication & JWT` be split into smaller, more focused modules?**
  _Cohesion score 0.05899122807017544 - nodes in this community are weakly interconnected._
- **Should `Membership Lifecycle` be split into smaller, more focused modules?**
  _Cohesion score 0.07088607594936709 - nodes in this community are weakly interconnected._
- **Should `VNPay Payment Gateway` be split into smaller, more focused modules?**
  _Cohesion score 0.060362173038229376 - nodes in this community are weakly interconnected._