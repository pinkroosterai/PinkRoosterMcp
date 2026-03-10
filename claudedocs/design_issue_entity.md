# Issue Entity — Full Vertical Slice Design

## Overview

Add an **Issue** entity tied to Projects, with MCP tools for AI agent CRUD, a full-field audit log, and dashboard views nested under projects.

**Human-readable ID format**: `proj-{ProjectId}-issue-{IssueNumber}` (derived at read-time, never stored).

---

## 1. Enums (PinkRooster.Shared/Enums/)

All enums stored as strings in DB. Placed in Shared because both API and MCP layers reference them via DTOs.

### CompletionState.cs
```csharp
public enum CompletionState
{
    NotStarted = 0,
    Designing = 1,
    Implementing = 2,
    Testing = 3,
    InReview = 4,
    Completed = 5,
    Cancelled = 6,
    Blocked = 7,
    Replaced = 8
}
```

### CompletionStateConstants.cs
```csharp
public static class CompletionStateConstants
{
    public static readonly HashSet<CompletionState> ActiveStates =
        [CompletionState.Designing, CompletionState.Implementing, CompletionState.Testing, CompletionState.InReview];

    public static readonly HashSet<CompletionState> InactiveStates =
        [CompletionState.Blocked, CompletionState.NotStarted];

    public static readonly HashSet<CompletionState> TerminalStates =
        [CompletionState.Completed, CompletionState.Cancelled, CompletionState.Replaced];
}
```

### IssueType.cs
```csharp
public enum IssueType
{
    Bug,
    Defect,
    Regression,
    TechnicalDebt,
    PerformanceIssue,
    SecurityVulnerability
}
```

### IssueSeverity.cs
```csharp
public enum IssueSeverity
{
    Critical,
    Major,
    Minor,
    Trivial
}
```

### Priority.cs
```csharp
public enum Priority
{
    Critical,
    High,
    Medium,
    Low
}
```

---

## 2. Entities (PinkRooster.Data/Entities/)

### Issue.cs
```csharp
public sealed class Issue
{
    public long Id { get; set; }
    public int IssueNumber { get; set; }          // per-project sequential, immutable after creation
    public long ProjectId { get; set; }            // FK
    public Project Project { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public IssueType IssueType { get; set; }
    public IssueSeverity Severity { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;

    // ── Reproduction / diagnosis ──
    public string? StepsToReproduce { get; set; }
    public string? ExpectedBehavior { get; set; }
    public string? ActualBehavior { get; set; }
    public string? AffectedComponent { get; set; }
    public string? StackTrace { get; set; }

    // ── Resolution ──
    public string? RootCause { get; set; }
    public string? Resolution { get; set; }

    // ── State ──
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // ── Attachments ──
    public List<FileReference> Attachments { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### FileReference.cs (owned type, not a standalone entity)
```csharp
public sealed class FileReference
{
    public required string FileName { get; set; }
    public required string RelativePath { get; set; }
    public string? Description { get; set; }
}
```

### IssueAuditLog.cs
```csharp
public sealed class IssueAuditLog
{
    public long Id { get; set; }
    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public required string FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public required string ChangedBy { get; set; }   // truncated API key from middleware
    public DateTimeOffset ChangedAt { get; set; }
}
```

---

## 3. EF Configuration (PinkRooster.Data/Configurations/)

### IssueConfiguration.cs
```
Table: "issues"
Columns: id, issue_number, project_id, name (200), description (4000), issue_type (30),
         severity (20), priority (20), steps_to_reproduce (4000), expected_behavior (4000),
         actual_behavior (4000), affected_component (500), stack_trace (8000),
         root_cause (4000), resolution (4000), state (20), started_at, completed_at,
         resolved_at, created_at, updated_at

FK: project_id → projects.id (CASCADE delete)
Unique composite index: (project_id, issue_number)
Indexes: project_id, state, priority, severity

Attachments: OwnsMany → ToJson("attachments") → stored as jsonb column
Enums: HasConversion<string>() with HasDefaultValue
Timestamps: HasDefaultValueSql("now()") for created_at and updated_at
```

### IssueAuditLogConfiguration.cs
```
Table: "issue_audit_logs"
Columns: id, issue_id, field_name (100), old_value (8000), new_value (8000),
         changed_by (100), changed_at

FK: issue_id → issues.id (CASCADE delete — audit logs deleted with issue)
Index: issue_id
Timestamp: HasDefaultValueSql("now()") for changed_at
```

### AppDbContext changes
- Add `DbSet<Issue> Issues => Set<Issue>();`
- Add `DbSet<IssueAuditLog> IssueAuditLogs => Set<IssueAuditLog>();`
- Extend `SaveChangesAsync` to also track `Issue` entities for `UpdatedAt`

---

## 4. Shared DTOs (PinkRooster.Shared/DTOs/)

### Requests/CreateIssueRequest.cs
```csharp
public sealed class CreateIssueRequest
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IssueType IssueType { get; init; }
    public required IssueSeverity Severity { get; init; }
    public Priority Priority { get; init; } = Priority.Medium;
    public string? StepsToReproduce { get; init; }
    public string? ExpectedBehavior { get; init; }
    public string? ActualBehavior { get; init; }
    public string? AffectedComponent { get; init; }
    public string? StackTrace { get; init; }
    public string? RootCause { get; init; }
    public string? Resolution { get; init; }
    public CompletionState State { get; init; } = CompletionState.NotStarted;
    public List<FileReferenceDto>? Attachments { get; init; }
}
```

### Requests/UpdateIssueRequest.cs
All fields nullable — only non-null fields are applied.
```csharp
public sealed class UpdateIssueRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public IssueType? IssueType { get; init; }
    public IssueSeverity? Severity { get; init; }
    public Priority? Priority { get; init; }
    public string? StepsToReproduce { get; init; }
    public string? ExpectedBehavior { get; init; }
    public string? ActualBehavior { get; init; }
    public string? AffectedComponent { get; init; }
    public string? StackTrace { get; init; }
    public string? RootCause { get; init; }
    public string? Resolution { get; init; }
    public CompletionState? State { get; init; }
    public List<FileReferenceDto>? Attachments { get; init; }
}
```

### Requests/FileReferenceDto.cs
```csharp
public sealed class FileReferenceDto
{
    public required string FileName { get; init; }
    public required string RelativePath { get; init; }
    public string? Description { get; init; }
}
```

### Responses/IssueResponse.cs
```csharp
public sealed class IssueResponse
{
    public required string IssueId { get; init; }         // "proj-1-issue-3"
    public required long Id { get; init; }                 // raw PK (for dashboard delete)
    public required int IssueNumber { get; init; }
    public required string ProjectId { get; init; }        // "proj-1"
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string IssueType { get; init; }
    public required string Severity { get; init; }
    public required string Priority { get; init; }
    public string? StepsToReproduce { get; init; }
    public string? ExpectedBehavior { get; init; }
    public string? ActualBehavior { get; init; }
    public string? AffectedComponent { get; init; }
    public string? StackTrace { get; init; }
    public string? RootCause { get; init; }
    public string? Resolution { get; init; }
    public required string State { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public required List<FileReferenceDto> Attachments { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
```

### Responses/IssueSummaryResponse.cs
Used by the updated `get_project_overview` MCP tool.
```csharp
public sealed class IssueSummaryResponse
{
    public required int ActiveCount { get; init; }
    public required int InactiveCount { get; init; }
    public required int TerminalCount { get; init; }
    public required List<IssueResponse> LatestTerminalIssues { get; init; }
}
```

### Responses/IssueAuditLogResponse.cs
```csharp
public sealed class IssueAuditLogResponse
{
    public required string FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public required string ChangedBy { get; init; }
    public required DateTimeOffset ChangedAt { get; init; }
}
```

---

## 5. API Routes

### ApiRoutes.cs — add:
```csharp
public static class Issues
{
    public const string Route = $"{Base}/projects/{{projectId:long}}/issues";
}
```

Endpoints:
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/projects/{projectId}/issues?state={active\|inactive\|terminal}` | List issues, optional state filter |
| `GET` | `/api/projects/{projectId}/issues/{issueNumber}` | Get single issue by number |
| `GET` | `/api/projects/{projectId}/issues/{issueNumber}/audit` | Get audit log for an issue |
| `GET` | `/api/projects/{projectId}/issues/summary` | Counts + latest 10 terminal |
| `POST` | `/api/projects/{projectId}/issues` | Create issue (returns 201) |
| `PATCH` | `/api/projects/{projectId}/issues/{issueNumber}` | Partial update (returns 200) |
| `DELETE` | `/api/projects/{projectId}/issues/{issueNumber}` | Delete (returns 204) |

---

## 6. Service Layer (PinkRooster.Api/Services/)

### IIssueService.cs
```csharp
public interface IIssueService
{
    Task<List<IssueResponse>> GetByProjectAsync(long projectId, string? stateFilter, CancellationToken ct);
    Task<IssueResponse?> GetByNumberAsync(long projectId, int issueNumber, CancellationToken ct);
    Task<IssueSummaryResponse> GetSummaryAsync(long projectId, CancellationToken ct);
    Task<List<IssueAuditLogResponse>> GetAuditLogAsync(long projectId, int issueNumber, CancellationToken ct);
    Task<IssueResponse> CreateAsync(long projectId, CreateIssueRequest request, string changedBy, CancellationToken ct);
    Task<IssueResponse?> UpdateAsync(long projectId, int issueNumber, UpdateIssueRequest request, string changedBy, CancellationToken ct);
    Task<bool> DeleteAsync(long projectId, int issueNumber, CancellationToken ct);
}
```

### IssueService.cs — Key Logic

**IssueNumber assignment** (concurrency-safe):
```csharp
// Inside a serializable transaction or using FOR UPDATE on project row
var nextNumber = await db.Issues
    .Where(i => i.ProjectId == projectId)
    .MaxAsync(i => (int?)i.IssueNumber, ct) ?? 0;
nextNumber++;
```
Wrapped in `ExecutionStrategy` + serializable transaction to prevent race conditions.

**State-driven timestamps** (applied in service, not settable by callers):
```
StartedAt:   set ONCE when State transitions from NotStarted/Blocked → any Active state
CompletedAt: set when State transitions → Completed
ResolvedAt:  set when State transitions → any Terminal state (Completed/Cancelled/Replaced)
```
If State moves back out of terminal, `CompletedAt`/`ResolvedAt` are cleared. `StartedAt` is never cleared once set.

**Full-field audit** (in same transaction as save):
```csharp
// On create: log every field with OldValue = null
// On update: compare each field, log only changed fields
private void AuditField(List<IssueAuditLog> entries, long issueId, string field, string? oldVal, string? newVal, string changedBy)
{
    if (oldVal == newVal) return;
    entries.Add(new IssueAuditLog { IssueId = issueId, FieldName = field, OldValue = oldVal, NewValue = newVal, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow });
}
```

**CallerIdentity**: Injected via `IHttpContextAccessor` — reads from `HttpContext.Items["CallerIdentity"]` (set by `ApiKeyAuthMiddleware`).

**ToResponse mapper**:
```csharp
private static IssueResponse ToResponse(Issue i) => new()
{
    IssueId = $"proj-{i.ProjectId}-issue-{i.IssueNumber}",
    ProjectId = $"proj-{i.ProjectId}",
    // ... all fields mapped, enums as .ToString()
};
```

---

## 7. Controller (PinkRooster.Api/Controllers/)

### IssueController.cs
```csharp
[ApiController]
[Route("api/projects/{projectId:long}/issues")]
public sealed class IssueController(IIssueService issueService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll(long projectId, [FromQuery] string? state, CancellationToken ct)

    [HttpGet("{issueNumber:int}")]
    public async Task<ActionResult<IssueResponse>> GetByNumber(long projectId, int issueNumber, CancellationToken ct)

    [HttpGet("{issueNumber:int}/audit")]
    public async Task<ActionResult<List<IssueAuditLogResponse>>> GetAuditLog(long projectId, int issueNumber, CancellationToken ct)

    [HttpGet("summary")]
    public async Task<ActionResult<IssueSummaryResponse>> GetSummary(long projectId, CancellationToken ct)

    [HttpPost]
    public async Task<ActionResult<IssueResponse>> Create(long projectId, CreateIssueRequest request, CancellationToken ct)
    // Returns Created($".../issues/{response.IssueNumber}", response)

    [HttpPatch("{issueNumber:int}")]
    public async Task<ActionResult<IssueResponse>> Update(long projectId, int issueNumber, UpdateIssueRequest request, CancellationToken ct)

    [HttpDelete("{issueNumber:int}")]
    public async Task<ActionResult> Delete(long projectId, int issueNumber, CancellationToken ct)
}
```

---

## 8. MCP Layer

### PinkRoosterApiClient.cs — add methods:
```csharp
// GET /api/projects/{projectId}/issues?state={filter}
Task<List<IssueResponse>> GetIssuesByProjectAsync(long projectId, string? stateFilter, CancellationToken ct)

// GET /api/projects/{projectId}/issues/{issueNumber}
Task<IssueResponse?> GetIssueAsync(long projectId, int issueNumber, CancellationToken ct)

// GET /api/projects/{projectId}/issues/summary
Task<IssueSummaryResponse> GetIssueSummaryAsync(long projectId, CancellationToken ct)

// POST /api/projects/{projectId}/issues
Task<IssueResponse> CreateIssueAsync(long projectId, CreateIssueRequest request, CancellationToken ct)

// PATCH /api/projects/{projectId}/issues/{issueNumber}
Task<IssueResponse?> UpdateIssueAsync(long projectId, int issueNumber, UpdateIssueRequest request, CancellationToken ct)
```

### IssueTools.cs (PinkRooster.Mcp/Tools/)

#### add_or_update_issue
```
Parameters:
  - projectId (required): "proj-1" format
  - issueId (optional): "proj-1-issue-3" format — when provided, updates existing
  - name (required on create, optional on update)
  - description (required on create, optional on update)
  - issueType (required on create, optional on update)
  - severity (required on create, optional on update)
  - priority (optional, defaults Medium on create)
  - stepsToReproduce, expectedBehavior, actualBehavior, affectedComponent,
    stackTrace, rootCause, resolution (all optional)
  - state (optional, defaults NotStarted on create)
  - attachments (optional): JSON array of {fileName, relativePath, description?}

Logic:
  1. Parse projectId → extract numeric ID
  2. If issueId provided → parse issueNumber, call PATCH endpoint
  3. Else → call POST endpoint
  4. Return OperationResult.Success with the issueId
```

#### get_issue_details (ReadOnly = true)
```
Parameters:
  - issueId (required): "proj-1-issue-3" format

Logic:
  1. Parse → projectId + issueNumber
  2. Call GET endpoint
  3. Return MCP-specific IssueDetailResponse (issue data only, no audit trail)
```

#### get_issue_overview (ReadOnly = true)
```
Parameters:
  - projectId (required): "proj-1" format
  - stateFilter (optional): "active" | "inactive" | "terminal" | null (all)

Logic:
  1. Parse projectId
  2. Call GET list endpoint with filter
  3. Return MCP-specific IssueOverviewResponse (compact list)
```

#### get_project_overview — updated
```
Existing behavior preserved. After fetching project, also call:
  GET /api/projects/{projectId}/issues/summary

Add to ProjectOverviewResponse:
  - ActiveIssueCount
  - InactiveIssueCount
  - LatestTerminalIssues (list of {issueId, name, state, resolvedAt})
```

### MCP Response Types (PinkRooster.Mcp/Responses/)

#### IssueDetailResponse.cs
Tailored for AI agents — issue data only (no audit trail).
```csharp
public sealed class IssueDetailResponse
{
    public required string IssueId { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string IssueType { get; init; }
    public required string Severity { get; init; }
    public required string Priority { get; init; }
    public required string State { get; init; }
    // reproduction fields...
    // resolution fields...
    public required List<FileReferenceDto> Attachments { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
```

#### IssueOverviewItem.cs
Compact representation for list views.
```csharp
public sealed class IssueOverviewItem
{
    public required string IssueId { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public required string Priority { get; init; }
    public required string Severity { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
```

---

## 9. Dashboard

### New Routes
```
/projects/:id          → ProjectDetailPage (project info + issue list)
/projects/:id/issues/:issueNumber → IssueDetailPage
```

### Modified Routes
```
/projects              → ProjectListPage (row click → /projects/:id instead of /)
```

### TypeScript Types (src/types/index.ts)
```typescript
interface Issue {
  issueId: string;        // "proj-1-issue-3"
  id: number;
  issueNumber: number;
  projectId: string;
  name: string;
  description: string;
  issueType: string;
  severity: "Critical" | "Major" | "Minor" | "Trivial";
  priority: "Critical" | "High" | "Medium" | "Low";
  stepsToReproduce: string | null;
  expectedBehavior: string | null;
  actualBehavior: string | null;
  affectedComponent: string | null;
  stackTrace: string | null;
  rootCause: string | null;
  resolution: string | null;
  state: string;
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  attachments: FileReference[];
  createdAt: string;
  updatedAt: string;
}

interface FileReference {
  fileName: string;
  relativePath: string;
  description: string | null;
}

interface IssueSummary {
  activeCount: number;
  inactiveCount: number;
  terminalCount: number;
  latestTerminalIssues: Issue[];
}

interface IssueAuditLog {
  fieldName: string;
  oldValue: string | null;
  newValue: string | null;
  changedBy: string;
  changedAt: string;
}
```

### API Layer (src/api/issues.ts)
```typescript
getIssues(projectId: number, state?: string): Promise<Issue[]>
getIssue(projectId: number, issueNumber: number): Promise<Issue>
getIssueSummary(projectId: number): Promise<IssueSummary>
getIssueAuditLog(projectId: number, issueNumber: number): Promise<IssueAuditLog[]>
deleteIssue(projectId: number, issueNumber: number): Promise<void>
```

### Hooks (src/hooks/use-issues.ts)
```typescript
useIssues(projectId: number, stateFilter?: string)        // queryKey: ["issues", projectId, stateFilter]
useIssue(projectId: number, issueNumber: number)           // queryKey: ["issue", projectId, issueNumber]
useIssueSummary(projectId: number)                         // queryKey: ["issue-summary", projectId]
useIssueAuditLog(projectId: number, issueNumber: number)  // queryKey: ["issue-audit", projectId, issueNumber]
useDeleteIssue()                                           // invalidates ["issues"]
```

### Pages

#### ProjectDetailPage (`/projects/:id`)
- Header: project name, projectId badge, status badge, description
- Summary cards: Active Issues count, Inactive Issues count, Terminal Issues count
- Issue table below with state filter tabs (All | Active | Inactive | Terminal)
- Table columns: Issue ID (badge), Name, Type, Severity, Priority, State (colored badge), Created
- Row click → navigate to `/projects/:id/issues/:issueNumber`
- Delete button per row with AlertDialog confirmation

#### IssueDetailPage (`/projects/:id/issues/:issueNumber`)
- Back button → `/projects/:id`
- Header: issue name, issueId badge, state badge
- Card sections:
  - **Definition**: type, severity, priority
  - **Reproduction**: steps, expected, actual, affected component, stack trace (collapsible)
  - **Resolution**: root cause, resolution
  - **Attachments**: file list with paths
  - **Timeline**: startedAt, completedAt, resolvedAt, createdAt, updatedAt
  - **Audit Log**: chronological table of all field changes

### Sidebar Changes
- Currently: clicking a project in the switcher navigates to `/` (dashboard)
- Change: clicking a project in the switcher navigates to `/projects/:id`
- Add "Issues" nav item under main nav (visible when a project is selected, links to `/projects/:id`)

---

## 10. Implementation Sequence

### Phase A: Data Layer
1. Add enums to Shared
2. Add FileReferenceDto to Shared
3. Add request/response DTOs to Shared
4. Add ApiRoutes.Issues
5. Add Issue + FileReference + IssueAuditLog entities
6. Add EF configurations
7. Update AppDbContext (DbSets + SaveChangesAsync)
8. Create EF migration

### Phase B: API Layer
9. Add IIssueService + IssueService (with audit logic + state timestamps)
10. Add IssueController
11. Register in DI (Program.cs)
12. Test with Swagger

### Phase C: MCP Layer
13. Add API client methods
14. Add MCP response types
15. Add IssueTools (3 tools)
16. Update ProjectTools.GetProjectOverview
17. Update ProjectOverviewResponse

### Phase D: Dashboard
18. Add TypeScript types
19. Add API functions
20. Add hooks
21. Add ProjectDetailPage + IssueDetailPage
22. Update routing (App.tsx)
23. Update sidebar navigation
24. Update ProjectListPage (row click → detail)

### Phase E: Tests
25. Add IssueEndpointTests (integration tests)

---

## 11. ID Parsing Utility

Needed in both MCP (parsing tool parameters) and API (if needed):

```csharp
// In PinkRooster.Shared/Constants/ or a Helpers/ folder
public static class IdParser
{
    // "proj-1" → 1
    public static bool TryParseProjectId(string humanId, out long projectId) { ... }

    // "proj-1-issue-3" → (projectId: 1, issueNumber: 3)
    public static bool TryParseIssueId(string humanId, out long projectId, out int issueNumber) { ... }
}
```

The MCP tools use this to parse human-readable IDs from AI agents into numeric values for API calls.

---

## 12. Edge Cases & Constraints

- **Cascade delete**: Deleting a project cascades to all its issues and their audit logs
- **IssueNumber is immutable**: Once assigned, never changes even if earlier issues are deleted
- **IssueNumber gaps are allowed**: If issue 2 is deleted, the next issue is still 4 (not 2)
- **Concurrent creation**: Serializable transaction on `SELECT MAX(issue_number)` prevents duplicate numbers
- **Audit log on delete**: No audit entry for deletion — the HTTP request is logged in ActivityLog by middleware
- **Empty project**: `get_project_overview` shows 0/0/0 counts and empty terminal list
- **State timestamp idempotency**: Setting the same state again does NOT update timestamps
