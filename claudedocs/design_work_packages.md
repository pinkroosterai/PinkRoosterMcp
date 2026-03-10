# Work Packages — Full Vertical Slice Design

## Overview

Work packages represent planned units of work (features, bug fixes, refactors, spikes, chores) that can be decomposed into phases and tasks. They provide a structured hierarchy for AI agents to plan and track implementation work within a project.

**Entity hierarchy**: Project → WorkPackage → Phase → Task
**Cross-entity**: WorkPackageDependency (WP↔WP), WorkPackageTaskDependency (Task↔Task), AcceptanceCriterion (on Phase)

---

## 1. New Enums

### `WorkPackageType` (in `PinkRooster.Shared/Enums/`)
```csharp
public enum WorkPackageType
{
    Feature,          // implements new functionality
    BugFix,           // resolves an issue
    Refactor,         // restructures without behavior change
    Spike,            // research / proof-of-concept
    Chore             // build, CI, docs, config, etc.
}
```

### `VerificationMethod` (in `PinkRooster.Shared/Enums/`)
```csharp
public enum VerificationMethod
{
    AutomatedTest,    // agent can run a test suite to verify
    Manual,           // requires human review
    AgentReview       // agent inspects output / code itself
}
```

> **Note**: `Priority` and `CompletionState` already exist and are reused.

---

## 2. Human-Readable ID Formats

| Entity | Format | Example |
|--------|--------|---------|
| WorkPackage | `proj-{ProjectId}-wp-{WpNumber}` | `proj-1-wp-3` |
| Phase | `proj-{ProjectId}-wp-{WpNumber}-phase-{PhaseNumber}` | `proj-1-wp-3-phase-2` |
| Task | `proj-{ProjectId}-wp-{WpNumber}-task-{TaskNumber}` | `proj-1-wp-3-task-5` |
| AcceptanceCriterion | No human-readable ID (referenced by name or internal ID) | — |

### Sequential Numbering

- **WorkPackageNumber**: Per-project sequential (like IssueNumber). Serializable transaction with `SELECT MAX + 1`.
- **PhaseNumber**: Per-work-package sequential. Immutable after creation. Assigned in serializable transaction.
- **TaskNumber**: Per-work-package sequential (across all phases). Immutable after creation. Assigned in serializable transaction.

> Tasks are numbered per-WP (not per-phase) so that `proj-{N}-wp-{N}-task-{N}` uniquely identifies a task without needing the phase number in the ID.

### IdParser Extensions

Add to `PinkRooster.Shared/Helpers/IdParser.cs`:

```csharp
public static bool TryParseWorkPackageId(string humanId, out long projectId, out int wpNumber)
{
    // Parses "proj-{N}-wp-{N}"
}

public static bool TryParsePhaseId(string humanId, out long projectId, out int wpNumber, out int phaseNumber)
{
    // Parses "proj-{N}-wp-{N}-phase-{N}"
}

public static bool TryParseTaskId(string humanId, out long projectId, out int wpNumber, out int taskNumber)
{
    // Parses "proj-{N}-wp-{N}-task-{N}"
}
```

---

## 3. Data Layer — Entities

All entities follow existing conventions: `sealed class`, `long Id` PK, `DateTimeOffset` timestamps, `required string` for mandatory fields.

### WorkPackage

```csharp
public sealed class WorkPackage
{
    public long Id { get; set; }
    public int WorkPackageNumber { get; set; }     // Per-project sequential, immutable
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // ── Optional Issue Link ──
    public long? LinkedIssueId { get; set; }
    public Issue? LinkedIssue { get; set; }

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public WorkPackageType Type { get; set; } = WorkPackageType.Feature;
    public Priority Priority { get; set; } = Priority.Medium;
    public string? Plan { get; set; }              // Markdown

    // ── Estimation ──
    public int? EstimatedComplexity { get; set; }  // 1–5
    public string? EstimationRationale { get; set; }

    // ── State ──
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public CompletionState? PreviousActiveState { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // ── Attachments (jsonb) ──
    public List<FileReference> Attachments { get; set; } = [];

    // ── Children ──
    public List<WorkPackagePhase> Phases { get; set; } = [];

    // ── Dependencies ──
    public List<WorkPackageDependency> BlockedBy { get; set; } = [];
    public List<WorkPackageDependency> Blocking { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### WorkPackagePhase

```csharp
public sealed class WorkPackagePhase
{
    public long Id { get; set; }
    public int PhaseNumber { get; set; }           // Per-WP sequential, immutable
    public long WorkPackageId { get; set; }
    public WorkPackage WorkPackage { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }             // Mutable execution order

    // ── State ──
    public CompletionState State { get; set; } = CompletionState.NotStarted;

    // ── Children ──
    public List<WorkPackageTask> Tasks { get; set; } = [];
    public List<AcceptanceCriterion> AcceptanceCriteria { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### WorkPackageTask

```csharp
public sealed class WorkPackageTask
{
    public long Id { get; set; }
    public int TaskNumber { get; set; }            // Per-WP sequential (across phases), immutable
    public long PhaseId { get; set; }
    public WorkPackagePhase Phase { get; set; } = null!;
    public long WorkPackageId { get; set; }        // Denormalized for ID format + querying
    public WorkPackage WorkPackage { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int SortOrder { get; set; }
    public string? ImplementationNotes { get; set; }

    // ── State ──
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public CompletionState? PreviousActiveState { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // ── Files (jsonb) ──
    public List<FileReference> TargetFiles { get; set; } = [];
    public List<FileReference> Attachments { get; set; } = [];

    // ── Dependencies ──
    public List<WorkPackageTaskDependency> BlockedBy { get; set; } = [];
    public List<WorkPackageTaskDependency> Blocking { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### AcceptanceCriterion

```csharp
public sealed class AcceptanceCriterion
{
    public long Id { get; set; }
    public long PhaseId { get; set; }
    public WorkPackagePhase Phase { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public VerificationMethod VerificationMethod { get; set; } = VerificationMethod.Manual;

    // ── Verification ──
    public string? VerificationResult { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
```

### WorkPackageDependency

```csharp
public sealed class WorkPackageDependency
{
    public long Id { get; set; }
    public long DependentWorkPackageId { get; set; }     // The WP that is blocked
    public WorkPackage DependentWorkPackage { get; set; } = null!;
    public long DependsOnWorkPackageId { get; set; }     // The WP that must finish
    public WorkPackage DependsOnWorkPackage { get; set; } = null!;
    public string? Reason { get; set; }
}
```

### WorkPackageTaskDependency

```csharp
public sealed class WorkPackageTaskDependency
{
    public long Id { get; set; }
    public long DependentTaskId { get; set; }
    public WorkPackageTask DependentTask { get; set; } = null!;
    public long DependsOnTaskId { get; set; }
    public WorkPackageTask DependsOnTask { get; set; } = null!;
    public string? Reason { get; set; }
}
```

### Audit Log Entities

Three separate tables following the IssueAuditLog pattern:

```csharp
public sealed class WorkPackageAuditLog
{
    public long Id { get; set; }
    public long WorkPackageId { get; set; }
    public WorkPackage WorkPackage { get; set; } = null!;
    public required string FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public required string ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}

public sealed class PhaseAuditLog
{
    public long Id { get; set; }
    public long PhaseId { get; set; }
    public WorkPackagePhase Phase { get; set; } = null!;
    public required string FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public required string ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}

public sealed class TaskAuditLog
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public WorkPackageTask Task { get; set; } = null!;
    public required string FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public required string ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
```

---

## 4. Data Layer — EF Configurations

All follow existing pattern: snake_case table/column names, enums as `HasConversion<string>()`, no `HasDefaultValue()` for enums.

### Tables

| Entity | Table Name |
|--------|-----------|
| WorkPackage | `work_packages` |
| WorkPackagePhase | `work_package_phases` |
| WorkPackageTask | `work_package_tasks` |
| AcceptanceCriterion | `acceptance_criteria` |
| WorkPackageDependency | `work_package_dependencies` |
| WorkPackageTaskDependency | `work_package_task_dependencies` |
| WorkPackageAuditLog | `work_package_audit_logs` |
| PhaseAuditLog | `phase_audit_logs` |
| TaskAuditLog | `task_audit_logs` |

### Key Configuration Details

**WorkPackage**:
- FK to `projects` (cascade delete)
- Optional FK to `issues` (set null on delete)
- Unique composite index: `(project_id, work_package_number)`
- Indexes: `project_id`, `state`, `priority`, `type`
- `OwnsMany(x => x.Attachments, a => a.ToJson("attachments"))`
- `Plan`: `HasMaxLength(16000)` (markdown content)
- `EstimatedComplexity`: no max length constraint (int)
- `PreviousActiveState`: `HasConversion<string?>()`, `HasMaxLength(20)`

**WorkPackagePhase**:
- FK to `work_packages` (cascade delete)
- Unique composite index: `(work_package_id, phase_number)`
- Index: `work_package_id`

**WorkPackageTask**:
- FK to `work_package_phases` (cascade delete)
- FK to `work_packages` (no cascade — handled by phase cascade)
- Unique composite index: `(work_package_id, task_number)`
- Index: `phase_id`, `work_package_id`, `state`
- `OwnsMany(x => x.TargetFiles, a => a.ToJson("target_files"))`
- `OwnsMany(x => x.Attachments, a => a.ToJson("attachments"))`

**AcceptanceCriterion**:
- FK to `work_package_phases` (cascade delete)
- Index: `phase_id`
- `VerificationMethod`: `HasConversion<string>()`, `HasMaxLength(20)`

**WorkPackageDependency**:
- Two FKs to `work_packages`: `DependentWorkPackageId` (restrict delete), `DependsOnWorkPackageId` (cascade delete)
- Unique composite index: `(dependent_work_package_id, depends_on_work_package_id)`
- Delete behavior: Cascade on `DependsOn` side (if the depended-on WP is deleted, the dependency goes away). Restrict on `Dependent` side (you can't delete a WP that has blockers without removing them first).

> Actually, to keep deletion simple (dashboard deletes WP and everything cascades), use Cascade on both sides. EF Core may require breaking the cycle — use `OnDelete(DeleteBehavior.Cascade)` on one side and `OnDelete(DeleteBehavior.ClientCascade)` or `Restrict` + manual cleanup on the other.

**Revised delete strategy for dependencies**: Use `OnDelete(DeleteBehavior.Cascade)` on both FK sides. PostgreSQL handles multi-path cascades fine. If EF complains, use `OnDelete(DeleteBehavior.Cascade)` for `DependsOn` FK and `OnDelete(DeleteBehavior.Cascade)` for `Dependent` FK — test with the migration to ensure PostgreSQL accepts it. If not, use `Restrict` on one side and handle cleanup in the service Delete method.

**WorkPackageTaskDependency**:
- Same pattern as WP dependencies but for tasks.

### AppDbContext Changes

```csharp
// Add DbSet declarations
public DbSet<WorkPackage> WorkPackages => Set<WorkPackage>();
public DbSet<WorkPackagePhase> WorkPackagePhases => Set<WorkPackagePhase>();
public DbSet<WorkPackageTask> WorkPackageTasks => Set<WorkPackageTask>();
public DbSet<AcceptanceCriterion> AcceptanceCriteria => Set<AcceptanceCriterion>();
public DbSet<WorkPackageDependency> WorkPackageDependencies => Set<WorkPackageDependency>();
public DbSet<WorkPackageTaskDependency> WorkPackageTaskDependencies => Set<WorkPackageTaskDependency>();
public DbSet<WorkPackageAuditLog> WorkPackageAuditLogs => Set<WorkPackageAuditLog>();
public DbSet<PhaseAuditLog> PhaseAuditLogs => Set<PhaseAuditLog>();
public DbSet<TaskAuditLog> TaskAuditLogs => Set<TaskAuditLog>();

// Add to SaveChangesAsync override
foreach (var entry in ChangeTracker.Entries<WorkPackage>()
    .Where(e => e.State == EntityState.Modified))
    entry.Entity.UpdatedAt = now;

foreach (var entry in ChangeTracker.Entries<WorkPackagePhase>()
    .Where(e => e.State == EntityState.Modified))
    entry.Entity.UpdatedAt = now;

foreach (var entry in ChangeTracker.Entries<WorkPackageTask>()
    .Where(e => e.State == EntityState.Modified))
    entry.Entity.UpdatedAt = now;
```

---

## 5. Shared Layer — DTOs

### Requests

**`CreateWorkPackageRequest`**:
```csharp
public sealed class CreateWorkPackageRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public WorkPackageType Type { get; set; } = WorkPackageType.Feature;
    public Priority Priority { get; set; } = Priority.Medium;
    public string? Plan { get; set; }
    public int? EstimatedComplexity { get; set; }
    public string? EstimationRationale { get; set; }
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public long? LinkedIssueId { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
```

**`UpdateWorkPackageRequest`** (all nullable for PATCH):
```csharp
public sealed class UpdateWorkPackageRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public WorkPackageType? Type { get; set; }
    public Priority? Priority { get; set; }
    public string? Plan { get; set; }
    public int? EstimatedComplexity { get; set; }
    public string? EstimationRationale { get; set; }
    public CompletionState? State { get; set; }
    public long? LinkedIssueId { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
```

**`CreatePhaseRequest`**:
```csharp
public sealed class CreatePhaseRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }            // Auto-assigned if omitted
    public List<AcceptanceCriterionDto>? AcceptanceCriteria { get; set; }
    public List<CreateTaskRequest>? Tasks { get; set; }  // Batch create
}
```

**`UpdatePhaseRequest`**:
```csharp
public sealed class UpdatePhaseRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
    public CompletionState? State { get; set; }
    public List<AcceptanceCriterionDto>? AcceptanceCriteria { get; set; }  // Full replacement
    public List<UpsertTaskInPhaseDto>? Tasks { get; set; }  // Upsert batch
}
```

**`UpsertTaskInPhaseDto`** (for batch task operations in CreateOrUpdatePhase):
```csharp
public sealed class UpsertTaskInPhaseDto
{
    public int? TaskNumber { get; set; }           // Provide to update existing, omit to create
    public string? Name { get; set; }              // Required for create
    public string? Description { get; set; }       // Required for create
    public int? SortOrder { get; set; }
    public string? ImplementationNotes { get; set; }
    public CompletionState? State { get; set; }
    public List<FileReferenceDto>? TargetFiles { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
```

**`CreateTaskRequest`**:
```csharp
public sealed class CreateTaskRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int? SortOrder { get; set; }
    public string? ImplementationNotes { get; set; }
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public List<FileReferenceDto>? TargetFiles { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
```

**`UpdateTaskRequest`**:
```csharp
public sealed class UpdateTaskRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
    public string? ImplementationNotes { get; set; }
    public CompletionState? State { get; set; }
    public long? PhaseId { get; set; }             // Allow moving task between phases
    public List<FileReferenceDto>? TargetFiles { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
```

**`AcceptanceCriterionDto`**:
```csharp
public sealed class AcceptanceCriterionDto
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public VerificationMethod VerificationMethod { get; set; } = VerificationMethod.Manual;
    public string? VerificationResult { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
```

**`ManageDependencyRequest`**:
```csharp
public sealed class ManageDependencyRequest
{
    public required long DependsOnId { get; set; }   // Internal long ID of the entity depended on
    public string? Reason { get; set; }
}
```

### Responses

**`WorkPackageResponse`** (full, for API → Dashboard):
```csharp
public sealed class WorkPackageResponse
{
    public required string WorkPackageId { get; init; }  // proj-{N}-wp-{N}
    public long Id { get; init; }
    public int WorkPackageNumber { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
    public string? Plan { get; init; }
    public int? EstimatedComplexity { get; init; }
    public string? EstimationRationale { get; init; }
    public required string State { get; init; }
    public string? PreviousActiveState { get; init; }
    public string? LinkedIssueId { get; init; }      // proj-{N}-issue-{N} or null
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<FileReferenceDto> Attachments { get; init; } = [];
    public List<PhaseResponse> Phases { get; init; } = [];
    public List<DependencyResponse> BlockedBy { get; init; } = [];
    public List<DependencyResponse> Blocking { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
```

**`PhaseResponse`**:
```csharp
public sealed class PhaseResponse
{
    public required string PhaseId { get; init; }    // proj-{N}-wp-{N}-phase-{N}
    public long Id { get; init; }
    public int PhaseNumber { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public required string State { get; init; }
    public List<TaskResponse> Tasks { get; init; } = [];
    public List<AcceptanceCriterionDto> AcceptanceCriteria { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
```

**`TaskResponse`**:
```csharp
public sealed class TaskResponse
{
    public required string TaskId { get; init; }     // proj-{N}-wp-{N}-task-{N}
    public long Id { get; init; }
    public int TaskNumber { get; init; }
    public required string PhaseId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public int SortOrder { get; init; }
    public string? ImplementationNotes { get; init; }
    public required string State { get; init; }
    public string? PreviousActiveState { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<FileReferenceDto> TargetFiles { get; init; } = [];
    public List<FileReferenceDto> Attachments { get; init; } = [];
    public List<TaskDependencyResponse> BlockedBy { get; init; } = [];
    public List<TaskDependencyResponse> Blocking { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
```

**`DependencyResponse`** (for WP dependencies):
```csharp
public sealed class DependencyResponse
{
    public required string WorkPackageId { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public string? Reason { get; init; }
}
```

**`TaskDependencyResponse`**:
```csharp
public sealed class TaskDependencyResponse
{
    public required string TaskId { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public string? Reason { get; init; }
}
```

**`WorkPackageSummaryResponse`** (for project overview):
```csharp
public sealed class WorkPackageSummaryResponse
{
    public int ActiveCount { get; init; }
    public int InactiveCount { get; init; }
    public int TerminalCount { get; init; }
}
```

---

## 6. API Layer

### Routes

```
api/projects/{projectId:long}/work-packages                                    GET, POST
api/projects/{projectId:long}/work-packages/summary                             GET
api/projects/{projectId:long}/work-packages/{wpNumber:int}                      GET, PATCH, DELETE
api/projects/{projectId:long}/work-packages/{wpNumber:int}/phases               POST
api/projects/{projectId:long}/work-packages/{wpNumber:int}/phases/{phaseNum:int}  PATCH, DELETE
api/projects/{projectId:long}/work-packages/{wpNumber:int}/tasks                POST
api/projects/{projectId:long}/work-packages/{wpNumber:int}/tasks/{taskNum:int}   PATCH, DELETE
api/projects/{projectId:long}/work-packages/{wpNumber:int}/dependencies         POST, DELETE
api/projects/{projectId:long}/work-packages/{wpNumber:int}/tasks/{taskNum:int}/dependencies  POST, DELETE
```

### ApiRoutes Constants

```csharp
public static class WorkPackages
{
    public const string Route = $"{Base}/projects/{{projectId:long}}/work-packages";
}
```

### Controllers

**`WorkPackageController`** — handles WP CRUD, summary, and WP dependency management.

**`PhaseController`** — handles phase CRUD (nested under WP route).

**`TaskController`** — handles task CRUD and task dependency management (nested under WP route).

### Service Interfaces

**`IWorkPackageService`**:
```csharp
public interface IWorkPackageService
{
    Task<List<WorkPackageResponse>> GetByProjectAsync(long projectId, string? stateFilter, CancellationToken ct);
    Task<WorkPackageResponse?> GetByNumberAsync(long projectId, int wpNumber, CancellationToken ct);
    Task<WorkPackageSummaryResponse> GetSummaryAsync(long projectId, CancellationToken ct);
    Task<WorkPackageResponse> CreateAsync(long projectId, CreateWorkPackageRequest request, string changedBy, CancellationToken ct);
    Task<WorkPackageResponse?> UpdateAsync(long projectId, int wpNumber, UpdateWorkPackageRequest request, string changedBy, CancellationToken ct);
    Task<bool> DeleteAsync(long projectId, int wpNumber, CancellationToken ct);
    Task<DependencyResponse> AddDependencyAsync(long projectId, int wpNumber, ManageDependencyRequest request, CancellationToken ct);
    Task<bool> RemoveDependencyAsync(long projectId, int wpNumber, long dependsOnId, CancellationToken ct);
}
```

**`IPhaseService`**:
```csharp
public interface IPhaseService
{
    Task<PhaseResponse> CreateAsync(long projectId, int wpNumber, CreatePhaseRequest request, string changedBy, CancellationToken ct);
    Task<PhaseResponse?> UpdateAsync(long projectId, int wpNumber, int phaseNumber, UpdatePhaseRequest request, string changedBy, CancellationToken ct);
    Task<bool> DeleteAsync(long projectId, int wpNumber, int phaseNumber, CancellationToken ct);
}
```

**`ITaskService`** (name: `IWorkPackageTaskService` to avoid conflict with `System.Threading.Tasks.Task`):
```csharp
public interface IWorkPackageTaskService
{
    Task<TaskResponse> CreateAsync(long projectId, int wpNumber, int phaseNumber, CreateTaskRequest request, string changedBy, CancellationToken ct);
    Task<TaskResponse?> UpdateAsync(long projectId, int wpNumber, int taskNumber, UpdateTaskRequest request, string changedBy, CancellationToken ct);
    Task<bool> DeleteAsync(long projectId, int wpNumber, int taskNumber, CancellationToken ct);
    Task<TaskDependencyResponse> AddDependencyAsync(long projectId, int wpNumber, int taskNumber, ManageDependencyRequest request, CancellationToken ct);
    Task<bool> RemoveDependencyAsync(long projectId, int wpNumber, int taskNumber, long dependsOnId, CancellationToken ct);
}
```

---

## 7. Service Layer — Key Logic

### State-Driven Timestamps

Same pattern as Issue, applied to WorkPackage and WorkPackageTask:

```csharp
private static void ApplyStateTimestamps(/* entity with State/StartedAt/CompletedAt/ResolvedAt */, CompletionState oldState, CompletionState newState)
{
    if (oldState == newState) return;
    var now = DateTimeOffset.UtcNow;

    // StartedAt: set once when entering active from inactive
    if (entity.StartedAt is null && CompletionStateConstants.ActiveStates.Contains(newState))
        entity.StartedAt = now;

    // CompletedAt: set when → Completed, cleared when leaving terminal
    if (newState == CompletionState.Completed)
        entity.CompletedAt = now;
    else if (CompletionStateConstants.TerminalStates.Contains(oldState) && !CompletionStateConstants.TerminalStates.Contains(newState))
        entity.CompletedAt = null;

    // ResolvedAt: set when → any terminal, cleared when leaving terminal
    if (CompletionStateConstants.TerminalStates.Contains(newState))
        entity.ResolvedAt = now;
    else if (CompletionStateConstants.TerminalStates.Contains(oldState))
        entity.ResolvedAt = null;
}
```

> Consider extracting this into a shared static helper in the service layer since it's identical for Issue, WorkPackage, and Task.

### PreviousActiveState (Blocked State Management)

Applied to WorkPackage and WorkPackageTask:

```csharp
private static void ApplyBlockedStateLogic(/* entity */, CompletionState oldState, CompletionState newState)
{
    // Transitioning TO Blocked: capture current state if it's active
    if (newState == CompletionState.Blocked && oldState != CompletionState.Blocked)
    {
        if (CompletionStateConstants.ActiveStates.Contains(oldState))
            entity.PreviousActiveState = oldState;
    }

    // Transitioning FROM Blocked: clear PreviousActiveState
    if (oldState == CompletionState.Blocked && newState != CompletionState.Blocked)
    {
        entity.PreviousActiveState = null;
    }
}
```

When the last blocking dependency on an entity is removed (via `RemoveDependencyAsync`) and the entity is currently Blocked:
```csharp
// Check remaining dependencies
var hasRemainingBlockers = await db.WorkPackageDependencies
    .AnyAsync(d => d.DependentWorkPackageId == wp.Id
        && !CompletionStateConstants.TerminalStates.Contains(d.DependsOnWorkPackage.State), ct);

if (!hasRemainingBlockers && wp.State == CompletionState.Blocked && wp.PreviousActiveState is not null)
{
    var restoredState = wp.PreviousActiveState.Value;
    wp.PreviousActiveState = null;
    wp.State = restoredState;
    ApplyStateTimestamps(wp, CompletionState.Blocked, restoredState);
    // Audit the state change
}
```

### Upward State Propagation

After any task state change:
```csharp
private async Task PropagateCompletionUpward(WorkPackageTask task, CancellationToken ct)
{
    // Check if all tasks in the phase are terminal
    var phase = await db.WorkPackagePhases
        .Include(p => p.Tasks)
        .FirstAsync(p => p.Id == task.PhaseId, ct);

    if (phase.Tasks.All(t => CompletionStateConstants.TerminalStates.Contains(t.State)))
    {
        if (!CompletionStateConstants.TerminalStates.Contains(phase.State))
        {
            phase.State = CompletionState.Completed;
            // Audit phase state change
        }

        // Check if all phases in the WP are terminal
        var wp = await db.WorkPackages
            .Include(w => w.Phases)
            .FirstAsync(w => w.Id == phase.WorkPackageId, ct);

        if (wp.Phases.All(p => CompletionStateConstants.TerminalStates.Contains(p.State)))
        {
            if (!CompletionStateConstants.TerminalStates.Contains(wp.State))
            {
                var oldState = wp.State;
                wp.State = CompletionState.Completed;
                ApplyStateTimestamps(wp, oldState, CompletionState.Completed);
                // Audit WP state change
            }
        }
    }
}
```

### Circular Dependency Detection

For both WP and task dependencies:

```csharp
private async Task<bool> WouldCreateCycleAsync(long dependentId, long dependsOnId, CancellationToken ct)
{
    // BFS from dependsOnId through its BlockedBy chain
    // If we ever reach dependentId, it's a cycle
    var visited = new HashSet<long>();
    var queue = new Queue<long>();
    queue.Enqueue(dependsOnId);

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (current == dependentId) return true;
        if (!visited.Add(current)) continue;

        var blockers = await db.WorkPackageDependencies
            .Where(d => d.DependentWorkPackageId == current)
            .Select(d => d.DependsOnWorkPackageId)
            .ToListAsync(ct);

        foreach (var blocker in blockers)
            queue.Enqueue(blocker);
    }

    return false;
}
```

### AcceptanceCriteria Management

When `AcceptanceCriteria` is provided in a phase update request, it's a **full replacement**:

```csharp
if (request.AcceptanceCriteria is not null)
{
    // Remove existing
    var existing = await db.AcceptanceCriteria
        .Where(ac => ac.PhaseId == phase.Id)
        .ToListAsync(ct);
    db.AcceptanceCriteria.RemoveRange(existing);

    // Add new
    var newCriteria = request.AcceptanceCriteria.Select(dto => new AcceptanceCriterion
    {
        PhaseId = phase.Id,
        Name = dto.Name,
        Description = dto.Description,
        VerificationMethod = dto.VerificationMethod,
        VerificationResult = dto.VerificationResult,
        VerifiedAt = dto.VerifiedAt
    }).ToList();
    db.AcceptanceCriteria.AddRange(newCriteria);
}
```

### Audit Logging

Same pattern as Issue: per-field comparison with `AuditAndSet`/`AuditAndSetEnum` helpers. On creation, all non-null fields logged with OldValue = null. Use navigation property (`WorkPackage = wp`) not FK ID for new entities.

---

## 8. MCP Layer

### MCP Response Types (in `PinkRooster.Mcp/Responses/`)

**`WorkPackageOverviewItem`** (for list view):
```csharp
public sealed class WorkPackageOverviewItem
{
    public required string WorkPackageId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
    public required string State { get; init; }
    public int PhaseCount { get; init; }
    public int TaskCount { get; init; }
    public int CompletedTaskCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
}
```

**`WorkPackageDetailResponse`** (full tree for AI agents):
```csharp
public sealed class WorkPackageDetailResponse
{
    public required string WorkPackageId { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
    public string? Plan { get; init; }
    public int? EstimatedComplexity { get; init; }
    public string? EstimationRationale { get; init; }
    public required string State { get; init; }
    public string? PreviousActiveState { get; init; }
    public string? LinkedIssueId { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<FileReferenceDto>? Attachments { get; init; }
    public required List<PhaseDetailItem> Phases { get; init; }
    public List<DependencyItem>? BlockedBy { get; init; }
    public List<DependencyItem>? Blocking { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
```

**`PhaseDetailItem`**:
```csharp
public sealed class PhaseDetailItem
{
    public required string PhaseId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public required string State { get; init; }
    public List<AcceptanceCriterionItem>? AcceptanceCriteria { get; init; }
    public required List<TaskDetailItem> Tasks { get; init; }
}
```

**`TaskDetailItem`**:
```csharp
public sealed class TaskDetailItem
{
    public required string TaskId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public int SortOrder { get; init; }
    public string? ImplementationNotes { get; init; }
    public required string State { get; init; }
    public string? PreviousActiveState { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<FileReferenceDto>? TargetFiles { get; init; }
    public List<FileReferenceDto>? Attachments { get; init; }
    public List<DependencyItem>? BlockedBy { get; init; }
    public List<DependencyItem>? Blocking { get; init; }
}
```

**`AcceptanceCriterionItem`**:
```csharp
public sealed class AcceptanceCriterionItem
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string VerificationMethod { get; init; }
    public string? VerificationResult { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
}
```

**`DependencyItem`**:
```csharp
public sealed class DependencyItem
{
    public required string EntityId { get; init; }  // Human-readable ID (WP or Task)
    public required string Name { get; init; }
    public required string State { get; init; }
    public string? Reason { get; init; }
}
```

### MCP Tools (in `PinkRooster.Mcp/Tools/WorkPackageTools.cs`)

**7 tools total** — all in a single `WorkPackageTools` class (or split into `WorkPackageTools` + `PhaseTaskTools` if too large):

#### 1. `get_work_packages` (ReadOnly)
```
Parameters:
  - projectId: string ("proj-{N}")
  - stateFilter?: string ("active"/"inactive"/"terminal")

Returns: List<WorkPackageOverviewItem> or OperationResult message
```

#### 2. `get_work_package_details` (ReadOnly)
```
Parameters:
  - workPackageId: string ("proj-{N}-wp-{N}")

Returns: WorkPackageDetailResponse (full tree: WP → phases → tasks → acceptance criteria → dependencies)
```

#### 3. `create_or_update_work_package`
```
Parameters:
  - projectId: string
  - workPackageId?: string          (omit to create, provide to update)
  - name?: string                   (required for create)
  - description?: string            (required for create)
  - type?: string                   (WorkPackageType enum)
  - priority?: string               (Priority enum)
  - plan?: string                   (markdown)
  - estimatedComplexity?: int       (1-5)
  - estimationRationale?: string
  - state?: string                  (CompletionState enum)
  - linkedIssueId?: string          ("proj-{N}-issue-{N}" format)
  - attachments?: string            (JSON array)

Returns: OperationResult with created/updated WP ID
```

#### 4. `manage_work_package_dependency`
```
Parameters:
  - workPackageId: string           (the dependent WP, "proj-{N}-wp-{N}")
  - dependsOnWorkPackageId: string  (the WP that must finish, "proj-{N}-wp-{N}")
  - action: string                  ("add" or "remove")
  - reason?: string                 (only for "add")

Returns: OperationResult
```

#### 5. `create_or_update_phase`
```
Parameters:
  - workPackageId: string           ("proj-{N}-wp-{N}")
  - phaseId?: string                ("proj-{N}-wp-{N}-phase-{N}", omit to create)
  - name?: string                   (required for create)
  - description?: string
  - sortOrder?: int
  - state?: string
  - acceptanceCriteria?: string     (JSON array of AcceptanceCriterionDto)
  - tasks?: string                  (JSON array of UpsertTaskInPhaseDto)

Returns: OperationResult with created/updated phase ID
```

#### 6. `create_or_update_task`
```
Parameters:
  - phaseId: string                 ("proj-{N}-wp-{N}-phase-{N}", required for create)
  - taskId?: string                 ("proj-{N}-wp-{N}-task-{N}", omit to create)
  - name?: string                   (required for create)
  - description?: string            (required for create)
  - sortOrder?: int
  - implementationNotes?: string
  - state?: string
  - targetFiles?: string            (JSON array of FileReferenceDto)
  - attachments?: string            (JSON array of FileReferenceDto)

Returns: OperationResult with created/updated task ID
```

#### 7. `manage_task_dependency`
```
Parameters:
  - taskId: string                  (dependent task, "proj-{N}-wp-{N}-task-{N}")
  - dependsOnTaskId: string         (task that must finish, "proj-{N}-wp-{N}-task-{N}")
  - action: string                  ("add" or "remove")
  - reason?: string

Returns: OperationResult
```

### PinkRoosterApiClient Extensions

Add methods for all work package API endpoints, following the existing pattern (GetFromJsonAsync, PostAsJsonAsync, PatchAsJsonAsync, etc.).

### ProjectOverviewResponse Update

Add work package summary to ProjectOverviewResponse:
```csharp
public List<WorkPackageOverviewItem> ActiveWorkPackages { get; set; } = [];
public List<WorkPackageOverviewItem> InactiveWorkPackages { get; set; } = [];
public int TerminalWorkPackageCount { get; set; }
```

Update `ProjectTools.GetProjectOverview` to fetch and populate WP summaries (same pattern as issue enrichment).

---

## 9. Dashboard

### TypeScript Types (`src/dashboard/src/types/index.ts`)

```typescript
export interface WorkPackage {
  workPackageId: string;
  id: number;
  workPackageNumber: number;
  projectId: string;
  name: string;
  description: string;
  type: "Feature" | "BugFix" | "Refactor" | "Spike" | "Chore";
  priority: "Critical" | "High" | "Medium" | "Low";
  plan: string | null;
  estimatedComplexity: number | null;
  estimationRationale: string | null;
  state: string;
  previousActiveState: string | null;
  linkedIssueId: string | null;
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  attachments: FileReference[];
  phases: Phase[];
  blockedBy: WorkPackageDep[];
  blocking: WorkPackageDep[];
  createdAt: string;
  updatedAt: string;
}

export interface Phase {
  phaseId: string;
  id: number;
  phaseNumber: number;
  name: string;
  description: string | null;
  sortOrder: number;
  state: string;
  tasks: WpTask[];
  acceptanceCriteria: AcceptanceCriterionView[];
  createdAt: string;
  updatedAt: string;
}

export interface WpTask {
  taskId: string;
  id: number;
  taskNumber: number;
  phaseId: string;
  name: string;
  description: string;
  sortOrder: number;
  implementationNotes: string | null;
  state: string;
  previousActiveState: string | null;
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  targetFiles: FileReference[];
  attachments: FileReference[];
  blockedBy: TaskDep[];
  blocking: TaskDep[];
  createdAt: string;
  updatedAt: string;
}

export interface AcceptanceCriterionView {
  name: string;
  description: string;
  verificationMethod: "AutomatedTest" | "Manual" | "AgentReview";
  verificationResult: string | null;
  verifiedAt: string | null;
}

export interface WorkPackageDep {
  workPackageId: string;
  name: string;
  state: string;
  reason: string | null;
}

export interface TaskDep {
  taskId: string;
  name: string;
  state: string;
  reason: string | null;
}

export interface WorkPackageSummary {
  activeCount: number;
  inactiveCount: number;
  terminalCount: number;
}
```

### API Layer (`src/dashboard/src/api/work-packages.ts`)

```typescript
getWorkPackages(projectId: number, state?: string): Promise<WorkPackage[]>
getWorkPackage(projectId: number, wpNumber: number): Promise<WorkPackage>
getWorkPackageSummary(projectId: number): Promise<WorkPackageSummary>
deleteWorkPackage(projectId: number, wpNumber: number): Promise<void>
```

### Hooks (`src/dashboard/src/hooks/use-work-packages.ts`)

```typescript
useWorkPackages(projectId, stateFilter?)
useWorkPackage(projectId, wpNumber)
useWorkPackageSummary(projectId)
useDeleteWorkPackage()
```

### Routes

```
/projects/:id                                   → ProjectDetailPage (add WP summary cards + WP tab)
/projects/:id/work-packages/:wpNumber           → WorkPackageDetailPage (new)
```

### Pages

**ProjectDetailPage** — Add a second tab/section for work packages:
- Summary cards: Active WP count, Inactive WP count, Terminal WP count
- State filter buttons (reuse pattern from issues)
- WP table: ID, Name, Type, Priority, State, Progress (tasks completed/total), Created

**WorkPackageDetailPage** — New page:
- Header: WP name, ID badge, type badge, priority badge, state badge
- Cards: Definition (name, description, plan), Estimation, Dependencies (blocked by / blocking lists)
- Phase/Task tree: collapsible phases with nested tasks
  - Each phase shows: name, state, sort order, acceptance criteria (expandable)
  - Each task shows: name, state, sort order, implementation notes (expandable)
- Timeline card: StartedAt, CompletedAt, ResolvedAt, CreatedAt, UpdatedAt
- Delete button with confirmation dialog

### Sidebar

Add "Work Packages" nav item (visible when project selected), using a `Package` or `Layers` icon from lucide-react. Place it after the "Issues" nav item.

---

## 10. Migration

Single migration covering all 9 new tables:
```bash
dotnet ef migrations add AddWorkPackages --project src/PinkRooster.Data --startup-project src/PinkRooster.Api
```

---

## 11. Implementation Phases

### Phase A: Shared Layer
1. New enums: `WorkPackageType`, `VerificationMethod`
2. New DTOs: all request/response classes
3. IdParser extensions: `TryParseWorkPackageId`, `TryParsePhaseId`, `TryParseTaskId`
4. ApiRoutes constant for work packages

### Phase B: Data Layer
1. All 9 entity classes
2. All 9 EF configurations
3. AppDbContext: new DbSets + SaveChangesAsync extensions
4. EF Migration

### Phase C: API Layer
1. Service interfaces: `IWorkPackageService`, `IPhaseService`, `IWorkPackageTaskService`
2. Service implementations with state logic, audit logging, cycle detection, propagation
3. Controllers: `WorkPackageController`, `PhaseController`, `TaskController`
4. DI registration in Program.cs

### Phase D: MCP Layer
1. PinkRoosterApiClient: work package endpoint methods
2. MCP response types (7 classes)
3. WorkPackageTools: 7 MCP tools
4. Update ProjectTools.GetProjectOverview with WP summaries

### Phase E: Dashboard
1. TypeScript types
2. API layer + hooks
3. ProjectDetailPage: add WP section
4. WorkPackageDetailPage: new page with phase/task tree
5. App.tsx route
6. Sidebar nav item

### Phase F: Testing & Deployment
1. Docker rebuild: `docker compose up -d --build api mcp`
2. MCP tool testing flow
3. Dashboard rebuild
4. Integration tests (if desired)
