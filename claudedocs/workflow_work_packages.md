# Work Packages — Implementation Workflow

> **Design doc**: `claudedocs/design_work_packages.md`
> **Status**: Not started

---

## Phase A: Shared Layer (enums, DTOs, helpers, constants)

No dependencies. All files in `src/PinkRooster.Shared/`.

### Step A1: New Enums

Create 2 enum files in `Enums/`:
- `WorkPackageType.cs` — Feature, BugFix, Refactor, Spike, Chore
- `VerificationMethod.cs` — AutomatedTest, Manual, AgentReview

> `Priority`, `CompletionState`, `CompletionStateConstants` already exist and are reused.

**Checkpoint**: `dotnet build src/PinkRooster.Shared` compiles

### Step A2: Request DTOs

Create in `DTOs/Requests/`:
- `CreateWorkPackageRequest.cs` — Name (required), Description (required), Type, Priority, Plan?, EstimatedComplexity?, EstimationRationale?, State, LinkedIssueId?, Attachments?
- `UpdateWorkPackageRequest.cs` — ALL fields nullable for PATCH semantics
- `CreatePhaseRequest.cs` — Name (required), Description?, SortOrder?, AcceptanceCriteria? (list), Tasks? (list of CreateTaskRequest for batch create)
- `UpdatePhaseRequest.cs` — all nullable + AcceptanceCriteria? (full replacement), Tasks? (list of UpsertTaskInPhaseDto for batch upsert)
- `UpsertTaskInPhaseDto.cs` — TaskNumber? (provide to update, omit to create), Name?, Description?, SortOrder?, ImplementationNotes?, State?, TargetFiles?, Attachments?
- `CreateTaskRequest.cs` — Name (required), Description (required), SortOrder?, ImplementationNotes?, State, TargetFiles?, Attachments?
- `UpdateTaskRequest.cs` — all nullable + PhaseId? (for moving task between phases)
- `AcceptanceCriterionDto.cs` — Name (required), Description (required), VerificationMethod, VerificationResult?, VerifiedAt?
- `ManageDependencyRequest.cs` — DependsOnId (required, long), Reason?

> `FileReferenceDto` already exists and is reused.

**Checkpoint**: `dotnet build src/PinkRooster.Shared` compiles

### Step A3: Response DTOs

Create in `DTOs/Responses/`:
- `WorkPackageResponse.cs` — full WP with nested Phases, Dependencies, human-readable IDs
- `PhaseResponse.cs` — phase with nested Tasks, AcceptanceCriteria
- `TaskResponse.cs` — task with Dependencies, human-readable IDs
- `DependencyResponse.cs` — WorkPackageId, Name, State, Reason
- `TaskDependencyResponse.cs` — TaskId, Name, State, Reason
- `WorkPackageSummaryResponse.cs` — ActiveCount, InactiveCount, TerminalCount

**Checkpoint**: `dotnet build src/PinkRooster.Shared` compiles

### Step A4: IdParser Extensions

Update `Helpers/IdParser.cs`:
- `TryParseWorkPackageId("proj-1-wp-3")` → extracts `(projectId: 1, wpNumber: 3)`
- `TryParsePhaseId("proj-1-wp-3-phase-2")` → extracts `(projectId: 1, wpNumber: 3, phaseNumber: 2)`
- `TryParseTaskId("proj-1-wp-3-task-5")` → extracts `(projectId: 1, wpNumber: 3, taskNumber: 5)`
- Return false on invalid format

### Step A5: ApiRoutes Constant

Update `Constants/ApiRoutes.cs`:
- Add `WorkPackages` nested class with route `api/projects/{projectId:long}/work-packages`

**Checkpoint**: `dotnet build src/PinkRooster.Shared` compiles

---

## Phase B: Data Layer (entities, EF config, DbContext, migration)

Depends on: Phase A complete.

### Step B1: Entities

Create 9 entity files in `src/PinkRooster.Data/Entities/`:

**Core entities**:
- `WorkPackage.cs` — sealed class. long Id, int WorkPackageNumber, long ProjectId + Project nav, long? LinkedIssueId + Issue? nav, Name (required), Description (required), WorkPackageType Type, Priority Priority, Plan?, EstimatedComplexity?, EstimationRationale?, CompletionState State, CompletionState? PreviousActiveState, StartedAt?, CompletedAt?, ResolvedAt?, List\<FileReference\> Attachments = [], List\<WorkPackagePhase\> Phases = [], List\<WorkPackageDependency\> BlockedBy = [], List\<WorkPackageDependency\> Blocking = [], CreatedAt, UpdatedAt
- `WorkPackagePhase.cs` — sealed class. long Id, int PhaseNumber, long WorkPackageId + WorkPackage nav, Name (required), Description?, int SortOrder, CompletionState State, List\<WorkPackageTask\> Tasks = [], List\<AcceptanceCriterion\> AcceptanceCriteria = [], CreatedAt, UpdatedAt
- `WorkPackageTask.cs` — sealed class. long Id, int TaskNumber, long PhaseId + Phase nav, long WorkPackageId + WorkPackage nav (denormalized), Name (required), Description (required), int SortOrder, ImplementationNotes?, CompletionState State, CompletionState? PreviousActiveState, StartedAt?, CompletedAt?, ResolvedAt?, List\<FileReference\> TargetFiles = [], List\<FileReference\> Attachments = [], List\<WorkPackageTaskDependency\> BlockedBy = [], List\<WorkPackageTaskDependency\> Blocking = [], CreatedAt, UpdatedAt
- `AcceptanceCriterion.cs` — sealed class. long Id, long PhaseId + Phase nav, Name (required), Description (required), VerificationMethod VerificationMethod, VerificationResult?, VerifiedAt?

**Dependency entities**:
- `WorkPackageDependency.cs` — sealed class. long Id, long DependentWorkPackageId + DependentWorkPackage nav, long DependsOnWorkPackageId + DependsOnWorkPackage nav, Reason?
- `WorkPackageTaskDependency.cs` — sealed class. long Id, long DependentTaskId + DependentTask nav, long DependsOnTaskId + DependsOnTask nav, Reason?

**Audit log entities**:
- `WorkPackageAuditLog.cs` — sealed class. long Id, long WorkPackageId + WorkPackage nav, FieldName (required), OldValue?, NewValue?, ChangedBy (required), ChangedAt
- `PhaseAuditLog.cs` — same pattern with PhaseId + Phase nav
- `TaskAuditLog.cs` — same pattern with TaskId + Task nav

> `FileReference` owned type already exists and is reused.

### Step B2: EF Configurations

Create 9 configuration files in `src/PinkRooster.Data/Configurations/`:

**`WorkPackageConfiguration.cs`**:
- Table `"work_packages"`, all columns snake_case
- FK to `projects` (cascade delete)
- Optional FK to `issues` (set null on delete)
- Unique composite index: `(project_id, work_package_number)`
- Indexes: `project_id`, `state`, `priority`, `type`
- Enums as `HasConversion<string>()` — no `HasDefaultValue()` for any enum (avoid sentinel warnings)
- `PreviousActiveState`: `HasConversion<string?>()`, `HasMaxLength(20)`
- `Plan`: `HasMaxLength(16000)`
- `OwnsMany(x => x.Attachments, a => a.ToJson("attachments"))`
- Timestamps: `HasDefaultValueSql("now()")`

**`WorkPackagePhaseConfiguration.cs`**:
- Table `"work_package_phases"`
- FK to `work_packages` (cascade delete) via `.WithMany(w => w.Phases)`
- Unique composite index: `(work_package_id, phase_number)`
- Index: `work_package_id`
- Timestamps: `HasDefaultValueSql("now()")`

**`WorkPackageTaskConfiguration.cs`**:
- Table `"work_package_tasks"`
- FK to `work_package_phases` (cascade delete) via `.WithMany(p => p.Tasks)`
- FK to `work_packages` (restrict — cascade handled by phase cascade) — **Important**: use `OnDelete(DeleteBehavior.Restrict)` to avoid multiple cascade paths. The WP→Phase→Task cascade handles deletion.
- Unique composite index: `(work_package_id, task_number)`
- Indexes: `phase_id`, `work_package_id`, `state`
- `OwnsMany(x => x.TargetFiles, a => a.ToJson("target_files"))`
- `OwnsMany(x => x.Attachments, a => a.ToJson("attachments"))`
- Timestamps: `HasDefaultValueSql("now()")`

**`AcceptanceCriterionConfiguration.cs`**:
- Table `"acceptance_criteria"`
- FK to `work_package_phases` (cascade delete) via `.WithMany(p => p.AcceptanceCriteria)`
- Index: `phase_id`
- `VerificationMethod`: `HasConversion<string>()`, `HasMaxLength(20)`

**`WorkPackageDependencyConfiguration.cs`**:
- Table `"work_package_dependencies"`
- Two FKs to `work_packages`:
  - `DependentWorkPackageId` → `.WithMany(w => w.BlockedBy)`, `OnDelete(DeleteBehavior.Cascade)`
  - `DependsOnWorkPackageId` → `.WithMany(w => w.Blocking)`, `OnDelete(DeleteBehavior.Cascade)`
- If EF/PostgreSQL rejects dual cascade due to cycle, use `ClientCascade` on one side
- Unique composite index: `(dependent_work_package_id, depends_on_work_package_id)`

**`WorkPackageTaskDependencyConfiguration.cs`**:
- Table `"work_package_task_dependencies"`
- Same dual-FK pattern as WP dependencies, pointing to `work_package_tasks`
  - `DependentTaskId` → `.WithMany(t => t.BlockedBy)`, cascade
  - `DependsOnTaskId` → `.WithMany(t => t.Blocking)`, cascade
- Unique composite index: `(dependent_task_id, depends_on_task_id)`

**`WorkPackageAuditLogConfiguration.cs`**:
- Table `"work_package_audit_logs"`
- FK to `work_packages` (cascade delete)
- Index: `work_package_id`
- `changed_at` with `HasDefaultValueSql("now()")`

**`PhaseAuditLogConfiguration.cs`**:
- Table `"phase_audit_logs"`
- FK to `work_package_phases` (cascade delete)
- Index: `phase_id`

**`TaskAuditLogConfiguration.cs`**:
- Table `"task_audit_logs"`
- FK to `work_package_tasks` (cascade delete)
- Index: `task_id`

### Step B3: AppDbContext Updates

Update `src/PinkRooster.Data/AppDbContext.cs`:
- Add 9 new DbSet properties
- Extend `SaveChangesAsync` to track `WorkPackage`, `WorkPackagePhase`, `WorkPackageTask` for UpdatedAt

### Step B4: Migration

Run:
```bash
dotnet ef migrations add AddWorkPackages --project src/PinkRooster.Data --startup-project src/PinkRooster.Api
```

Review generated migration for:
- All 9 tables created with correct columns
- FK constraints (check cascade vs restrict as designed)
- Unique indexes on composite keys
- jsonb columns for Attachments, TargetFiles
- No sentinel value warnings for enums

**Checkpoint**: `dotnet build PinkRooster.slnx` compiles, migration file generated, no EF warnings about sentinels

---

## Phase C: API Layer (services, controllers, DI)

Depends on: Phase B complete.

### Step C1: Shared State Logic Helper

Create `src/PinkRooster.Api/Services/StateTransitionHelper.cs`:

Extract the reusable state logic (currently duplicated in IssueService) into a static helper:
- `ApplyStateTimestamps(entity, oldState, newState)` — works on any entity with StartedAt/CompletedAt/ResolvedAt via an interface or generic approach. Alternatively, keep it as explicit per-entity methods to avoid complexity. **Recommended**: keep as static methods taking the relevant fields, called by each service.
- `ApplyBlockedStateLogic(entity, oldState, newState)` — captures/clears PreviousActiveState

> These helpers handle: StartedAt (set once on first active), CompletedAt (set on Completed, cleared on leaving terminal), ResolvedAt (set on any terminal, cleared on leaving terminal), PreviousActiveState (captured on transition to Blocked from active, cleared on transition from Blocked).

### Step C2: WorkPackage Service

Create `src/PinkRooster.Api/Services/IWorkPackageService.cs` (interface):
- `GetByProjectAsync(long projectId, string? stateFilter, CancellationToken ct)` → `List<WorkPackageResponse>`
- `GetByNumberAsync(long projectId, int wpNumber, CancellationToken ct)` → `WorkPackageResponse?` (full tree with phases, tasks, deps)
- `GetSummaryAsync(long projectId, CancellationToken ct)` → `WorkPackageSummaryResponse`
- `CreateAsync(long projectId, CreateWorkPackageRequest request, string changedBy, CancellationToken ct)` → `WorkPackageResponse`
- `UpdateAsync(long projectId, int wpNumber, UpdateWorkPackageRequest request, string changedBy, CancellationToken ct)` → `WorkPackageResponse?`
- `DeleteAsync(long projectId, int wpNumber, CancellationToken ct)` → `bool`
- `AddDependencyAsync(long projectId, int wpNumber, ManageDependencyRequest request, CancellationToken ct)` → `DependencyResponse`
- `RemoveDependencyAsync(long projectId, int wpNumber, long dependsOnWpId, CancellationToken ct)` → `bool`

Create `src/PinkRooster.Api/Services/WorkPackageService.cs`:

**CreateAsync** critical logic:
1. Verify project exists
2. Serializable transaction
3. `SELECT MAX(work_package_number) + 1` for project (default 1)
4. Create WorkPackage entity
5. Apply state timestamps if initial state is active
6. Build full-field audit entries (using `WorkPackage = wp` nav prop for new entity)
7. SaveChanges + commit
8. Return full response (no phases/deps yet on create)

**UpdateAsync** critical logic:
1. Find WP by (projectId, wpNumber) — include Phases.Tasks for propagation check
2. Per-field comparison with audit tracking (AuditAndSet/AuditAndSetEnum pattern)
3. If State changed:
   - Apply blocked state logic (PreviousActiveState capture/clear)
   - Apply state timestamps
4. AddRange audit entries, SaveChanges
5. Return full tree response

**GetByNumberAsync** — include full tree:
```csharp
.Include(w => w.Phases).ThenInclude(p => p.Tasks).ThenInclude(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
.Include(w => w.Phases).ThenInclude(p => p.Tasks).ThenInclude(t => t.Blocking).ThenInclude(d => d.DependentTask)
.Include(w => w.Phases).ThenInclude(p => p.AcceptanceCriteria)
.Include(w => w.BlockedBy).ThenInclude(d => d.DependsOnWorkPackage)
.Include(w => w.Blocking).ThenInclude(d => d.DependentWorkPackage)
```
Order phases by SortOrder, tasks by SortOrder within each phase.

**AddDependencyAsync** critical logic:
1. Resolve dependent WP from (projectId, wpNumber)
2. Resolve dependsOn WP from DependsOnId (internal long ID)
3. Validate no existing duplicate dependency
4. Validate no circular dependency (BFS from dependsOn through its BlockedBy chain — if we reach dependent, reject)
5. Create WorkPackageDependency
6. SaveChanges
7. Return DependencyResponse

**RemoveDependencyAsync** critical logic:
1. Find and remove the dependency
2. After removal, check if dependent WP is Blocked and has no remaining non-terminal blockers
3. If auto-unblock conditions met: restore PreviousActiveState, apply state timestamps, audit the change
4. SaveChanges

**ToResponse** helper — maps entity tree to WorkPackageResponse with all nested children, human-readable IDs:
- `WorkPackageId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}"`
- `PhaseId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}-phase-{p.PhaseNumber}"`
- `TaskId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}-task-{t.TaskNumber}"`
- `LinkedIssueId` mapped to `$"proj-{w.ProjectId}-issue-{issue.IssueNumber}"` if LinkedIssue is loaded, otherwise use LinkedIssueId with a lookup

### Step C3: Phase Service

Create `src/PinkRooster.Api/Services/IPhaseService.cs` (interface):
- `CreateAsync(long projectId, int wpNumber, CreatePhaseRequest request, string changedBy, CancellationToken ct)` → `PhaseResponse`
- `UpdateAsync(long projectId, int wpNumber, int phaseNumber, UpdatePhaseRequest request, string changedBy, CancellationToken ct)` → `PhaseResponse?`
- `DeleteAsync(long projectId, int wpNumber, int phaseNumber, CancellationToken ct)` → `bool`

Create `src/PinkRooster.Api/Services/PhaseService.cs`:

**CreateAsync** critical logic:
1. Find WP by (projectId, wpNumber)
2. Serializable transaction
3. Assign PhaseNumber: `MAX(phase_number) + 1` within WP (default 1)
4. If SortOrder not provided, auto-assign: `MAX(sort_order) + 1` within WP
5. Create phase entity
6. If request.AcceptanceCriteria provided, create AcceptanceCriterion entities
7. If request.Tasks provided: for each task, assign TaskNumber (`MAX(task_number) + 1` within WP, incrementing), create WorkPackageTask entities
8. Audit all fields on phase creation
9. Audit all fields on each task creation
10. SaveChanges + commit

**UpdateAsync** critical logic:
1. Find phase by (wpId + phaseNumber) — include Tasks
2. Per-field audit for phase fields (Name, Description, SortOrder, State)
3. If AcceptanceCriteria provided: **full replacement** — remove existing, add new
4. If Tasks provided: **upsert** —
   - Tasks with TaskNumber: find existing by (wpId + taskNumber), apply per-field updates with audit
   - Tasks without TaskNumber: create new with next TaskNumber, apply state timestamps, audit all fields
   - Tasks NOT in the array: leave untouched (no deletion)
5. If phase state changed → apply state timestamps (Phase has no PreviousActiveState per design)
6. SaveChanges

**State check after task changes**: After any task state change in this batch, check upward propagation:
- All tasks in phase terminal → auto-complete phase
- All phases in WP terminal → auto-complete WP

### Step C4: Task Service

Create `src/PinkRooster.Api/Services/IWorkPackageTaskService.cs` (interface):
- `CreateAsync(long projectId, int wpNumber, CreateTaskRequest request, int phaseNumber, string changedBy, CancellationToken ct)` → `TaskResponse`
- `UpdateAsync(long projectId, int wpNumber, int taskNumber, UpdateTaskRequest request, string changedBy, CancellationToken ct)` → `TaskResponse?`
- `DeleteAsync(long projectId, int wpNumber, int taskNumber, CancellationToken ct)` → `bool`
- `AddDependencyAsync(long projectId, int wpNumber, int taskNumber, ManageDependencyRequest request, CancellationToken ct)` → `TaskDependencyResponse`
- `RemoveDependencyAsync(long projectId, int wpNumber, int taskNumber, long dependsOnTaskId, CancellationToken ct)` → `bool`

Create `src/PinkRooster.Api/Services/WorkPackageTaskService.cs`:

**CreateAsync** critical logic:
1. Find phase by (wpId + phaseNumber)
2. Serializable transaction
3. Assign TaskNumber: `MAX(task_number) + 1` within the WP (NOT within phase)
4. Create task with WorkPackageId (denormalized from phase's WP)
5. Apply state timestamps if initial state is active
6. Audit all fields
7. SaveChanges + commit

**UpdateAsync** critical logic:
1. Find task by (wpId + taskNumber)
2. Per-field comparison with audit
3. If PhaseId provided: validate target phase exists in same WP, move task
4. If State changed:
   - Apply blocked state logic (PreviousActiveState)
   - Apply state timestamps
   - **Upward propagation**: check if all tasks in (old) phase are now terminal → auto-complete phase. Check if all phases in WP terminal → auto-complete WP. If task moved to new phase, check both old and new phase.
5. SaveChanges

**AddDependencyAsync** — same pattern as WP dependency (cycle detection via BFS on task dependency graph).

**RemoveDependencyAsync** — same pattern (auto-unblock if last dependency removed).

### Step C5: Controllers

Create `src/PinkRooster.Api/Controllers/WorkPackageController.cs`:
- Route: `api/projects/{projectId:long}/work-packages`
- `GET` — list with optional `?state=` filter
- `GET summary` — summary counts
- `GET {wpNumber:int}` — full tree detail
- `POST` — create (201 Created)
- `PATCH {wpNumber:int}` — partial update (200)
- `DELETE {wpNumber:int}` — delete (204/404)
- `POST {wpNumber:int}/dependencies` — add WP dependency (201)
- `DELETE {wpNumber:int}/dependencies/{dependsOnWpId:long}` — remove WP dependency (204/404)

Create `src/PinkRooster.Api/Controllers/PhaseController.cs`:
- Route: `api/projects/{projectId:long}/work-packages/{wpNumber:int}/phases`
- `POST` — create phase with optional batch tasks (201)
- `PATCH {phaseNumber:int}` — update phase with optional batch task upsert (200)
- `DELETE {phaseNumber:int}` — delete phase (204/404)

Create `src/PinkRooster.Api/Controllers/WorkPackageTaskController.cs`:
- Route: `api/projects/{projectId:long}/work-packages/{wpNumber:int}/tasks`
- `POST` — create task (201, requires `phaseNumber` in query or body)
- `PATCH {taskNumber:int}` — update task (200)
- `DELETE {taskNumber:int}` — delete task (204/404)
- `POST {taskNumber:int}/dependencies` — add task dependency (201)
- `DELETE {taskNumber:int}/dependencies/{dependsOnTaskId:long}` — remove task dependency (204/404)

### Step C6: DI Registration

Update `src/PinkRooster.Api/Program.cs`:
```csharp
builder.Services.AddScoped<IWorkPackageService, WorkPackageService>();
builder.Services.AddScoped<IPhaseService, PhaseService>();
builder.Services.AddScoped<IWorkPackageTaskService, WorkPackageTaskService>();
```

**Checkpoint**: `dotnet build PinkRooster.slnx` compiles. Start API (`make dev-api`), verify all new endpoints in Swagger at localhost:5100/swagger. Test basic CRUD via Swagger against running Postgres.

---

## Phase D: MCP Layer (client, responses, tools)

Depends on: Phase C complete.

### Step D1: API Client Methods

Update `src/PinkRooster.Mcp/Clients/PinkRoosterApiClient.cs`:

Work package methods:
- `GetWorkPackagesByProjectAsync(long projectId, string? stateFilter, CancellationToken ct)` → GET, 404 → empty list
- `GetWorkPackageAsync(long projectId, int wpNumber, CancellationToken ct)` → GET, 404 → null
- `GetWorkPackageSummaryAsync(long projectId, CancellationToken ct)` → GET
- `CreateWorkPackageAsync(long projectId, CreateWorkPackageRequest request, CancellationToken ct)` → POST, returns WorkPackageResponse
- `UpdateWorkPackageAsync(long projectId, int wpNumber, UpdateWorkPackageRequest request, CancellationToken ct)` → PATCH, 404 → null
- `AddWorkPackageDependencyAsync(long projectId, int wpNumber, ManageDependencyRequest request, CancellationToken ct)` → POST
- `RemoveWorkPackageDependencyAsync(long projectId, int wpNumber, long dependsOnWpId, CancellationToken ct)` → DELETE

Phase methods:
- `CreatePhaseAsync(long projectId, int wpNumber, CreatePhaseRequest request, CancellationToken ct)` → POST
- `UpdatePhaseAsync(long projectId, int wpNumber, int phaseNumber, UpdatePhaseRequest request, CancellationToken ct)` → PATCH, 404 → null

Task methods:
- `CreateTaskAsync(long projectId, int wpNumber, int phaseNumber, CreateTaskRequest request, CancellationToken ct)` → POST
- `UpdateTaskAsync(long projectId, int wpNumber, int taskNumber, UpdateTaskRequest request, CancellationToken ct)` → PATCH, 404 → null
- `AddTaskDependencyAsync(long projectId, int wpNumber, int taskNumber, ManageDependencyRequest request, CancellationToken ct)` → POST
- `RemoveTaskDependencyAsync(long projectId, int wpNumber, int taskNumber, long dependsOnTaskId, CancellationToken ct)` → DELETE

### Step D2: MCP Response Types

Create in `src/PinkRooster.Mcp/Responses/`:
- `WorkPackageOverviewItem.cs` — WpId, Name, Type, Priority, State, PhaseCount, TaskCount, CompletedTaskCount, CreatedAt, ResolvedAt?
- `WorkPackageDetailResponse.cs` — full tree for AI agents: WP details + List\<PhaseDetailItem\> + List\<DependencyItem\>
- `PhaseDetailItem.cs` — PhaseId, Name, Description?, SortOrder, State, List\<TaskDetailItem\>, List\<AcceptanceCriterionItem\>?
- `TaskDetailItem.cs` — TaskId, Name, Description, SortOrder, ImplementationNotes?, State, PreviousActiveState?, timestamps, TargetFiles?, Attachments?, BlockedBy?, Blocking?
- `AcceptanceCriterionItem.cs` — Name, Description, VerificationMethod, VerificationResult?, VerifiedAt?
- `DependencyItem.cs` — EntityId (human-readable), Name, State, Reason?

### Step D3: Work Package MCP Tools

Create `src/PinkRooster.Mcp/Tools/WorkPackageTools.cs`:

**`get_work_packages`** (ReadOnly = true):
- Params: `projectId` (proj-{N}), `stateFilter?` (active/inactive/terminal)
- Parse projectId → call GetWorkPackagesByProjectAsync → map to List\<WorkPackageOverviewItem\>
- Compute PhaseCount, TaskCount, CompletedTaskCount from the full response
- Return serialized JSON or OperationResult if empty

**`get_work_package_details`** (ReadOnly = true):
- Params: `workPackageId` (proj-{N}-wp-{N})
- Parse → call GetWorkPackageAsync → map to WorkPackageDetailResponse (full tree)
- Return serialized JSON

**`create_or_update_work_package`**:
- Params: `projectId`, `workPackageId?`, `name?`, `description?`, `type?`, `priority?`, `plan?`, `estimatedComplexity?` (string, parse to int), `estimationRationale?`, `state?`, `linkedIssueId?` (proj-{N}-issue-{N} format, parse to long), `attachments?` (JSON string)
- If workPackageId null → validate required fields (name, description), build CreateWorkPackageRequest
- If workPackageId set → build UpdateWorkPackageRequest with non-null fields
- Return OperationResult with created/updated WP ID

**`manage_work_package_dependency`**:
- Params: `workPackageId` (dependent), `dependsOnWorkPackageId`, `action` (add/remove), `reason?`
- Parse both WP IDs
- If action=add → call AddWorkPackageDependencyAsync
- If action=remove → resolve dependsOn WP's internal ID, call RemoveWorkPackageDependencyAsync
- Return OperationResult

**`create_or_update_phase`**:
- Params: `workPackageId`, `phaseId?`, `name?`, `description?`, `sortOrder?` (string→int), `state?`, `acceptanceCriteria?` (JSON string → List\<AcceptanceCriterionDto\>), `tasks?` (JSON string → List\<UpsertTaskInPhaseDto\> or List\<CreateTaskRequest\>)
- If phaseId null → validate required (name), build CreatePhaseRequest with optional tasks
- If phaseId set → build UpdatePhaseRequest with task upserts
- Return OperationResult with created/updated phase ID

**`create_or_update_task`**:
- Params: `phaseId` (proj-{N}-wp-{N}-phase-{N}), `taskId?` (proj-{N}-wp-{N}-task-{N}), `name?`, `description?`, `sortOrder?`, `implementationNotes?`, `state?`, `targetFiles?` (JSON), `attachments?` (JSON)
- If taskId null → validate required (name, description), build CreateTaskRequest, call CreateTaskAsync
- If taskId set → build UpdateTaskRequest, call UpdateTaskAsync
- Return OperationResult with created/updated task ID

**`manage_task_dependency`**:
- Params: `taskId` (dependent), `dependsOnTaskId`, `action`, `reason?`
- Same pattern as WP dependency management

### Step D4: Update ProjectTools + ProjectOverviewResponse

Update `src/PinkRooster.Mcp/Responses/ProjectOverviewResponse.cs`:
- Add `List<WorkPackageOverviewItem> ActiveWorkPackages { get; set; } = []`
- Add `List<WorkPackageOverviewItem> InactiveWorkPackages { get; set; } = []`
- Add `int TerminalWorkPackageCount { get; set; }`

Update `src/PinkRooster.Mcp/Tools/ProjectTools.cs` → `GetProjectOverview`:
- After issue enrichment, fetch WP data:
  - Get active WPs, map to WorkPackageOverviewItem list
  - Get inactive WPs, map to WorkPackageOverviewItem list
  - Get summary for terminal count
- Populate new fields on overview response

**Checkpoint**: `dotnet build PinkRooster.slnx` compiles. Deploy with `docker compose up -d --build api mcp`. Wait for health checks. Test all 7 MCP tools end-to-end.

---

## Phase E: Dashboard

Depends on: Phase C complete (API endpoints available).

### Step E1: TypeScript Types

Update `src/dashboard/src/types/index.ts`:
- Add interfaces: `WorkPackage`, `Phase`, `WpTask`, `AcceptanceCriterionView`, `WorkPackageDep`, `TaskDep`, `WorkPackageSummary`

### Step E2: API Functions

Create `src/dashboard/src/api/work-packages.ts`:
- `getWorkPackages(projectId: number, state?: string)` — GET `/projects/${projectId}/work-packages`
- `getWorkPackage(projectId: number, wpNumber: number)` — GET `/projects/${projectId}/work-packages/${wpNumber}`
- `getWorkPackageSummary(projectId: number)` — GET `/projects/${projectId}/work-packages/summary`
- `deleteWorkPackage(projectId: number, wpNumber: number)` — DELETE `/projects/${projectId}/work-packages/${wpNumber}`

### Step E3: Hooks

Create `src/dashboard/src/hooks/use-work-packages.ts`:
- `useWorkPackages(projectId: number | undefined, stateFilter?: string)` — enabled when projectId defined
- `useWorkPackage(projectId: number, wpNumber: number)`
- `useWorkPackageSummary(projectId: number | undefined)` — enabled when projectId defined
- `useDeleteWorkPackage()` — invalidates `["work-packages"]` and `["work-package-summary"]` on success

### Step E4: Update ProjectDetailPage

Update `src/dashboard/src/pages/project-detail-page.tsx`:
- Add a tab/section switcher: **Issues** | **Work Packages**
- Work Packages section:
  - Summary cards row: Active WPs, Inactive WPs, Terminal WPs (from useWorkPackageSummary)
  - State filter buttons (reuse pattern)
  - WP table: WP ID (badge), Name, Type, Priority, State (colored badge), Progress (completed tasks / total tasks), Created
  - Row click → navigate to `/projects/${projectId}/work-packages/${wp.workPackageNumber}`
  - Delete button per row → AlertDialog confirmation

### Step E5: WorkPackageDetailPage

Create `src/dashboard/src/pages/work-package-detail-page.tsx`:
- Route: `/projects/:id/work-packages/:wpNumber`
- Back button → `/projects/${id}`
- Header: WP name, wpId badge, type badge, priority badge, state badge
- Cards layout:
  - **Definition card**: Description, Plan (rendered as markdown or preformatted text)
  - **Estimation card** (shown if estimatedComplexity or rationale exists): Complexity (1-5), Rationale
  - **Dependencies card** (shown if any deps): "Blocked By" list (WP name, state, reason), "Blocking" list
  - **Linked Issue card** (shown if linkedIssueId exists): link to issue detail page
  - **Timeline card**: StartedAt, CompletedAt, ResolvedAt, CreatedAt, UpdatedAt
  - **Attachments card** (shown if non-empty): table of FileName, RelativePath, Description

- **Phase/Task tree section**:
  - For each phase (ordered by SortOrder):
    - Collapsible card with: Phase name, state badge, phase number badge
    - Expanded content:
      - Description (if present)
      - Acceptance Criteria (if present): checklist-style display with verification status
      - Tasks table: TaskId, Name, State, SortOrder, dependencies indicator
      - Each task row expandable or clickable for: ImplementationNotes, TargetFiles, Attachments, Dependencies
  - Empty state if no phases: "No phases yet"

- Delete button in header → AlertDialog → navigate back to project detail on success

### Step E6: Routing

Update `src/dashboard/src/App.tsx`:
- Add route: `<Route path="projects/:id/work-packages/:wpNumber" element={<WorkPackageDetailPage />} />`

### Step E7: Sidebar Navigation

Update `src/dashboard/src/components/layout/app-sidebar.tsx`:
- Add "Work Packages" nav item after "Issues" (visible only when selectedProject is set)
- Icon: `Layers` or `Package` from lucide-react
- Link: `/projects/${selectedProject.id}` (same as Issues — the tab on project detail handles the distinction)

**Checkpoint**: `make dev-dashboard` starts without errors. Navigate to `/projects/:id`, verify WP section renders. Navigate to WP detail, verify phase/task tree renders. Test delete flow.

---

## Phase F: Testing & Deployment

Depends on: Phase C complete for integration tests. Phase D complete for MCP testing. Phase E complete for dashboard.

### Step F1: Docker Deployment

```bash
docker compose up -d --build api mcp    # Rebuild API + MCP
docker compose ps                        # Verify healthy
```

Wait ~15s for health checks (postgres → api → mcp chain).

### Step F2: MCP Tool Testing Flow

1. `get_project_overview` with projectPath → verify WP summary fields appear (all zeros initially)
2. `create_or_update_work_package` with projectId + name + description → verify returns `proj-{N}-wp-1`
3. `get_work_package_details` with returned wpId → verify empty phases/deps
4. `create_or_update_phase` with wpId + name + tasks array (2 tasks) → verify returns phase ID, tasks have sequential task numbers
5. `get_work_package_details` again → verify full tree with phase + 2 tasks
6. `create_or_update_task` with phaseId + name + description → verify creates task with next task number
7. `create_or_update_work_package` (update) with wpId + state="Implementing" → verify startedAt set
8. `create_or_update_task` (update) with taskId + state="Completed" → verify completedAt/resolvedAt set
9. Create second WP, `manage_work_package_dependency` add → verify dependency created
10. `manage_work_package_dependency` add circular → verify rejection
11. `manage_work_package_dependency` remove → verify dependency removed
12. `manage_task_dependency` add/remove → same pattern
13. `create_or_update_phase` (update) with acceptanceCriteria array → verify replacement
14. `get_work_packages` with stateFilter → verify filtering
15. `get_project_overview` → verify WP summary counts updated

### Step F3: Dashboard Rebuild

```bash
docker compose up -d --build dashboard
```

Verify at localhost:3000:
- Project detail page shows WP section
- WP list renders with correct data
- WP detail page shows phase/task tree
- Delete WP works

### Step F4: Integration Tests (Optional)

Create `tests/PinkRooster.Api.Tests/WorkPackageEndpointTests.cs`:

Test cases:
1. **Create work package** — POST, verify 201, WpNumber = 1
2. **Create second WP** — verify WpNumber = 2
3. **Get WP by number** — full tree response
4. **Get WPs list** — filter by state
5. **Update WP fields** — PATCH partial, verify audit
6. **State → timestamps** — Implementing sets StartedAt, Completed sets CompletedAt+ResolvedAt
7. **Blocked state** — transition to Blocked captures PreviousActiveState
8. **Create phase with tasks** — batch creation, verify task numbers
9. **Update phase with task upsert** — update existing + create new tasks
10. **AcceptanceCriteria replacement** — provide new list, verify old removed
11. **Create standalone task** — verify task number continues WP sequence
12. **Move task between phases** — update task with new PhaseId
13. **Add WP dependency** — verify created
14. **Reject circular WP dependency** — verify 400/error
15. **Remove WP dependency + auto-unblock** — verify state restored
16. **Add/remove task dependency** — same pattern
17. **Upward propagation** — complete all tasks → phase auto-completes → WP auto-completes
18. **Delete WP** — cascades to phases, tasks, deps, audit logs
19. **WP summary** — verify counts per state category
20. **Get WP detail includes full tree** — phases ordered by SortOrder, tasks within phases

---

## Execution Order Summary

```
Phase A (Shared)
    │
    ▼
Phase B (Data + Migration)
    │
    ▼
Phase C (API: Services + Controllers)
    │
    ├──► Phase D (MCP: Client + Tools)
    │
    ├──► Phase E (Dashboard)
    │
    └──► Phase F (Testing + Deployment)

Phases D, E, F can run in parallel after C completes.
```

## File Count Estimate

**New files**: ~45
- Shared: 2 enums, 9 DTOs (requests), 6 DTOs (responses) = 17
- Data: 9 entities, 9 EF configs = 18
- API: 1 helper, 3 interfaces, 3 implementations, 3 controllers = 10
- MCP: 6 response types, 1 tool class = 7
- Dashboard: 1 API file, 1 hooks file, 1 page = 3
- Migration: 1 auto-generated

**Modified files**: ~12
- `IdParser.cs`, `ApiRoutes.cs`
- `AppDbContext.cs`
- `Program.cs` (DI)
- `PinkRoosterApiClient.cs`
- `ProjectOverviewResponse.cs`, `ProjectTools.cs`
- `types/index.ts`, `App.tsx`, `project-detail-page.tsx`, `app-sidebar.tsx`
- 1 migration snapshot

## Complexity Notes

This feature is significantly larger than the Issue entity (~45 new files vs ~20). The most complex areas are:

1. **Service layer logic** (C2-C4): State propagation, blocked state management, cycle detection, batch task upserts in phase updates
2. **EF configuration** (B2): Self-referencing many-to-many dependencies with dual cascade paths — may need testing to verify PostgreSQL accepts the migration
3. **MCP tools** (D3): JSON string parsing for nested arrays (tasks, acceptance criteria) — error handling for malformed JSON
4. **Phase batch operations** (C3): CreateOrUpdatePhase with task array — transaction management, sequential number assignment across batch

Recommend implementing in the exact phase order above, with checkpoints at each phase boundary.
