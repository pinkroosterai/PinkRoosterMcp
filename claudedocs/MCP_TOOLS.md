# MCP Tools Reference

PinkRooster exposes 17 MCP tools across 6 tool classes, registered as `pinkrooster` at `http://localhost:5200`. All tools communicate with the API via HTTP through `PinkRoosterApiClient`. Write tools never throw — they return `OperationResult` JSON with `responseType`, `message`, and optional `id`/`stateChanges`.

## Tool Classes

| Class | File | Tools |
|-------|------|-------|
| `ProjectTools` | `Tools/ProjectTools.cs` | `get_project_status`, `get_next_actions`, `create_or_update_project` |
| `IssueTools` | `Tools/IssueTools.cs` | `create_or_update_issue`, `get_issue_details`, `get_issue_overview` |
| `WorkPackageTools` | `Tools/WorkPackageTools.cs` | `get_work_packages`, `get_work_package_details`, `create_or_update_work_package`, `manage_work_package_dependency`, `scaffold_work_package` |
| `PhaseTools` | `Tools/PhaseTools.cs` | `create_or_update_phase` |
| `TaskTools` | `Tools/TaskTools.cs` | `create_or_update_task`, `batch_update_task_states`, `manage_task_dependency` |
| `ActivityLogTools` | `Tools/ActivityLogTools.cs` | `get_activity_logs` |

## MCP Tool Annotations

All tools use MCP annotations for client hints:
- **`Title`** — human-readable display name on all 17 tools
- **`ReadOnly = true`** — on all 7 read tools
- **`Destructive = false`** — on all 10 write tools (none delete data)
- **`Idempotent = true`** — on `create_or_update_project`, `batch_update_task_states`, `manage_*_dependency`
- **`OpenWorld = false`** — on all 17 tools (closed domain)

## MCP-Specific Enums (Inputs/)

Constrained string parameters use enum types for schema-level validation:
- `DependencyAction` — `Add`, `Remove` (used by `manage_*_dependency` tools)
- `StateFilterCategory` — `Active`, `Inactive`, `Terminal` (used by list tools)
- `EntityTypeFilter` — `Task`, `Wp`, `Issue` (used by `get_next_actions`)

## MCP Input Types (Inputs/)

MCP tool parameters use MCP-specific input types (never shared DTOs directly):
- `FileReferenceInput` — file reference params (maps to FileReferenceDto)
- `AcceptanceCriterionInput` — acceptance criteria params (maps to AcceptanceCriterionDto)
- `PhaseTaskInput` — create_or_update_phase task params (maps to CreateTaskRequest or UpsertTaskInPhaseDto)
- `ScaffoldPhaseInput` / `ScaffoldTaskInput` — scaffold_work_package params (maps to ScaffoldPhaseRequest)
- `BatchTaskStateInput` — batch_update_task_states params (required TaskId + State)

## Shared Infrastructure

**`McpInputParser`** (`Helpers/McpInputParser.cs`) — static helpers used across all tool classes:
- `MapFileReferences(List<FileReferenceInput>?)` — maps MCP input to shared DTO
- `MapAcceptanceCriteria(List<AcceptanceCriterionInput>?)` — maps criteria input to shared DTO
- `MapCreateTasks(List<PhaseTaskInput>?)` / `MapUpsertTasks(List<PhaseTaskInput>?)` — maps task batch inputs
- `MapScaffoldPhases(List<ScaffoldPhaseInput>)` — maps scaffold phase/task inputs
- `NullIfEmpty<T>(List<T>)` — returns null for empty lists (cleaner MCP output)
- `IsTerminalState(string)` — checks against `CompletionStateConstants.TerminalStates`

---

## Project Tools

### `get_project_status` (read-only)
Entry point — call first when starting work on a project. Resolves by filesystem path, returns project ID and compact status summary.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectPath` | string | yes | Absolute path to the project root directory |

**Returns**: `ProjectStatusResponse` JSON with issue/WP counts by state category and active/inactive item lists. Returns `OperationResult` warning if project not found.

### `get_next_actions` (read-only)
Priority-ordered actionable items. Use after `get_project_status` to decide what to work on next. Excludes blocked and terminal items.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `limit` | int | no | Max items to return (default: 10) |
| `entityType` | `EntityTypeFilter?` | no | Filter: `Task`, `Wp`, or `Issue`. Omit for all. |

**Returns**: Array of `NextActionItem` JSON sorted by priority then entity type (tasks first).

### `create_or_update_project`
Upserts a project matched by path. Idempotent — calling again with same path updates name/description.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Project display name |
| `description` | string | yes | Short project description |
| `projectPath` | string | yes | Absolute path to the project root directory |

**Returns**: `OperationResult` with `id` (e.g. `proj-1`).

---

## Issue Tools

### `create_or_update_issue`
Creates or updates an issue. Issues track bugs and problems — use `create_or_update_work_package` for planned work. Omit `issueId` to create; provide it to update (PATCH semantics — null fields unchanged).

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `issueId` | string | create: omit, update: yes | `proj-{N}-issue-{N}` format |
| `name` | string | create: yes | Issue title |
| `description` | string | create: yes | Detailed description |
| `issueType` | `IssueType` | create: yes | Bug, Defect, Regression, TechnicalDebt, PerformanceIssue, SecurityVulnerability |
| `severity` | `IssueSeverity` | create: yes | Critical, Major, Minor, Trivial |
| `priority` | `Priority?` | no | Critical, High, Medium (default), Low |
| `state` | `CompletionState?` | no | Omit to keep current. Default: NotStarted |
| `stepsToReproduce` | string | no | Reproduction steps |
| `expectedBehavior` | string | no | Expected behavior |
| `actualBehavior` | string | no | Actual behavior observed |
| `affectedComponent` | string | no | Affected file/module/area |
| `stackTrace` | string | no | Stack trace or error output |
| `rootCause` | string | no | Root cause analysis |
| `resolution` | string | no | Resolution description |
| `attachments` | `List<FileReferenceInput>?` | no | File attachments |

**Returns**: `OperationResult` with `id`.

### `get_issue_details` (read-only)
Full issue detail including timestamps, attachments, and linked work packages. For listing multiple issues, use `get_issue_overview` instead.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `issueId` | string | yes | `proj-{N}-issue-{N}` format |

**Returns**: `IssueDetailResponse` JSON.

### `get_issue_overview` (read-only)
Compact list of issues (ID, name, state, priority, severity). For issue counts by category, use `get_project_status` instead.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `stateFilter` | `StateFilterCategory?` | no | `Active`, `Inactive`, `Terminal`, or omit for all |

**Returns**: Array of compact issue objects.

---

## Work Package Tools

### `get_work_packages` (read-only)
Compact list of work packages (ID, name, state, task counts). For WP counts by category, use `get_project_status`. For full WP tree, use `get_work_package_details`.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `stateFilter` | `StateFilterCategory?` | no | `Active`, `Inactive`, `Terminal`, or omit for all |

**Returns**: Array of compact WP objects (includes phase/task counts).

### `get_work_package_details` (read-only)
Full WP tree: phases, tasks, acceptance criteria, dependencies. Use `get_work_packages` for a compact list first.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `workPackageId` | string | yes | `proj-{N}-wp-{N}` format |

**Returns**: `WorkPackageDetailResponse` JSON with nested `Phases[].Tasks[]`, `BlockedBy[]`, `Blocking[]`.

### `create_or_update_work_package`
Creates or updates a work package. Returns WP ID and any cascade state changes. For creating a complete WP with phases and tasks, use `scaffold_work_package` instead.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `workPackageId` | string | create: omit, update: yes | `proj-{N}-wp-{N}` format |
| `name` | string | create: yes | WP title |
| `description` | string | create: yes | Detailed description |
| `type` | `WorkPackageType?` | no | Feature (default), BugFix, Refactor, Spike, Chore |
| `priority` | `Priority?` | no | Default: Medium |
| `plan` | string | no | Implementation plan (supports markdown) |
| `estimatedComplexity` | int? | no | 1-10 scale |
| `estimationRationale` | string | no | Rationale for estimate |
| `state` | `CompletionState?` | no | Omit to keep current. Default: NotStarted |
| `linkedIssueId` | string | no | `proj-{N}-issue-{N}` format |
| `attachments` | `List<FileReferenceInput>?` | no | File attachments |

**Returns**: `OperationResult` with `id` and optional `stateChanges` on update.

### `manage_work_package_dependency`
Adds or removes a WP-to-WP dependency. When adding: if the blocker is non-terminal, the dependent auto-transitions to Blocked. When the blocker completes, dependents auto-unblock. Returns stateChanges showing automatic transitions.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `workPackageId` | string | yes | Dependent WP (`proj-{N}-wp-{N}`) |
| `dependsOnWorkPackageId` | string | yes | Blocker WP (`proj-{N}-wp-{N}`) |
| `action` | `DependencyAction` | yes | `Add` or `Remove` |
| `reason` | string | no | Reason for the dependency |

**Returns**: `OperationResult` with `stateChanges` on add (shows auto-block). Errors on circular dependency.

### `scaffold_work_package`
Creates a complete WP with phases, tasks, acceptance criteria, and task dependencies in one call. For creating/updating a WP without phases, use `create_or_update_work_package` instead.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `name` | string | yes | WP title |
| `description` | string | yes | Detailed description |
| `phases` | `List<ScaffoldPhaseInput>` | yes | Phases with optional tasks and criteria |
| `type` | `WorkPackageType?` | no | Default: Feature |
| `priority` | `Priority?` | no | Default: Medium |
| `plan` | string | no | Implementation plan (supports markdown) |
| `estimatedComplexity` | int? | no | 1-10 scale |
| `estimationRationale` | string | no | Rationale |
| `state` | `CompletionState?` | no | Default: NotStarted |
| `linkedIssueId` | string | no | `proj-{N}-issue-{N}` format |
| `blockedByWorkPackageIds` | `List<string>?` | no | Existing WP IDs that block this WP |
| `attachments` | `List<FileReferenceInput>?` | no | File attachments |

**Returns**: `ScaffoldOperationResult` with ID map of all created entities.

---

## Phase Tools

### `create_or_update_phase`
Creates or updates a phase, optionally with batch task creation/update. For creating a full WP with phases and tasks at once, use `scaffold_work_package` instead.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `workPackageId` | string | yes | `proj-{N}-wp-{N}` format |
| `phaseId` | string | create: omit, update: yes | `proj-{N}-wp-{N}-phase-{N}` format |
| `name` | string | create: yes | Phase name |
| `description` | string | no | Phase description |
| `sortOrder` | int? | no | Display sort order |
| `state` | `CompletionState?` | no (update only) | Omit to keep current |
| `acceptanceCriteria` | `List<AcceptanceCriterionInput>?` | no | Replaces all existing criteria on update |
| `tasks` | `List<PhaseTaskInput>?` | no | Create: `[{name, description}]`. Update: `[{taskNumber, ...fields}]` |

**Returns**: `OperationResult` with `id` and optional `stateChanges` on update.

---

## Task Tools

### `create_or_update_task`
Creates or updates a task. For creating multiple tasks at once, use `create_or_update_phase` with tasks parameter or `scaffold_work_package` instead.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `phaseId` | string | create: yes | `proj-{N}-wp-{N}-phase-{N}` format |
| `taskId` | string | update: yes | `proj-{N}-wp-{N}-task-{N}` format |
| `name` | string | create: yes | Task name |
| `description` | string | create: yes | Task description |
| `sortOrder` | int? | no | Display sort order |
| `implementationNotes` | string | no | Implementation notes (supports markdown) |
| `state` | `CompletionState?` | no | Omit to keep current |
| `targetFiles` | `List<FileReferenceInput>?` | no | Target files for this task |
| `attachments` | `List<FileReferenceInput>?` | no | File attachments |

**Returns**: `OperationResult` with `id` and optional `stateChanges` on update.

### `batch_update_task_states`
Updates the state of multiple tasks in one operation. Cascades run once after all transitions. For updating individual task fields beyond state, use `create_or_update_task` instead.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `workPackageId` | string | yes | `proj-{N}-wp-{N}` format |
| `tasks` | `List<BatchTaskStateInput>` | yes | Array of `{taskId, state}` (both required) |

**Returns**: `OperationResult` with `id` (WP ID), update count, and consolidated `stateChanges`.

### `manage_task_dependency`
Adds or removes a task-to-task dependency. When adding: if the blocker is non-terminal, the dependent auto-transitions to Blocked. When the blocker completes, dependents auto-unblock. Returns stateChanges showing automatic transitions.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `taskId` | string | yes | Dependent task (`proj-{N}-wp-{N}-task-{N}`) |
| `dependsOnTaskId` | string | yes | Blocker task (`proj-{N}-wp-{N}-task-{N}`) |
| `action` | `DependencyAction` | yes | `Add` or `Remove` |
| `reason` | string | no | Reason for the dependency |

**Returns**: `OperationResult` with `stateChanges` on add. Errors on circular dependency.

---

## Activity Log Tools

### `get_activity_logs` (read-only)
Paginated HTTP request logs. Use to audit recent API activity or debug issues with tool calls.

| Param | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `page` | int | no | 1 | Page number |
| `pageSize` | int | no | 25 | Items per page |

**Returns**: Paginated activity log JSON.

---

## ID Format Reference

| Entity | Format | Example |
|--------|--------|---------|
| Project | `proj-{N}` | `proj-1` |
| Issue | `proj-{N}-issue-{N}` | `proj-1-issue-3` |
| Work Package | `proj-{N}-wp-{N}` | `proj-1-wp-2` |
| Phase | `proj-{N}-wp-{N}-phase-{N}` | `proj-1-wp-2-phase-1` |
| Task | `proj-{N}-wp-{N}-task-{N}` | `proj-1-wp-2-task-5` |

## State Cascade Behavior

Write operations may trigger automatic state transitions reported in `stateChanges`:
- **Auto-block**: Adding a dependency on a non-terminal entity auto-transitions active dependents to `Blocked`
- **Auto-unblock**: Completing a blocker restores dependents to their `PreviousActiveState`
- **Upward propagation**: All tasks terminal → phase auto-completes → all phases terminal → WP auto-completes
