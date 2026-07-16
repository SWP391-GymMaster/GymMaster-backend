# Graph Report - backend  (2026-07-16)

## Corpus Check
- Corpus is ~34,868 words - fits in a single context window. You may not need a graph.

## Summary
- 1240 nodes · 3425 edges · 43 communities (36 shown, 7 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 224 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Authentication & JWT
- Membership Lifecycle
- VNPay Payment Gateway
- Gym Check-in & Notifications
- Nutrition & Meal Logging
- User Account Admin
- Self-service Account Profile
- Payment Records
- Trainer Management
- PT Assignment
- Member API Endpoints
- Progress & Member 360
- Membership Packages
- Dashboard & Audit
- Workout Plans
- Member Profile Service
- Trainer Notes
- Food Catalog
- Shared Service Contracts
- Data Layer & Seeder
- Avatar Storage (Cloudinary)
- Gemini Vision Client
- AI Food Scan Service
- Launch Settings
- Food Scan API
- PT Training Contracts
- Auth Token Entities
- Workout Entities
- Food Scan DTOs
- NuGet Dependencies
- Food Analyzer Contract
- Auth & Account Endpoints
- Meal Log Entities
- Database Seeder
- Role Entities
- CheckInService
- TrainerAssignment
- AuditService
- ApiResponse
- ITrainerService
- TrainerNote
- AuditLog
- ProgressLog

## God Nodes (most connected - your core abstractions)
1. `ServiceResult` - 185 edges
2. `GymMaster.API.Common` - 68 edges
3. `GymMaster.API.Entities` - 62 edges
4. `GymMasterDbContext` - 51 edges
5. `AuthService` - 41 edges
6. `User` - 30 edges
7. `ApiControllerBase` - 28 edges
8. `UserService` - 27 edges
9. `MemberProfile` - 25 edges
10. `MemberService` - 25 edges

## Surprising Connections (you probably didn't know these)
- `AccountController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Features/Account/AccountController.cs → GymMaster.API/Common/ApiControllerBase.cs
- `AuthController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Features/Auth/AuthController.cs → GymMaster.API/Common/ApiControllerBase.cs
- `MemberMembershipsController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Features/Billing/MemberMembershipsController.cs → GymMaster.API/Common/ApiControllerBase.cs
- `MembershipsController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Features/Billing/MembershipsController.cs → GymMaster.API/Common/ApiControllerBase.cs
- `PackagesController` --inherits--> `ApiControllerBase`  [EXTRACTED]
  GymMaster.API/Features/Billing/PackagesController.cs → GymMaster.API/Common/ApiControllerBase.cs

## Import Cycles
- None detected.

## Communities (43 total, 7 thin omitted)

### Community 0 - "Authentication & JWT"
Cohesion: 0.05
Nodes (47): DateTime, List, string, User, UserStatuses, Authorize, CancellationToken, HttpGet (+39 more)

### Community 1 - "Membership Lifecycle"
Cohesion: 0.07
Nodes (44): DateOnly, DateTime, Membership, CancellationToken, ClaimsPrincipal, IReadOnlyList, Task, IMembershipService (+36 more)

### Community 2 - "VNPay Payment Gateway"
Cohesion: 0.06
Nodes (37): AllowAnonymous, MembershipStatus, PaymentMethod, PaymentStatus, DateTime, Payment, CancellationToken, ClaimsPrincipal (+29 more)

### Community 3 - "Gym Check-in & Notifications"
Cohesion: 0.07
Nodes (38): CheckInRow, EndUtc, Expression, ServiceResult, DateTime, CheckIn, CheckInResponse, CreateCheckInRequest (+30 more)

### Community 4 - "Nutrition & Meal Logging"
Cohesion: 0.09
Nodes (36): DateOnly, DateTime, CalorieTarget, CancellationToken, ClaimsPrincipal, DateOnly, IReadOnlyList, Task (+28 more)

### Community 5 - "User Account Admin"
Cohesion: 0.09
Nodes (25): CancellationToken, Task, IUserService, AdminUserResponse, CreateUserRequest, CreateUserResponse, ResetUserPasswordResponse, UpdateUserRequest (+17 more)

### Community 6 - "Self-service Account Profile"
Cohesion: 0.08
Nodes (31): DateTime, StaffProfile, CancellationToken, HttpGet, HttpPost, HttpPut, IActionResult, IFormFile (+23 more)

### Community 7 - "Payment Records"
Cohesion: 0.06
Nodes (39): code, ControllerBase, ApiControllerBase, CancellationToken, ClaimsPrincipal, DateOnly, IReadOnlyList, Task (+31 more)

### Community 8 - "Trainer Management"
Cohesion: 0.09
Nodes (26): DateTime, int, PersonValidation, DateTime, TrainerProfile, CancellationToken, ClaimsPrincipal, Task (+18 more)

### Community 9 - "PT Assignment"
Cohesion: 0.08
Nodes (31): DateOnly, DateTime, AppClock, Authorize, CancellationToken, HttpGet, HttpPost, IActionResult (+23 more)

### Community 10 - "Member API Endpoints"
Cohesion: 0.15
Nodes (21): IActionResult, CancellationToken, ClaimsPrincipal, Task, IMemberService, UpdateMemberRequest, Authorize, CancellationToken (+13 more)

### Community 11 - "Progress & Member 360"
Cohesion: 0.10
Nodes (25): CancellationToken, ClaimsPrincipal, IReadOnlyList, Task, IProgressService, CancellationToken, HttpGet, HttpPost (+17 more)

### Community 12 - "Membership Packages"
Cohesion: 0.10
Nodes (23): DateTime, MembershipPackage, CancellationToken, ClaimsPrincipal, IReadOnlyList, Task, IMembershipPackageService, CancellationToken (+15 more)

### Community 13 - "Dashboard & Audit"
Cohesion: 0.08
Nodes (27): PagedResult, CancellationToken, DateTime, HttpGet, IActionResult, Task, AuditLogsController, CancellationToken (+19 more)

### Community 14 - "Workout Plans"
Cohesion: 0.14
Nodes (20): CancellationToken, ClaimsPrincipal, IReadOnlyList, Task, IWorkoutPlanService, UpdateWorkoutPlanRequest, WorkoutPlanResponse, Authorize (+12 more)

### Community 15 - "Member Profile Service"
Cohesion: 0.16
Nodes (11): DateTime, MemberProfile, CreateMemberRequest, CreateMemberResponse, MemberResponse, CancellationToken, ClaimsPrincipal, DbUpdateException (+3 more)

### Community 16 - "Trainer Notes"
Cohesion: 0.14
Nodes (19): CancellationToken, ClaimsPrincipal, IReadOnlyList, Task, ITrainerNoteService, TrainerNoteResponse, UpdateTrainerNoteRequest, Authorize (+11 more)

### Community 17 - "Food Catalog"
Cohesion: 0.13
Nodes (18): Authorize, CancellationToken, HttpGet, HttpPost, IActionResult, Task, FoodItemsController, CancellationToken (+10 more)

### Community 18 - "Shared Service Contracts"
Cohesion: 0.16
Nodes (3): GymMaster.API.Common, GymMaster.API.Features.Billing, GymMaster.API.Features.Nutrition

### Community 19 - "Data Layer & Seeder"
Cohesion: 0.16
Nodes (5): GymMaster.API.Features.Members, GymMaster.API.Data, GymMaster.API.Features.Dashboard, GymMaster.API.Features.Users, GymMaster.API.Entities

### Community 20 - "Avatar Storage (Cloudinary)"
Cohesion: 0.13
Nodes (11): GymMaster.API.Infrastructure, GymMaster.API.Options, Exception, AvatarStorageException, CancellationToken, Stream, Task, CloudinaryAvatarStorage (+3 more)

### Community 21 - "Gemini Vision Client"
Cohesion: 0.15
Nodes (12): FinishReason, CancellationToken, ILogger, IReadOnlyList, Task, GeminiService, string, GeminiOptions (+4 more)

### Community 22 - "AI Food Scan Service"
Cohesion: 0.27
Nodes (9): DateTime, FoodItem, ScannedFood, CancellationToken, ClaimsPrincipal, IFormFile, string, Task (+1 more)

### Community 23 - "Launch Settings"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 24 - "Food Scan API"
Cohesion: 0.24
Nodes (10): Consumes, Authorize, CancellationToken, HttpPost, IActionResult, IFormFile, RequestSizeLimit, Task (+2 more)

### Community 26 - "Auth Token Entities"
Cohesion: 0.18
Nodes (8): DbContext, DbSet, GymMasterDbContext, DateTime, PasswordResetToken, DateTime, RefreshToken, ModelBuilder

### Community 27 - "Workout Entities"
Cohesion: 0.18
Nodes (8): ExerciseCatalog, WorkoutExercise, byte, DateOnly, DateTime, WorkoutPlan, WorkoutPlanStatuses, ICollection

### Community 28 - "Food Scan DTOs"
Cohesion: 0.24
Nodes (8): ConfirmAiFoodRequest, FoodNutritionDraft, FoodScanItem, FoodScanResponse, CancellationToken, ClaimsPrincipal, IFormFile, Task

### Community 29 - "NuGet Dependencies"
Cohesion: 0.20
Nodes (9): net10.0, BCrypt.Net-Next (4.2.0), CloudinaryDotNet (1.29.2), Google.Apis.Auth (1.74.0), MailKit (4.17.0), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.8), Microsoft.AspNetCore.OpenApi (10.0.8), Microsoft.EntityFrameworkCore.SqlServer (10.0.8) (+1 more)

### Community 30 - "Food Analyzer Contract"
Cohesion: 0.24
Nodes (6): CancellationToken, IReadOnlyList, Task, DetectedFood, FoodImageAnalysisResult, IFoodImageAnalyzer

### Community 32 - "Meal Log Entities"
Cohesion: 0.22
Nodes (6): DateOnly, DateTime, List, MealLog, MealLogItem, MealType

### Community 33 - "Database Seeder"
Cohesion: 0.50
Nodes (3): Task, DatabaseSeeder, IServiceProvider

### Community 34 - "Role Entities"
Cohesion: 0.29
Nodes (5): List, string, Role, RoleNames, UserRole

### Community 36 - "TrainerAssignment"
Cohesion: 0.33
Nodes (5): byte, DateOnly, DateTime, AssignmentStatuses, TrainerAssignment

### Community 37 - "AuditService"
Cohesion: 0.40
Nodes (4): CancellationToken, Task, AuditService, IHttpContextAccessor

### Community 40 - "TrainerNote"
Cohesion: 0.50
Nodes (3): DateOnly, DateTime, TrainerNote

## Knowledge Gaps
- **36 isolated node(s):** `ApiError`, `PaymentBrief`, `PaymentResponse`, `PaymentMethodSummaryResponse`, `DailyRevenueResponse` (+31 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ServiceResult` connect `Gym Check-in & Notifications` to `Authentication & JWT`, `Membership Lifecycle`, `VNPay Payment Gateway`, `Nutrition & Meal Logging`, `User Account Admin`, `Self-service Account Profile`, `Payment Records`, `Trainer Management`, `PT Assignment`, `Member API Endpoints`, `Progress & Member 360`, `Membership Packages`, `Dashboard & Audit`, `Workout Plans`, `Member Profile Service`, `Trainer Notes`, `Food Catalog`, `AI Food Scan Service`, `Food Scan DTOs`?**
  _High betweenness centrality (0.479) - this node is a cross-community bridge._
- **Why does `GymMasterDbContext` connect `Auth Token Entities` to `Authentication & JWT`, `Membership Lifecycle`, `VNPay Payment Gateway`, `Gym Check-in & Notifications`, `Nutrition & Meal Logging`, `User Account Admin`, `Self-service Account Profile`, `Payment Records`, `Trainer Management`, `PT Assignment`, `Progress & Member 360`, `Membership Packages`, `Dashboard & Audit`, `Workout Plans`, `Member Profile Service`, `Trainer Notes`, `Food Catalog`, `Data Layer & Seeder`, `AI Food Scan Service`, `Workout Entities`, `Meal Log Entities`, `Database Seeder`, `Role Entities`, `TrainerAssignment`, `AuditService`, `TrainerNote`, `AuditLog`, `ProgressLog`?**
  _High betweenness centrality (0.108) - this node is a cross-community bridge._
- **Why does `GymMaster.API.Common` connect `Shared Service Contracts` to `Gym Check-in & Notifications`, `CheckInService`, `ApiResponse`, `Payment Records`, `Trainer Management`, `PT Assignment`, `ITrainerService`, `Dashboard & Audit`, `Data Layer & Seeder`, `Avatar Storage (Cloudinary)`, `PT Training Contracts`, `Auth & Account Endpoints`?**
  _High betweenness centrality (0.073) - this node is a cross-community bridge._
- **What connects `ApiError`, `PaymentBrief`, `PaymentResponse` to the rest of the system?**
  _36 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Authentication & JWT` be split into smaller, more focused modules?**
  _Cohesion score 0.05273177232057872 - nodes in this community are weakly interconnected._
- **Should `Membership Lifecycle` be split into smaller, more focused modules?**
  _Cohesion score 0.06925624811803674 - nodes in this community are weakly interconnected._
- **Should `VNPay Payment Gateway` be split into smaller, more focused modules?**
  _Cohesion score 0.05701592002961866 - nodes in this community are weakly interconnected._