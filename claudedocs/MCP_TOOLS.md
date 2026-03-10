# MCP Tools Reference

PinkRooster exposes 14 MCP tools across 6 tool classes, registered as `pinkrooster` at `http://localhost:5200`. All tools communicate with the API via HTTP through `PinkRoosterApiClient`. Write tools never throw — they return `OperationResult` JSON with `responseType`, `message`, and optional `id`/`stateChanges`.

## Tool Classes

| Class | File | Tools |
|-------|------|-------|
| `ProjectTools` | `Tools/ProjectTools.cs` | `get_project_overview`, `create_or_update_project` |
| `IssueTools` | `Tools/IssueTools.cs` | `add_or_update_issue`, `get_issue_details`, `get_issue_overview` |
| `WorkPackageTools` | `Tools/WorkPackageTools.cs` | `get_work_packages`, `get_work_package_details`, `create_or_update_work_package`, `manage_work_package_dependency` |
| `PhaseTools` | `Tools/PhaseTools.cs` | `create_or_update_phase` |
| `TaskTools` | `Tools/TaskTools.cs` | `create_or_update_task`, `batch_update_task_states`, `manage_task_dependency` |
| `ActivityLogTools` | `Tools/ActivityLogTools.cs` | `get_activity_logs` |

## Shared Infrastructure

**`McpInputParser`** (`Helpers/McpInputParser.cs`) — static helpers used across all tool classes:
- `ParseEnumOrDefault<T>(string?, T)` — parse enum or return default
- `ParseEnum<T>(string)` — parse enum, null on failure
- `ParseInt(string?)` — parse nullable int
- `ParseFileReferences(string?)` — deserialize `FileReferenceDto[]` from JSON
- `ParseAcceptanceCriteria(string?)` — deserialize `AcceptanceCriterionDto[]` from JSON
- `ParseCreateTasks(string?)` / `ParseUpsertTasks(string?)` — deserialize task batch JSON
- `NullIfEmpty<T>(List<T>)` — returns null for empty lists (cleaner MCP output)
- `IsTerminalState(string)` — checks against `CompletionStateConstants.TerminalStates`

---

## Project Tools

### `get_project_overview` (read-only)
Entry point — call first when starting work on a project. Returns project info enriched with issue and work package summaries.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectPath` | string | yes | Absolute path to the project root directory |

**Returns**: `ProjectOverviewResponse` JSON with active/inactive issues, active/inactive WPs, terminal counts. Returns `OperationResult` warning if project not found.

### `create_or_update_project`
Upserts a project matched by path.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Display name |
| `description` | string | yes | Short description |
| `projectPath` | string | yes | Absolute path to the project root directory |

**Returns**: `OperationResult` with `id` (e.g. `proj-1`).

---

## Issue Tools

### `add_or_update_issue`
Creates or updates an issue. Omit `issueId` to create; provide it to update (PATCH semantics — null fields are unchanged).

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `issueId` | string | create: omit, update: yes | `proj-{N}-issue-{N}` format |
| `name` | string | create: yes | Issue title |
| `description` | string | create: yes | Detailed description |
| `issueType` | string | create: yes | `Bug`, `Defect`, `Regression`, `TechnicalDebt`, `PerformanceIssue`, `SecurityVulnerability` |
| `severity` | string | create: yes | `Critical`, `Major`, `Minor`, `Trivial` |
| `priority` | string | no | `Critical`, `High`, `Medium` (default), `Low` |
| `state` | string | no | `NotStarted` (default), `Designing`, `Implementing`, `Testing`, `InReview`, `Completed`, `Cancelled`, `Blocked`, `Replaced` |
| `stepsToReproduce` | string | no | Reproduction steps |
| `expectedBehavior` | string | no | Expected behavior |
| `actualBehavior` | string | no | Actual behavior observed |
| `affectedComponent` | string | no | Affected file/module/area |
| `stackTrace` | string | no | Stack trace or error output |
| `rootCause` | string | no | Root cause analysis |
| `resolution` | string | no | Resolution description |
| `attachments` | string | no | JSON array: `[{"fileName":"...","relativePath":"...","description":"..."}]` |

**Returns**: `OperationResult` with `id`.

### `get_issue_details` (read-only)
Full issue detail including timestamps and attachments.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `issueId` | string | yes | `proj-{N}-issue-{N}` format |

**Returns**: `IssueDetailResponse` JSON.

### `get_issue_overview` (read-only)
List issues for a project with optional state filtering.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `stateFilter` | string | no | `active`, `inactive`, `terminal`, or omit for all |

**Returns**: Array of `IssueOverviewItem` JSON.

---

## Work Package Tools

### `get_work_packages` (read-only)
List work packages for a project with optional state filtering.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `stateFilter` | string | no | `active`, `inactive`, `terminal`, or omit for all |

**Returns**: Array of `WorkPackageOverviewItem` JSON (includes phase/task counts).

### `get_work_package_details` (read-only)
Full WP tree: phases, tasks, acceptance criteria, dependencies.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `workPackageId` | string | yes | `proj-{N}-wp-{N}` format |

**Returns**: `WorkPackageDetailResponse` JSON with nested `Phases[].Tasks[]`, `BlockedBy[]`, `Blocking[]`.

### `create_or_update_work_package`
Creates or updates a work package. Omit `workPackageId` to create.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | string | yes | `proj-{N}` format |
| `workPackageId` | string | create: omit, update: yes | `proj-{N}-wp-{N}` format |
| `name` | string | create: yes | WP title |
| `description` | string | create: yes | Detailed description |
| `type` | string | no | `Feature` (default), `BugFix`, `Refactor`, `Spike`, `Chore` |
| `priority` | string | no | `Critical`, `High`, `Medium` (default), `Low` |
| `plan` | string | no | Implementation plan (markdown) |
| `estimatedComplexity` | string | no | Integer complexity estimate |
| `estimationRationale` | string | no | Rationale for estimate |
| `state` | string | no | `NotStarted` (default), same 9-value enum as issues |
| `linkedIssueId` | string | no | `proj-{N}-issue-{N}` format |
| `attachments` | string | no | JSON array of file references |

**Returns**: `OperationResult` with `id` and optional `stateChanges` on update.

### `manage_work_package_dependency`
Adds or removes a WP-to-WP dependency. Adding auto-blocks active dependents.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `workPackageId` | string | yes | Dependent WP (`proj-{N}-wp-{N}`) |
| `dependsOnWorkPackageId` | string | yes | Blocker WP (`proj-{N}-wp-{N}`) |
| `action` | string | yes | `add` or `remove` |
| `reason` | string | no | Reason for the dependency |

**Returns**: `OperationResult` with `stateChanges` on add (shows auto-block). Errors on circular dependency.

---

## Phase Tools

### `create_or_update_phase`
Creates or updates a phase, optionally with batch task creation/update.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `workPackageId` | string | yes | `proj-{N}-wp-{N}` format |
| `phaseId` | string | create: omit, update: yes | `proj-{N}-wp-{N}-phase-{N}` format |
| `name` | string | create: yes | Phase name |
| `description` | string | no | Phase description |
| `sortOrder` | string | no | Integer sort order |
| `state` | string | no (update only) | Same 9-value enum |
| `acceptanceCriteria` | string | no | JSON array: `[{"name":"...","description":"...","verificationMethod":"Manual\|Automated\|CodeReview"}]` |
| `tasks` | string | no | For create: `[{"name":"...","description":"..."}]`. For update: `[{"taskNumber":1,"name":"..."}]` |

**Returns**: `OperationResult` with `id` and optional `stateChanges` on update.

---

## Task Tools

### `create_or_update_task`
Creates or updates a task. Provide `phaseId` for create, `taskId` for update.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `phaseId` | string | create: yes | `proj-{N}-wp-{N}-phase-{N}` format |
| `taskId` | string | update: yes | `proj-{N}-wp-{N}-task-{N}` format |
| `name` | string | create: yes | Task name |
| `description` | string | create: yes | Task description |
| `sortOrder` | string | no | Integer sort order |
| `implementationNotes` | string | no | Implementation notes |
| `state` | string | no | Same 9-value enum |
| `targetFiles` | string | no | JSON array of file references |
| `attachments` | string | no | JSON array of file references |

**Returns**: `OperationResult` with `id` and optional `stateChanges` on update.

### `batch_update_task_states`
Updates the state of multiple tasks in a single work package in one operation. Cascades (auto-unblock, phase/WP auto-complete) run once after all transitions, returning a consolidated state change list.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `workPackageId` | string | yes | `proj-{N}-wp-{N}` format |
| `tasks` | string | yes | JSON array: `[{"taskId":"proj-1-wp-1-task-1","state":"Completed"},...]` |

**Returns**: `OperationResult` with `id` (the work package ID), message showing count of updated tasks, and consolidated `stateChanges`.

### `manage_task_dependency`
Adds or removes a task-to-task dependency. Adding auto-blocks active dependents.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `taskId` | string | yes | Dependent task (`proj-{N}-wp-{N}-task-{N}`) |
| `dependsOnTaskId` | string | yes | Blocker task (`proj-{N}-wp-{N}-task-{N}`) |
| `action` | string | yes | `add` or `remove` |
| `reason` | string | no | Reason for the dependency |

**Returns**: `OperationResult` with `stateChanges` on add. Errors on circular dependency.

---

## Activity Log Tools

### `get_activity_logs` (read-only)
Paginated HTTP request logs from the API.

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
