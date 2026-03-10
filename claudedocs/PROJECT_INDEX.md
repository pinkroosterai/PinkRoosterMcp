# PinkRoosterMcp — Project Index

> Auto-generated comprehensive project documentation. Last updated: 2026-03-10

## Overview

**PinkRoosterMcp** is a monorepo containing a C# MCP server, REST API, React dashboard, and PostgreSQL database for AI-driven project management. AI agents create and manage entities via MCP tools; humans view and delete via the dashboard.

| Component | Stack | Port |
|-----------|-------|------|
| **API** | .NET 9, EF Core, PostgreSQL 17 | 5100 |
| **MCP Server** | .NET 9, ModelContextProtocol SDK v1.0.0 | 5200 |
| **Dashboard** | React 19, Vite 7, Tailwind v4, Shadcn/ui | 5173 (dev) / 3000 (docker) |
| **Database** | PostgreSQL 17, snake_case, long PKs | 5432 |
| **Tests** | xUnit v3, Testcontainers, Respawn | — |

---

## Architecture

```
PinkRooster.Shared  ← no dependencies (DTOs, enums, constants, helpers)
       ↑
PinkRooster.Data    ← EF Core + Npgsql (entities, migrations, DbContext)
       ↑
PinkRooster.Api     ← REST API (controllers, services, middleware)

PinkRooster.Mcp     ← refs Shared ONLY, calls API via HTTP

dashboard           ← standalone Vite/React app, proxies to API

PinkRooster.Api.Tests ← integration tests (real PostgreSQL via Testcontainers)
```

**Key constraint**: MCP server has zero reference to Data or Api. All communication is HTTP via `PinkRoosterApiClient`.

---

## Entity Model

### Entities & Relationships

```
Project (1)
 ├── Issue (N)           — per-project IssueNumber
 └── WorkPackage (N)     — per-project WpNumber, optional LinkedIssueId
      ├── WorkPackagePhase (N)    — per-WP PhaseNumber
      │    ├── WorkPackageTask (N) — per-WP TaskNumber (across phases)
      │    └── AcceptanceCriterion (N)
      ├── WorkPackageDependency    — self-referencing WP↔WP
      └── WorkPackageTaskDependency — self-referencing Task↔Task

ActivityLog              — HTTP request logging (middleware)
IssueAuditLog            — full-field audit per Issue change
WorkPackageAuditLog      — full-field audit per WP change
PhaseAuditLog            — full-field audit per Phase change
TaskAuditLog             — full-field audit per Task change
FileReference            — owned type (jsonb), metadata only
```

### Human-Readable ID Formats

| Entity | Format | Example |
|--------|--------|---------|
| Project | `proj-{Id}` | `proj-1` |
| Issue | `proj-{ProjectId}-issue-{IssueNumber}` | `proj-1-issue-3` |
| Work Package | `proj-{ProjectId}-wp-{WpNumber}` | `proj-1-wp-2` |
| Phase | `proj-{ProjectId}-wp-{WpNumber}-phase-{PhaseNumber}` | `proj-1-wp-2-phase-1` |
| Task | `proj-{ProjectId}-wp-{WpNumber}-task-{TaskNumber}` | `proj-1-wp-2-task-5` |

IDs are derived at read-time from DB auto-increment `long` PKs. Never stored. Parsed by `IdParser` in `Shared/Helpers/`.

### Enums

| Enum | Values |
|------|--------|
| `CompletionState` | NotStarted, Designing, Implementing, Testing, InReview, Completed, Cancelled, Blocked, Replaced |
| `IssueType` | Bug, Defect, Regression, TechnicalDebt, PerformanceIssue, SecurityVulnerability |
| `IssueSeverity` | Critical, Major, Minor, Trivial |
| `WorkPackageType` | Feature, BugFix, Refactor, Spike, Chore |
| `Priority` | Critical, High, Medium, Low |
| `VerificationMethod` | AutomatedTest, Manual, AgentReview |
| `ProjectStatus` | (project lifecycle) |

State categories (`CompletionStateConstants`): Active (Designing, Implementing, Testing, InReview), Inactive (NotStarted, Blocked), Terminal (Completed, Cancelled, Replaced).

### State Behaviors

- **State-driven timestamps**: `StartedAt` (set once on first active transition), `CompletedAt` (→Completed), `ResolvedAt` (→any terminal). Service layer manages these, never callers.
- **Auto-block**: Adding dependency on non-terminal entity → dependent transitions to Blocked, `PreviousActiveState` captured.
- **Auto-unblock**: Blocker completes or last dependency removed → dependent restores `PreviousActiveState`.
- **Upward propagation**: All tasks terminal → Phase auto-completes → All phases terminal → WP auto-completes.
- **Cascade reporting**: `StateChangeDto` list returned in response DTOs and surfaced in MCP `OperationResult`.

---

## API Endpoints

### Projects — `api/projects`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/projects?path=...` | List all or find by path |
| GET | `/api/projects/{projectId}/status` | Compact project status (issue/WP counts + item lists) |
| GET | `/api/projects/{projectId}/next-actions?limit=&entityType=` | Priority-ordered actionable items |
| PUT | `/api/projects` | Upsert project |
| DELETE | `/api/projects/{id}` | Delete project |

### Issues — `api/projects/{projectId}/issues`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `.../issues?state=` | List (optional state filter) |
| GET | `.../issues/{issueNumber}` | Get by number |
| GET | `.../issues/{issueNumber}/audit` | Audit trail |
| GET | `.../issues/summary` | State counts |
| POST | `.../issues` | Create (201) |
| PATCH | `.../issues/{issueNumber}` | Partial update |
| DELETE | `.../issues/{issueNumber}` | Delete |

### Work Packages — `api/projects/{projectId}/work-packages`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `.../work-packages?state=` | List (optional state filter) |
| GET | `.../work-packages/{wpNumber}` | Get by number |
| GET | `.../work-packages/summary` | State counts |
| POST | `.../work-packages` | Create |
| PATCH | `.../work-packages/{wpNumber}` | Partial update |
| DELETE | `.../work-packages/{wpNumber}` | Delete |
| POST | `.../work-packages/{wpNumber}/dependencies` | Add dependency |
| DELETE | `.../work-packages/{wpNumber}/dependencies/{id}` | Remove dependency |

### Phases — `api/projects/{projectId}/work-packages/{wpNumber}/phases`
| Method | Route | Description |
|--------|-------|-------------|
| POST | `.../phases` | Create (with optional batch tasks) |
| PATCH | `.../phases/{phaseNumber}` | Update |
| DELETE | `.../phases/{phaseNumber}` | Delete |

### Tasks — `api/projects/{projectId}/work-packages/{wpNumber}/tasks`
| Method | Route | Description |
|--------|-------|-------------|
| POST | `.../tasks?phaseNumber=` | Create |
| PATCH | `.../tasks/{taskNumber}` | Update |
| DELETE | `.../tasks/{taskNumber}` | Delete |
| POST | `.../tasks/{taskNumber}/dependencies` | Add dependency |
| DELETE | `.../tasks/{taskNumber}/dependencies/{id}` | Remove dependency |

### Activity Logs — `api/activity-logs`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/activity-logs?page=&pageSize=` | Paginated logs |

### Middleware Pipeline
1. **CORS** → 2. **ApiKeyAuthMiddleware** (`X-Api-Key` header) → 3. **RequestLoggingMiddleware** (logs to ActivityLog) → 4. **Controllers**

---

## MCP Tools (16 total)

Registered as `pinkrooster` in `.mcp.json` at `http://localhost:5200`.

| Tool | R/W | Description |
|------|-----|-------------|
| `get_project_status` | R | Compact project status: issue/WP counts by state, active/inactive/blocked item lists |
| `get_next_actions` | R | Priority-ordered actionable items (tasks, WPs, issues) with limit and entity type filter |
| `create_or_update_project` | W | Upsert project by path |
| `add_or_update_issue` | W | Create (omit issueId) or update (provide issueId) |
| `get_issue_details` | R | Full issue data |
| `get_issue_overview` | R | List issues (filterable: active/inactive/terminal) |
| `get_work_packages` | R | List WPs (filterable by state) |
| `get_work_package_details` | R | Full WP tree (phases, tasks, deps, criteria) |
| `create_or_update_work_package` | W | Create/update WP (reports state changes) |
| `create_or_update_phase` | W | Create/update phase (optional batch tasks) |
| `create_or_update_task` | W | Create/update task (reports state changes) |
| `manage_work_package_dependency` | W | Add/remove WP dependency (reports auto-block/unblock) |
| `manage_task_dependency` | W | Add/remove task dependency (reports cascades) |
| `get_activity_logs` | R | Paginated HTTP request logs |

Write tools return `OperationResult` JSON: `{ responseType, message, id?, nextStep?, stateChanges? }`.

---

## Project Structure

### PinkRooster.Shared (34 files)
```
src/PinkRooster.Shared/
├── Constants/
│   ├── ApiRoutes.cs              — centralized route constants
│   └── AuthConstants.cs          — auth header/key names
├── DTOs/
│   ├── Requests/                 — 14 request DTOs
│   │   ├── CreateIssueRequest.cs
│   │   ├── UpdateIssueRequest.cs
│   │   ├── CreateOrUpdateProjectRequest.cs
│   │   ├── CreateWorkPackageRequest.cs
│   │   ├── UpdateWorkPackageRequest.cs
│   │   ├── CreatePhaseRequest.cs
│   │   ├── UpdatePhaseRequest.cs
│   │   ├── CreateTaskRequest.cs
│   │   ├── UpdateTaskRequest.cs
│   │   ├── ManageDependencyRequest.cs
│   │   ├── FileReferenceDto.cs
│   │   ├── AcceptanceCriterionDto.cs
│   │   ├── UpsertTaskInPhaseDto.cs
│   │   └── PaginationRequest.cs
│   └── Responses/                — 14 response DTOs
│       ├── ProjectResponse.cs
│       ├── ProjectStatusResponse.cs  — compact status (EntityStatusSummary, WorkPackageStatusSummary, StatusItem)
│       ├── NextActionItem.cs        — priority-ordered actionable item (Type, Id, Name, Priority, State, ParentId)
│       ├── IssueResponse.cs
│       ├── IssueSummaryResponse.cs
│       ├── IssueAuditLogResponse.cs
│       ├── WorkPackageResponse.cs
│       ├── WorkPackageSummaryResponse.cs
│       ├── PhaseResponse.cs
│       ├── TaskResponse.cs
│       ├── DependencyResponse.cs
│       ├── TaskDependencyResponse.cs
│       ├── StateChangeDto.cs
│       ├── ActivityLogResponse.cs
│       └── PaginatedResponse.cs
├── Enums/
│   ├── CompletionState.cs
│   ├── CompletionStateConstants.cs
│   ├── IssueType.cs
│   ├── IssueSeverity.cs
│   ├── WorkPackageType.cs
│   ├── Priority.cs
│   ├── ProjectStatus.cs
│   └── VerificationMethod.cs
└── Helpers/
    └── IdParser.cs               — human-readable ID parsing
```

### PinkRooster.Data (43 files)
```
src/PinkRooster.Data/
├── AppDbContext.cs                — 13 DbSets, auto-UpdatedAt
├── DbInitializer.cs              — auto-migrate on startup
├── Entities/                     — 14 entity classes
│   ├── Project.cs
│   ├── Issue.cs
│   ├── WorkPackage.cs
│   ├── WorkPackagePhase.cs
│   ├── WorkPackageTask.cs
│   ├── AcceptanceCriterion.cs
│   ├── WorkPackageDependency.cs
│   ├── WorkPackageTaskDependency.cs
│   ├── FileReference.cs          — owned type (jsonb)
│   ├── ActivityLog.cs
│   ├── IssueAuditLog.cs
│   ├── WorkPackageAuditLog.cs
│   ├── PhaseAuditLog.cs
│   └── TaskAuditLog.cs
├── Configurations/               — 14 fluent config classes
│   ├── ProjectConfiguration.cs
│   ├── IssueConfiguration.cs
│   ├── WorkPackageConfiguration.cs
│   ├── WorkPackagePhaseConfiguration.cs
│   ├── WorkPackageTaskConfiguration.cs
│   ├── AcceptanceCriterionConfiguration.cs
│   ├── WorkPackageDependencyConfiguration.cs
│   ├── WorkPackageTaskDependencyConfiguration.cs
│   ├── ActivityLogConfiguration.cs
│   ├── IssueAuditLogConfiguration.cs
│   ├── WorkPackageAuditLogConfiguration.cs
│   ├── PhaseAuditLogConfiguration.cs
│   └── TaskAuditLogConfiguration.cs
└── Migrations/                   — 4 migrations
    ├── 20260310100554_InitialCreate
    ├── 20260310111720_AddProjectEntity
    ├── 20260310121127_AddIssueEntity
    └── 20260310131114_AddWorkPackages
```

### PinkRooster.Api (22 files)
```
src/PinkRooster.Api/
├── Program.cs                    — DI, middleware pipeline, CORS
├── Controllers/
│   ├── ProjectController.cs      — GET/PUT/DELETE
│   ├── IssueController.cs        — GET/POST/PATCH/DELETE + audit + summary
│   ├── WorkPackageController.cs  — CRUD + dependencies
│   ├── PhaseController.cs        — POST/PATCH/DELETE
│   ├── WorkPackageTaskController.cs — CRUD + dependencies
│   └── ActivityLogController.cs  — GET paginated
├── Services/
│   ├── IProjectService.cs / ProjectService.cs
│   ├── IIssueService.cs / IssueService.cs
│   ├── IWorkPackageService.cs / WorkPackageService.cs
│   ├── IPhaseService.cs / PhaseService.cs
│   ├── IWorkPackageTaskService.cs / WorkPackageTaskService.cs
│   └── IActivityLogService.cs / ActivityLogService.cs
└── Middleware/
    ├── ApiKeyAuthMiddleware.cs   — X-Api-Key validation
    └── RequestLoggingMiddleware.cs — ActivityLog recording
```

### PinkRooster.Mcp (14 files)
```
src/PinkRooster.Mcp/
├── Program.cs                    — MCP server config, health endpoint
├── Clients/
│   └── PinkRoosterApiClient.cs   — typed HTTP client for all API calls
├── Tools/
│   ├── ProjectTools.cs           — 3 tools
│   ├── IssueTools.cs             — 3 tools
│   ├── WorkPackageTools.cs       — 7 tools
│   └── ActivityLogTools.cs       — 1 tool
└── Responses/
    ├── OperationResult.cs        — standard write response
    ├── ResponseType.cs           — Success/Warning/Error enum
    ├── IssueDetailResponse.cs
    ├── WorkPackageDetailResponse.cs
    └── JsonDefaults.cs           — shared serializer options
```

### PinkRooster.Api.Tests (10 files)
```
tests/PinkRooster.Api.Tests/
├── Fixtures/
│   ├── PostgresFixture.cs        — Testcontainers PostgreSQL 17
│   ├── ApiFactory.cs             — WebApplicationFactory config
│   ├── IntegrationTest.cs        — base class (auth client + Respawn reset)
│   └── IntegrationTestCollection.cs — shared container fixture
├── AuthMiddlewareTests.cs
├── ProjectEndpointTests.cs       — 12 tests
├── ProjectStatusTests.cs         — 7 tests (status endpoint: counts, categorization, blocked separation)
├── NextActionsTests.cs           — 11 tests (next actions: filtering, sorting, limit, exclusions)
├── IssueEndpointTests.cs         — 12 tests
├── WorkPackageEndpointTests.cs   — 22 tests (inc. cascade tests)
├── PhaseEndpointTests.cs         — 9 tests
├── ScaffoldEndpointTests.cs
└── TaskEndpointTests.cs          — 16 tests
```

**Total: 97 integration tests** covering CRUD, dependencies, cascades, timestamps, status summaries, next actions, and audit logs.

### Dashboard (37 files)
```
src/dashboard/
├── src/
│   ├── App.tsx                   — routes + QueryClient + ProjectProvider
│   ├── main.tsx                  — React DOM entry
│   ├── index.css                 — Tailwind imports
│   ├── api/
│   │   ├── client.ts             — apiFetch wrapper (proxies to :5100)
│   │   ├── projects.ts
│   │   ├── issues.ts
│   │   ├── work-packages.ts
│   │   └── activity.ts
│   ├── components/
│   │   ├── layout/
│   │   │   ├── app-layout.tsx    — shell with sidebar
│   │   │   ├── app-sidebar.tsx   — navigation
│   │   │   └── project-switcher.tsx — dropdown project selector
│   │   └── ui/                   — 12 Shadcn/ui components
│   │       ├── alert-dialog.tsx, badge.tsx, button.tsx, card.tsx
│   │       ├── dropdown-menu.tsx, input.tsx, separator.tsx
│   │       ├── sheet.tsx, sidebar.tsx, skeleton.tsx
│   │       ├── table.tsx, tooltip.tsx
│   ├── hooks/
│   │   ├── use-projects.ts      — useProjects, useDeleteProject
│   │   ├── use-issues.ts        — useIssues, useIssue, useIssueSummary, useIssueAuditLog, useDeleteIssue
│   │   ├── use-work-packages.ts — useWorkPackages, useWorkPackage, useWPSummary, useDeleteWP
│   │   ├── use-activity-logs.ts
│   │   ├── use-health.ts        — polls /health every 30s
│   │   ├── use-mobile.tsx
│   │   └── use-project-context.tsx — selected project (localStorage)
│   ├── lib/
│   │   └── utils.ts             — cn() utility
│   ├── pages/
│   │   ├── dashboard-page.tsx
│   │   ├── project-list-page.tsx
│   │   ├── project-detail-page.tsx — Issues/WP tab switcher
│   │   ├── issue-detail-page.tsx
│   │   ├── work-package-detail-page.tsx — phase/task tree
│   │   └── activity-log-page.tsx
│   └── types/
│       └── index.ts             — all TypeScript interfaces
├── vite.config.ts               — proxy to :5100, @/ alias
├── components.json              — Shadcn new-york style
├── package.json
├── tsconfig.json / tsconfig.app.json / tsconfig.node.json
└── eslint.config.js
```

**Routes:**
- `/` — Dashboard home (health, stats)
- `/projects` — Project list
- `/projects/:id` — Project detail (Issues + Work Packages tabs)
- `/projects/:id/issues/:issueNumber` — Issue detail + audit
- `/projects/:id/work-packages/:wpNumber` — WP detail + phase/task tree
- `/activity` — Activity log

---

## Infrastructure

### Docker Services
| Service | Image | Port Mapping | Health Check | Depends On |
|---------|-------|-------------|--------------|------------|
| postgres | postgres:17 | 5432:5432 | pg_isready | — |
| api | docker/api.Dockerfile | 5100:8080 | curl /health | postgres |
| mcp | docker/mcp.Dockerfile | 5200:8080 | curl /health | api |
| dashboard | docker/dashboard.Dockerfile | 3000:80 | — | api |

### Make Targets
| Target | Description |
|--------|-------------|
| `make setup` | First-time: copy .env + install deps |
| `make dev` | Start all services locally (hot reload) |
| `make dev-api` / `dev-mcp` / `dev-dashboard` | Individual service |
| `make build` | Build all |
| `make up` / `down` / `restart` | Docker lifecycle |
| `make logs` | Tail all logs |
| `make db-migrate` | Apply EF migrations |
| `make db-migration name=X` | Create new migration |
| `make db-status` / `db-rollback` / `db-reset` | DB management |
| `make lint` / `format` | Code quality |
| `make clean` / `nuke` | Cleanup |

### EF Core Migrations
| # | Name | Tables Created |
|---|------|---------------|
| 1 | InitialCreate | activity_logs |
| 2 | AddProjectEntity | projects |
| 3 | AddIssueEntity | issues, issue_audit_logs |
| 4 | AddWorkPackages | work_packages, work_package_phases, work_package_tasks, acceptance_criteria, work_package_dependencies, work_package_task_dependencies, work_package_audit_logs, phase_audit_logs, task_audit_logs |

### Key Configuration Files
| File | Purpose |
|------|---------|
| `.env` / `.env.example` | POSTGRES_PASSWORD, API_KEY |
| `.mcp.json` | MCP server registration (pinkrooster @ :5200) |
| `Directory.Build.props` | net9.0, nullable, implicit usings |
| `.editorconfig` | C# 4-space, TS/JS 2-space, LF, UTF-8 |
| `PinkRooster.slnx` | Solution file (new format) |
| `appsettings.Development.json` | DB connection, API keys, CORS |

---

## Design Documents

| Document | Content |
|----------|---------|
| `claudedocs/design_project_entity.md` | Project entity full vertical slice |
| `claudedocs/design_issue_entity.md` | Issue entity full vertical slice |
| `claudedocs/design_work_packages.md` | Work Package entity full vertical slice |
| `claudedocs/workflow_issue_entity.md` | Issue implementation workflow (6 phases) |
| `claudedocs/workflow_work_packages.md` | Work Package implementation workflow |
| `claudedocs/workflow_implementation.md` | High-level 6-phase monorepo plan |

---

## Implementation Status

| Feature | Status | Tests |
|---------|--------|-------|
| Infrastructure (Phases 1-6) | Complete | — |
| Project entity (full slice) | Complete | 12 |
| Compact project status | Complete | 7 |
| Priority-ordered next actions | Complete | 11 |
| Issue entity (full slice) | Complete | 12 |
| Work Packages (full slice) | Complete | 22 WP + 9 phase + 16 task |
| State change cascades | Complete | 4 (within WP/task tests) |
| Dashboard | Complete | — |
| Docker orchestration | Complete | — |
| **Total integration tests** | — | **97** |

### Known Minor Issues (from Phase 1-6 reflection)
1. Hardcoded API key in dashboard client — should use env var
2. RequestLoggingMiddleware logs its own `/api/activity-logs` requests
3. Dashboard title still generic "dashboard"
4. Missing favicon
5. ApiRoutes.cs constants not consumed by controllers
6. Status card hardcoded "Online" — should call /health
