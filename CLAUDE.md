# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Development Commands

All commands are available via `make`. Run `make help` for the full list.

### Build
```bash
make build              # Build everything (.NET + dashboard)
make build-api          # Build .NET solution only
make build-dashboard    # Build dashboard only
dotnet build PinkRooster.slnx
```

### Local Development (requires Docker for PostgreSQL)
```bash
make setup              # First-time: copy .env + install deps
make dev                # Start all services (DB + API + MCP + dashboard)
make dev-api            # API only (hot reload) — http://localhost:5100
make dev-mcp            # MCP only (hot reload) — http://localhost:5200
make dev-dashboard      # Dashboard only — http://localhost:5173
```

### Docker
```bash
make up                 # Start all containers (detached)
make down               # Stop all containers
make restart            # Rebuild and restart
make logs               # Tail all logs
```

### Database (EF Core)
```bash
make db-migrate                     # Apply pending migrations
make db-migration name=MigrationName  # Create new migration
make db-status                      # Show migration status
make db-rollback                    # Rollback last migration
```

The underlying EF command for migrations:
```bash
dotnet ef migrations add <Name> --project src/PinkRooster.Data --startup-project src/PinkRooster.Api
```

### Linting & Formatting
```bash
make lint               # Lint dashboard (ESLint)
make format             # Format .NET code
```

### Testing
```bash
dotnet test                 # Run all tests
dotnet test tests/PinkRooster.Api.Tests  # Run API integration tests only
```

## Architecture

Monorepo with 4 .NET projects + 1 React dashboard + 1 test project, all targeting .NET 9.0 (set via `Directory.Build.props`). Solution file uses the new `.slnx` format.

### Project Dependency Graph
```
PinkRooster.Shared  ← no dependencies (DTOs, constants)
       ↑
PinkRooster.Data    ← EF Core + Npgsql (entities, migrations, DbContext)
       ↑
PinkRooster.Api     ← REST API (controllers, services, middleware)

PinkRooster.Mcp     ← references Shared ONLY, calls API via HTTP
dashboard           ← standalone Vite/React app, proxies to API

PinkRooster.Api.Tests ← integration tests (xUnit v3, Testcontainers, Respawn)
```

**Key design decision**: The MCP server has no reference to Data or Api projects. It communicates with the API exclusively over HTTP via `PinkRoosterApiClient`.

### API Pattern
Service layer + Controllers (not Clean Architecture/CQRS). Services are interfaces with implementations registered in DI.

### Middleware Pipeline (API)
1. `RequestLoggingMiddleware` — logs all requests via `IActivityLogService.LogRequestAsync()`
2. `ApiKeyAuthMiddleware` — validates `X-Api-Key` header (keys configured in `Auth:ApiKeys` array)

### MCP Server
Uses official ModelContextProtocol.AspNetCore SDK. `MapMcp()` maps to root `/`, serving Streamable HTTP (protocol 2025-03-26) and legacy SSE at `/sse` and `/message`.

**Claude Code registration**: The MCP server is registered in `.mcp.json` (project scope) as `pinkrooster` with URL `http://localhost:5200` (root, NOT `/mcp`). Docker containers must be running (`make up` or `docker compose up -d`).

### Testing MCP Tools
After code changes to the API or MCP server, rebuild and redeploy with:
```bash
docker compose up -d --build api mcp    # Rebuild + restart API and MCP
docker compose ps                        # Verify all services show "healthy"
```
Wait for health checks to pass (~10-15s) before testing. The MCP server depends on the API being healthy first.

**Available MCP tools** (registered as `pinkrooster` in `.mcp.json`):
| Tool | Type | Description |
|------|------|-------------|
| `get_project_status` | Read | Compact project status: issue/WP counts by state, active/inactive/blocked item lists |
| `get_next_actions` | Read | Priority-ordered actionable items (tasks, WPs, issues) with optional limit and entity type filter |
| `create_or_update_project` | Write | Upsert project by path |
| `create_or_update_issue` | Write | Create (omit issueId) or update (provide issueId) an issue |
| `get_issue_details` | Read | Full issue data by composite ID (no audit trail) |
| `get_issue_overview` | Read | List issues for a project, filterable by state category |
| `get_work_packages` | Read | List work packages, filterable by state category |
| `get_work_package_details` | Read | Full WP tree: phases, tasks, dependencies, acceptance criteria |
| `create_or_update_work_package` | Write | Create or update WP (returns state changes on cascades) |
| `create_or_update_phase` | Write | Create or update phase, optional batch task creation |
| `create_or_update_task` | Write | Create or update task (returns state changes on cascades) |
| `batch_update_task_states` | Write | Update state of multiple tasks in one call (consolidated cascades) |
| `manage_work_package_dependency` | Write | Add/remove WP dependency (returns auto-block state changes) |
| `manage_task_dependency` | Write | Add/remove task dependency (returns auto-block state changes) |
| `scaffold_work_package` | Write | One-call WP creation with phases, tasks, dependencies, and WP blockers |

**Testing flow for MCP tools (E2E):**

1. **Project setup**: `get_project_status` with `projectPath` — confirms project exists, returns `projectId` + compact status summary
2. **Issue CRUD**:
   - `create_or_update_issue` with `projectId` + required fields (name, description, issueType, severity) — creates issue, response has `id` field with `proj-{N}-issue-{N}`
   - `get_issue_details` with returned ID — verify all fields including auto-set timestamps
   - `create_or_update_issue` with `projectId` + `issueId` + fields to change — partial update
   - `get_issue_overview` with `projectId` and optional `stateFilter` (active/inactive/terminal)
3. **Work Package CRUD**:
   - `create_or_update_work_package` with `projectId` + name/description — creates WP, response has `id` with `proj-{N}-wp-{N}`
   - `get_work_package_details` with returned ID — verify full tree
   - `create_or_update_work_package` with `projectId` + `workPackageId` + fields — partial update
   - `get_work_packages` with `projectId` and optional `stateFilter`
4. **Phase & Task CRUD**:
   - `create_or_update_phase` with `workPackageId` + name — creates phase, optionally include `tasks` JSON for batch creation
   - `create_or_update_task` with `phaseId` + name/description — creates task
   - Update via `taskId` or `phaseId` + fields to change
5. **Dependencies & State Change Cascades**:
   - `manage_work_package_dependency` / `manage_task_dependency` with action `add` — verify `stateChanges` array shows auto-block
   - Update blocker to `Completed` — verify `stateChanges` shows auto-unblock of dependents
   - Complete all tasks in a phase — verify `stateChanges` shows Phase auto-complete + WP auto-complete cascade
6. **State timestamp verification** (applies to Issues, WPs, and Tasks):
   - Create with `state: "Implementing"` → `startedAt` should be set
   - Update to `state: "Completed"` → `completedAt` + `resolvedAt` should be set, `startedAt` preserved
7. **Error handling verification**:
   - Circular dependency → `OperationResult` with `responseType: "Error"` and clear reason
   - Self-dependency, duplicate dependency → same pattern
   - Invalid IDs → `OperationResult` with format hint

**API key for direct curl testing:** configured in `appsettings.Development.json` under `Auth:ApiKeys` array. Pass via `X-Api-Key` header.

**Common issues:**
- 500 on entity create with FK violation → audit log entries must use navigation property (`Entity = entity`) not `EntityId = entity.Id` for new entities (Id is 0 before SaveChanges)
- MCP server shows "health: starting" → wait for API to become healthy first (dependency chain: postgres → api → mcp)
- Batch task creation duplicate key → service must pre-fetch starting numbers before the loop, not query inside the loop

### Database
PostgreSQL 17. EF Core with snake_case naming convention (fluent configuration). `DbInitializer` auto-applies migrations on API startup.

### Dashboard
Vite + React 19 + TypeScript. Shadcn/ui (new-york style) with Tailwind CSS v4. TanStack Query for data fetching, TanStack Table for tables. Path alias `@/` → `src/`.

## Service URLs
| Service   | Local Dev        | Docker           |
|-----------|-----------------|------------------|
| API       | localhost:5100  | localhost:5100   |
| MCP       | localhost:5200  | localhost:5200   |
| Dashboard | localhost:5173  | localhost:3000   |
| Postgres  | localhost:5432  | localhost:5432   |
| Swagger   | localhost:5100/swagger/index.html | same |

## Key Configuration
- `.env` — `POSTGRES_PASSWORD` and `API_KEY` (copy from `.env.example`)
- `appsettings.Development.json` — DB connection string and API keys for local dev
- `docker-compose.override.yml` — dev overrides with hot reload via `dotnet watch` and volume mounts

## Code Style
- C#: 4-space indent, nullable enabled, implicit usings
- TypeScript/JS/CSS/JSON: 2-space indent
- Line endings: LF
- Encoding: UTF-8
- See `.editorconfig` for full rules

## EF Core Version Pinning
EF Core packages are pinned to 9.0.13 and Npgsql to 9.0.4 across both Data and Api projects to avoid version conflicts. The Design package must be in both projects for migrations to work.

## Design Decisions

### MCP ↔ API Response Boundary
The API returns its own DTOs (defined in Shared). The MCP layer **maps** API responses to MCP-specific response classes before returning to agents. MCP response types (`OperationResult`, `ResponseType`, tool-specific detail responses) live **exclusively in `PinkRooster.Mcp/Responses/`** — never in Shared or API. Exception: `ProjectStatusResponse` lives in Shared because the API response is already agent-optimized (compact counts + item lists). Rationale: MCP responses are tailored for AI agent consumption (fewer fields, contextual hints), while API responses serve the dashboard with full data.

### MCP Tool Error Handling
MCP tools must **never throw exceptions**. Every code path — including API errors, validation failures, circular dependency rejection, not-found cases, and unexpected exceptions — must return an `OperationResult` string with a clear, actionable message the AI agent can act on. Wrap all API client calls in try-catch. `PinkRoosterApiClient` uses `EnsureSuccessAsync()` which extracts the error body from non-success HTTP responses via `ReadErrorMessageAsync` — all 16 endpoints use this uniformly. The agent should always understand **why** an operation failed and what it can do next.

### Human-Readable IDs
Business entities use human-readable ID formats derived at read-time from DB auto-increment `long` PKs. Never stored as a column. **No GUIDs** — all primary keys are `long` auto-increment.
- Projects: `proj-{Id}` (e.g., `proj-1`)
- Issues: `proj-{ProjectId}-issue-{IssueNumber}` (e.g., `proj-1-issue-3`) — IssueNumber is per-project sequential (not global), stored as `int` column on Issue entity

ID parsing utility: `IdParser` in `PinkRooster.Shared/Helpers/` with `TryParseProjectId` and `TryParseIssueId`.

### Entity Creation & Deletion Ownership
- **Creation**: MCP tools only (AI agents create entities). No create/edit UI in dashboard.
- **Deletion**: Dashboard only (with confirmation dialog). No MCP delete tools.
- **Viewing**: Both MCP tools and dashboard can read data, but receive different response shapes per the boundary above.

### Auto-Timestamps
`UpdatedAt` is auto-set via `SaveChangesAsync` override in `AppDbContext` using a single `ChangeTracker.Entries<IHasUpdatedAt>()` loop (all timestamped entities implement `IHasUpdatedAt`). `CreatedAt` uses DB default `now()`.

### State-Driven Timestamps (Issues, WPs, Tasks)
Entities implementing `IHasStateTimestamps` (Issue, WorkPackage, WorkPackageTask) have `StartedAt`, `CompletedAt`, `ResolvedAt` fields **never set by callers** — they are computed via `StateTransitionHelper.ApplyStateTimestamps()` based on `CompletionState` transitions:
- `StartedAt` — set once when transitioning from NotStarted/Blocked → any Active state (never cleared)
- `CompletedAt` — set when → Completed (cleared if moving out of terminal)
- `ResolvedAt` — set when → any Terminal state (cleared if moving out of terminal)
- Same-state transitions are no-ops for timestamps

Entities implementing `IHasBlockedState` (WorkPackage, WorkPackageTask) additionally have `PreviousActiveState` managed via `StateTransitionHelper.ApplyBlockedStateLogic()` — captured on transition to Blocked, cleared on transition from Blocked.

### Issue Audit Log
Full-field audit via `IssueAuditLog` table (separate from `ActivityLog` which is HTTP request logging). Every field change on create/update produces an audit entry with FieldName, OldValue, NewValue, ChangedBy, ChangedAt. On creation, all fields logged with OldValue = null. Deletion does not create audit entries (covered by ActivityLog middleware).

### Partial Updates (PATCH Semantics)
`UpdateIssueRequest` has all-nullable fields. `null` means "don't change" — **no field clearing support** (once set, optional fields can only be overwritten, never set back to null).

### Nested API Routes (Issues)
Issues use nested routes under projects: `api/projects/{projectId:long}/issues`. POST for create (201), PATCH for partial update (200). This differs from the Project entity's flat PUT upsert pattern.

### CompletionState Enum
Shared enum used for Issue state tracking. Three categories defined in `CompletionStateConstants`: ActiveStates (Designing, Implementing, Testing, InReview), InactiveStates (Blocked, NotStarted), TerminalStates (Completed, Cancelled, Replaced). All state transitions are allowed (no validation).

### Per-Project Sequential Numbering
`IssueNumber` assigned via `SELECT MAX(issue_number) + 1` in a serializable transaction. Gaps are allowed (deletion doesn't reuse numbers). Number is immutable after creation.

### File Attachments
`FileReference` is an owned type stored as a jsonb column on the Issue entity via `OwnsMany(...).ToJson()`. Metadata only (FileName, RelativePath, Description) — no file upload infrastructure.

### Testing Strategy
Integration tests use **Testcontainers** (real PostgreSQL 17 in Docker) + **Respawn** (database reset between tests) + **WebApplicationFactory** (in-process API). Uses xUnit v3 with collection fixtures to share a single Postgres container across all test classes. Key patterns:
- `PostgresFixture` — starts container once, runs EF migrations once, provides Respawn reset
- `ApiFactory` — configures `WebApplicationFactory<Program>` with test DB + API key
- `IntegrationTest` — base class providing authenticated `HttpClient` and per-test DB reset
- `Program` class exposed via `public partial class Program;` at bottom of `Program.cs`
- Use `TestContext.Current.CancellationToken` in all async test methods (xUnit v3 requirement)

### Dashboard Routing
- Flat routes for top-level pages: `/projects`, `/activity`
- Nested routes for entity detail: `/projects/:id` (project detail + issue list), `/projects/:id/issues/:issueNumber` (issue detail)
- Project switcher click navigates to `/projects/:id` (not dashboard home)

### Shared Infrastructure (API)
Domain logic shared across services is centralized in:
- **`StateTransitionHelper`** (static) — `ApplyStateTimestamps()`, `ApplyBlockedStateLogic()`, `MapFileReferences()`. Operates on `IHasStateTimestamps` / `IHasBlockedState` interfaces.
- **`StateCascadeService`** (DI-registered) — `PropagateStateUpwardAsync()`, `AutoUnblockDependentWpsAsync()`, `AutoUnblockDependentTasksAsync()`, `HasCircularWpDependencyAsync()`, `HasCircularTaskDependencyAsync()`. Owns all cross-entity state transitions.
- **`ResponseMapper`** (static) — `MapTask()`, `MapPhase()`, `MapFileReferences()`, `MapAcceptanceCriterion()`, dependency mapping. Shared by `WorkPackageService`, `WorkPackageTaskService`, `PhaseService`.
- **`HttpContextExtensions`** — `GetCallerIdentity()` extension method used by all 4 controllers.
- **Marker interfaces** in `PinkRooster.Data.Entities` — `IHasUpdatedAt`, `IHasStateTimestamps`, `IHasBlockedState`.

### Shared Infrastructure (MCP)
- **`McpInputParser`** (internal static in `Helpers/`) — `MapFileReferences()`, `MapAcceptanceCriteria()`, `MapCreateTasks()`, `MapUpsertTasks()`, `MapScaffoldPhases()`, `NullIfEmpty()`, `IsTerminalState()`. Shared by all MCP tool classes.
- **MCP-specific enums** (in `Inputs/`) — `DependencyAction` (Add/Remove), `StateFilterCategory` (Active/Inactive/Terminal), `EntityTypeFilter` (Task/Wp/Issue). Provide schema-level validation for constrained string parameters.
- **MCP input types** (in `Inputs/`) — `FileReferenceInput`, `AcceptanceCriterionInput`, `PhaseTaskInput`, `ScaffoldPhaseInput`/`ScaffoldTaskInput`, `BatchTaskStateInput`. Map to shared DTOs via `McpInputParser`.
- **MCP tool annotations** — All 16 tools have `Title` and `OpenWorld = false`. Read tools: `ReadOnly = true`. Write tools: `Destructive = false`. Idempotent tools (`create_or_update_project`, `batch_update_task_states`, `manage_*_dependency`): `Idempotent = true`.
- **MCP tool classes** are split by entity domain: `ProjectTools`, `IssueTools`, `WorkPackageTools`, `PhaseTools`, `TaskTools`.

### State Change Cascade Notifications
When automatic state transitions occur (auto-block, auto-unblock, upward propagation), the API response includes a `StateChanges` list so MCP tools can report them to AI agents. The cascade flows:
- **Auto-block**: Adding a dependency on a non-terminal entity auto-transitions active dependents to `Blocked`, capturing `PreviousActiveState`
- **Auto-unblock**: Completing a blocker (or removing last dependency) restores dependents to their `PreviousActiveState`
- **Upward propagation**: All tasks terminal → Phase auto-completes → All phases terminal → WP auto-completes

Implementation pattern:
- `StateChangeDto` in Shared (`EntityType`, `EntityId`, `OldState`, `NewState`, `Reason`)
- Response DTOs (`WorkPackageResponse`, `PhaseResponse`, `TaskResponse`, `DependencyResponse`, `TaskDependencyResponse`) have optional `StateChanges` field
- Services always create an internal `stateChanges` list (`stateChanges ??= []`) and set it on the response DTO before returning
- Controllers pass `ct:` named parameter to skip the optional `stateChanges` param (they get state changes in the response body automatically)
- `OperationResult` in MCP has `Id`, `NextStep`, and `StateChanges` fields (all `JsonIgnoreCondition.WhenWritingNull`)
- MCP tools extract `response.StateChanges` from API response DTOs and pass to `OperationResult.Success(id, message, stateChanges: ...)`
- Remove-dependency endpoints return `bool`/`NoContent` so state changes are not reported for that path (niche case)

### MCP OperationResult Format
Write operations return a structured JSON response:
```json
{
  "responseType": "Success",
  "message": "Work package 'proj-1-wp-3' updated.",
  "id": "proj-1-wp-3",
  "nextStep": null,
  "stateChanges": [
    {
      "entityType": "WorkPackage",
      "entityId": "proj-1-wp-4",
      "oldState": "Blocked",
      "newState": "Implementing",
      "reason": "Auto-unblocked: blocker 'proj-1-wp-3' completed"
    }
  ]
}
```
Fields `id`, `nextStep`, and `stateChanges` are omitted when null. Use `OperationResult.Success(id, message)` for entity operations and `OperationResult.SuccessMessage(message)` for informational messages without an entity ID.

### Design Documents
Detailed design specs live in `claudedocs/`. Current docs:
- `claudedocs/design_project_entity.md` — Project entity full vertical slice (entity, API, MCP tools, dashboard)
- `claudedocs/design_issue_entity.md` — Issue entity full vertical slice design
- `claudedocs/workflow_issue_entity.md` — Issue entity implementation workflow (6 phases)
- `claudedocs/design_work_packages.md` — Work Package entity full vertical slice design
- `claudedocs/workflow_work_packages.md` — Work Package implementation workflow
- `claudedocs/PROJECT_INDEX.md` — Comprehensive project documentation (architecture, entities, API endpoints, MCP tools, file tree)
- `claudedocs/SOLID_ANALYSIS.md` — SOLID principles analysis (14 findings, all resolved)
