# Issue Entity — Implementation Workflow

> **Design doc**: `claudedocs/design_issue_entity.md`
> **Status**: Not started

---

## Phase A: Shared Layer (enums, DTOs, constants)

No dependencies. All files in `src/PinkRooster.Shared/`.

### Step A1: Enums
Create 5 enum files in `Enums/`:
- `CompletionState.cs` — 9 values (NotStarted through Replaced)
- `CompletionStateConstants.cs` — static class with ActiveStates, InactiveStates, TerminalStates HashSets
- `IssueType.cs` — Bug, Defect, Regression, TechnicalDebt, PerformanceIssue, SecurityVulnerability
- `IssueSeverity.cs` — Critical, Major, Minor, Trivial
- `Priority.cs` — Critical, High, Medium, Low

**Checkpoint**: `dotnet build src/PinkRooster.Shared` compiles

### Step A2: DTOs
Create in `DTOs/`:
- `Requests/FileReferenceDto.cs` — FileName (required), RelativePath (required), Description (optional)
- `Requests/CreateIssueRequest.cs` — all definition fields required, reproduction/resolution/attachments optional, State defaults NotStarted, Priority defaults Medium
- `Requests/UpdateIssueRequest.cs` — ALL fields nullable (null = don't change, no field clearing support)
- `Responses/IssueResponse.cs` — full issue shape with IssueId (`proj-{N}-issue-{N}`), ProjectId, all fields, enums as string
- `Responses/IssueSummaryResponse.cs` — ActiveCount, InactiveCount, TerminalCount, LatestTerminalIssues list
- `Responses/IssueAuditLogResponse.cs` — FieldName, OldValue, NewValue, ChangedBy, ChangedAt

**Checkpoint**: `dotnet build src/PinkRooster.Shared` compiles

### Step A3: Constants
Update `Constants/ApiRoutes.cs`:
- Add `Issues` nested class (note: route template uses `{projectId:long}` placeholder, but the constant is just the base pattern for reference)

Create `Helpers/IdParser.cs`:
- `TryParseProjectId("proj-1")` → extracts `1`
- `TryParseIssueId("proj-1-issue-3")` → extracts `(1, 3)`
- Return false on invalid format

**Checkpoint**: `dotnet build src/PinkRooster.Shared` compiles

---

## Phase B: Data Layer (entities, EF config, migration)

Depends on: Phase A complete.

### Step B1: Entities
Create in `src/PinkRooster.Data/Entities/`:
- `FileReference.cs` — owned type (not standalone entity). FileName, RelativePath, Description.
- `Issue.cs` — sealed class, long Id, int IssueNumber, long ProjectId, Project nav prop, all fields from design, `List<FileReference> Attachments = []`, DateTimeOffset timestamps
- `IssueAuditLog.cs` — sealed class, long Id, long IssueId, Issue nav prop, FieldName, OldValue, NewValue, ChangedBy, ChangedAt

### Step B2: EF Configurations
Create in `src/PinkRooster.Data/Configurations/`:
- `IssueConfiguration.cs`:
  - Table `"issues"`, all columns snake_case
  - FK to projects with cascade delete
  - Unique composite index on (project_id, issue_number)
  - Indexes on state, priority, severity, project_id
  - Enums as string conversions with defaults
  - `OwnsMany(x => x.Attachments, a => a.ToJson("attachments"))`
  - Timestamps with `HasDefaultValueSql("now()")`
- `IssueAuditLogConfiguration.cs`:
  - Table `"issue_audit_logs"`
  - FK to issues with cascade delete
  - Index on issue_id
  - `changed_at` with `HasDefaultValueSql("now()")`

### Step B3: AppDbContext
Update `src/PinkRooster.Data/AppDbContext.cs`:
- Add `public DbSet<Issue> Issues => Set<Issue>();`
- Add `public DbSet<IssueAuditLog> IssueAuditLogs => Set<IssueAuditLog>();`
- Extend `SaveChangesAsync` to track Issue entities for UpdatedAt (alongside existing Project tracking)

### Step B4: Migration
Run: `dotnet ef migrations add AddIssueEntity --project src/PinkRooster.Data --startup-project src/PinkRooster.Api`

**Checkpoint**: `dotnet build PinkRooster.slnx` compiles, migration file generated, review migration SQL for correctness

---

## Phase C: API Layer (service, controller, DI)

Depends on: Phase B complete.

### Step C1: Service Interface
Create `src/PinkRooster.Api/Services/IIssueService.cs`:
- `GetByProjectAsync(long projectId, string? stateFilter, CancellationToken ct)` → `List<IssueResponse>`
- `GetByNumberAsync(long projectId, int issueNumber, CancellationToken ct)` → `IssueResponse?`
- `GetSummaryAsync(long projectId, CancellationToken ct)` → `IssueSummaryResponse`
- `GetAuditLogAsync(long projectId, int issueNumber, CancellationToken ct)` → `List<IssueAuditLogResponse>`
- `CreateAsync(long projectId, CreateIssueRequest request, string changedBy, CancellationToken ct)` → `IssueResponse`
- `UpdateAsync(long projectId, int issueNumber, UpdateIssueRequest request, string changedBy, CancellationToken ct)` → `IssueResponse?`
- `DeleteAsync(long projectId, int issueNumber, CancellationToken ct)` → `bool`

### Step C2: Service Implementation
Create `src/PinkRooster.Api/Services/IssueService.cs`:

**Constructor**: primary constructor with `AppDbContext db, IHttpContextAccessor httpContextAccessor`

**CreateAsync** critical logic:
1. Verify project exists (return error/throw if not)
2. Begin serializable transaction
3. `SELECT MAX(issue_number) FROM issues WHERE project_id = @id` (+ 1, default 1)
4. Create Issue entity with IssueNumber
5. Apply state timestamp logic (if initial state is active, set StartedAt)
6. Generate audit entries for all fields (OldValue = null for each)
7. AddRange audit entries + Add issue
8. SaveChanges + commit transaction
9. Return ToResponse(issue)

**UpdateAsync** critical logic:
1. Find issue by (projectId, issueNumber), return null if not found
2. For each non-null field in request: compare with current value, if different → add audit entry + apply change
3. Apply state timestamp logic if State changed:
   - NotStarted/Blocked → Active state: set StartedAt (if null)
   - Any → Completed: set CompletedAt
   - Any → Terminal: set ResolvedAt
   - Terminal → non-Terminal: clear CompletedAt/ResolvedAt
   - Same state: no timestamp change
4. AddRange audit entries
5. SaveChanges
6. Return ToResponse(issue)

**GetByProjectAsync**: filter by projectId, optionally by state category (parse "active"/"inactive"/"terminal" to enum sets from CompletionStateConstants), order by CreatedAt desc

**GetSummaryAsync**: counts per category + latest 10 terminal issues ordered by ResolvedAt desc

**ToResponse**: maps entity to IssueResponse, generates `IssueId = $"proj-{i.ProjectId}-issue-{i.IssueNumber}"`

### Step C3: Controller
Create `src/PinkRooster.Api/Controllers/IssueController.cs`:
- Route: `api/projects/{projectId:long}/issues`
- GET (list with optional `?state=` filter)
- GET `{issueNumber:int}` (single)
- GET `{issueNumber:int}/audit` (audit log)
- GET `summary` (counts + terminal)
- POST (create, returns 201 Created)
- PATCH `{issueNumber:int}` (partial update, returns 200)
- DELETE `{issueNumber:int}` (returns 204/404)
- Extract `changedBy` from `HttpContext.Items["CallerIdentity"]` for create/update calls

### Step C4: DI Registration
Update `src/PinkRooster.Api/Program.cs`:
- Add `builder.Services.AddScoped<IIssueService, IssueService>();`
- Ensure `IHttpContextAccessor` is registered (may already be via AddControllers or needs `builder.Services.AddHttpContextAccessor()`)

**Checkpoint**: `dotnet build PinkRooster.slnx` compiles. Start API (`make dev-api`), verify endpoints appear in Swagger at localhost:5100/swagger. Test create/get/update/delete manually via Swagger against a running Postgres.

---

## Phase D: MCP Layer (client, responses, tools)

Depends on: Phase C complete.

### Step D1: API Client Methods
Update `src/PinkRooster.Mcp/Clients/PinkRoosterApiClient.cs`:
- `GetIssuesByProjectAsync(long projectId, string? stateFilter, CancellationToken ct)` — GET with 404 → empty list
- `GetIssueAsync(long projectId, int issueNumber, CancellationToken ct)` — GET with 404 → null
- `GetIssueSummaryAsync(long projectId, CancellationToken ct)` — GET
- `CreateIssueAsync(long projectId, CreateIssueRequest request, CancellationToken ct)` — POST, returns IssueResponse
- `UpdateIssueAsync(long projectId, int issueNumber, UpdateIssueRequest request, CancellationToken ct)` — PATCH with 404 → null

### Step D2: MCP Response Types
Create in `src/PinkRooster.Mcp/Responses/`:
- `IssueDetailResponse.cs` — full issue data for AI agents (no audit trail). All fields from IssueResponse minus raw Id.
- `IssueOverviewItem.cs` — compact: IssueId, Name, State, Priority, Severity, IssueType, CreatedAt

### Step D3: Issue Tools
Create `src/PinkRooster.Mcp/Tools/IssueTools.cs`:

**add_or_update_issue**:
- Parameters: projectId (required), issueId (optional), name, description, issueType, severity, priority, state, stepsToReproduce, expectedBehavior, actualBehavior, affectedComponent, stackTrace, rootCause, resolution, attachments (JSON string)
- If issueId null → validate required fields (name, description, issueType, severity), build CreateIssueRequest, call POST
- If issueId set → parse to projectId + issueNumber, build UpdateIssueRequest (only non-null fields), call PATCH
- Return OperationResult.Success/Error

**get_issue_details** (ReadOnly = true):
- Parameter: issueId (required, "proj-1-issue-3" format)
- Parse → call GET → map to IssueDetailResponse → serialize JSON

**get_issue_overview** (ReadOnly = true):
- Parameters: projectId (required), stateFilter (optional)
- Parse → call GET list → map to IssueOverviewItem list → serialize JSON

### Step D4: Update ProjectTools + ProjectOverviewResponse
Update `src/PinkRooster.Mcp/Responses/ProjectOverviewResponse.cs`:
- Add: `public int ActiveIssueCount { get; init; }`, `public int InactiveIssueCount { get; init; }`, `public List<TerminalIssueItem> LatestTerminalIssues { get; init; } = []`
- Add `TerminalIssueItem` class: IssueId, Name, State, ResolvedAt

Update `src/PinkRooster.Mcp/Tools/ProjectTools.cs` → `GetProjectOverview`:
- After fetching project, parse projectId number, call `GetIssueSummaryAsync`
- Populate new fields on ProjectOverviewResponse

**Checkpoint**: `dotnet build PinkRooster.slnx` compiles. Start all services (`make up`), test MCP tools via Claude Desktop or MCP inspector.

---

## Phase E: Dashboard

Depends on: Phase C complete (API endpoints available).

### Step E1: Types
Update `src/dashboard/src/types/index.ts`:
- Add `Issue`, `FileReference`, `IssueSummary`, `IssueAuditLog` interfaces

### Step E2: API Functions
Create `src/dashboard/src/api/issues.ts`:
- `getIssues(projectId: number, state?: string)` — GET `/projects/${projectId}/issues`
- `getIssue(projectId: number, issueNumber: number)` — GET `/projects/${projectId}/issues/${issueNumber}`
- `getIssueSummary(projectId: number)` — GET `/projects/${projectId}/issues/summary`
- `getIssueAuditLog(projectId: number, issueNumber: number)` — GET `/projects/${projectId}/issues/${issueNumber}/audit`
- `deleteIssue(projectId: number, issueNumber: number)` — DELETE `/projects/${projectId}/issues/${issueNumber}`

### Step E3: Hooks
Create `src/dashboard/src/hooks/use-issues.ts`:
- `useIssues(projectId: number | undefined, stateFilter?: string)` — enabled when projectId defined
- `useIssue(projectId: number, issueNumber: number)`
- `useIssueSummary(projectId: number | undefined)` — enabled when projectId defined
- `useIssueAuditLog(projectId: number, issueNumber: number)`
- `useDeleteIssue()` — invalidates `["issues"]` on success

### Step E4: ProjectDetailPage
Create `src/dashboard/src/pages/project-detail-page.tsx`:
- Route: `/projects/:id` (extract `id` from useParams)
- Fetch project from `useProjects()` data (or add a `getProject(id)` API call)
- Header section: project name, projectId badge, status badge, description, path
- Summary cards row: Active Issues, Inactive Issues, Terminal Issues (from useIssueSummary)
- State filter: tab-style buttons (All | Active | Inactive | Terminal) controlling local state
- Issue table: IssueId (badge), Name, Type, Severity badge (colored by level), Priority badge, State badge (colored by category), Created date
- Row click → navigate to `/projects/${id}/issues/${issueNumber}`
- Delete button per row → AlertDialog confirmation → useDeleteIssue mutation
- Loading state: skeleton text
- Empty state: card with icon "No issues found"

### Step E5: IssueDetailPage
Create `src/dashboard/src/pages/issue-detail-page.tsx`:
- Route: `/projects/:id/issues/:issueNumber`
- Back button → `/projects/${id}`
- Header: issue name, issueId badge, state badge (colored)
- Cards layout:
  - **Definition card**: IssueType, Severity, Priority as badges
  - **Reproduction card** (shown only if any repro field has data): StepsToReproduce, ExpectedBehavior, ActualBehavior, AffectedComponent, StackTrace (in `<pre>` or code block, collapsible if long)
  - **Resolution card** (shown only if rootCause or resolution has data): RootCause, Resolution
  - **Attachments card** (shown only if attachments non-empty): table of FileName, RelativePath, Description
  - **Timeline card**: StartedAt, CompletedAt, ResolvedAt, CreatedAt, UpdatedAt — formatted dates, null shown as "—"
  - **Audit Log card**: table of all entries from useIssueAuditLog, columns: Timestamp, Field, Old Value, New Value, Changed By. All entries shown (no pagination). Loading skeleton while fetching.
- Delete button in header → AlertDialog → navigate back to project detail on success

### Step E6: Routing
Update `src/dashboard/src/App.tsx`:
- Add route: `<Route path="projects/:id" element={<ProjectDetailPage />} />`
- Add route: `<Route path="projects/:id/issues/:issueNumber" element={<IssueDetailPage />} />`

### Step E7: Navigation Updates
Update `src/dashboard/src/components/layout/project-switcher.tsx`:
- Change: clicking a project → `navigate(`/projects/${project.id}`)` instead of `navigate("/")`

Update `src/dashboard/src/pages/project-list-page.tsx`:
- Change: row click → `navigate(`/projects/${project.id}`)` instead of `navigate("/")`

Update `src/dashboard/src/components/layout/app-sidebar.tsx`:
- Add "Issues" nav item that links to `/projects/${selectedProject.id}` (visible only when selectedProject is set)

**Checkpoint**: `make dev-dashboard` starts without errors. Navigate to `/projects/:id`, verify issue list renders. Navigate to issue detail, verify all cards render. Test delete flow.

---

## Phase F: Integration Tests

Depends on: Phase C complete.

### Step F1: Issue Integration Tests
Create `tests/PinkRooster.Api.Tests/IssueEndpointTests.cs`:

Test cases:
1. **Create issue** — POST to project, verify 201, verify IssueNumber = 1, verify response shape
2. **Create second issue** — verify IssueNumber = 2
3. **Get issue by number** — verify response matches created data
4. **Get issues list** — verify returns all issues for project
5. **Get issues with state filter** — create issues in different states, filter by "active"/"terminal"
6. **Update issue fields** — PATCH with partial data, verify only changed fields affected
7. **Update state → timestamps** — change state to Implementing, verify StartedAt set. Change to Completed, verify CompletedAt + ResolvedAt set.
8. **Get issue summary** — verify counts match
9. **Get audit log** — after create + update, verify audit entries exist with correct field/old/new values
10. **Delete issue** — verify 204, verify GET returns 404
11. **Issue not found** — GET/PATCH/DELETE nonexistent issue returns 404
12. **Project not found** — POST to nonexistent project returns 404
13. **Concurrent creation** — (optional) verify no duplicate IssueNumbers under concurrent POSTs

Pattern: extend existing `IntegrationTest` base class, use `TestContext.Current.CancellationToken`.

**Checkpoint**: `dotnet test tests/PinkRooster.Api.Tests` — all tests pass (existing + new)

---

## Execution Order Summary

```
Phase A (Shared)  ──┐
                    ├── Phase B (Data) ── Phase C (API) ──┬── Phase D (MCP)
                    │                                      ├── Phase E (Dashboard)
                    │                                      └── Phase F (Tests)
                    │
                    └── Phases D, E, F can run in parallel after C
```

Estimated file count:
- **New files**: ~20 (5 enums, 6 DTOs, 3 entities, 2 EF configs, 2 service files, 1 controller, 1 MCP tools, 2 MCP responses, 1 dashboard API, 1 hooks, 2 pages, 1 test, 1 migration)
- **Modified files**: ~8 (AppDbContext, ApiRoutes, PinkRoosterApiClient, ProjectTools, ProjectOverviewResponse, App.tsx, app-sidebar, project-switcher, project-list-page, types/index.ts, Program.cs)
