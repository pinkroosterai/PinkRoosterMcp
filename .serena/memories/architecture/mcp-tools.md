# MCP Server & Tools

## Setup
- MCP SDK v1.0.0, Streamable HTTP (protocol 2025-03-26) + legacy SSE
- Registered in `.mcp.json` as `pinkrooster` at `http://localhost:5200`
- `PinkRoosterApiClient`: all 16 endpoints use `EnsureSuccessAsync` with body-aware error extraction

## 17 MCP Tools
| Tool | Type | Description |
|------|------|-------------|
| get_project_status | Read | Compact status: issue/WP counts by state, active/inactive/blocked lists |
| get_next_actions | Read | Priority-ordered actionable items with optional limit/entityType filter |
| create_or_update_project | Write | Upsert project by path (idempotent) |
| create_or_update_issue | Write | Create (omit issueId) or update (provide issueId) |
| get_issue_details | Read | Full issue data by composite ID |
| get_issue_overview | Read | List issues, filterable by state category |
| get_work_packages | Read | List WPs, filterable by state category |
| get_work_package_details | Read | Full WP tree: phases, tasks, dependencies, acceptance criteria |
| create_or_update_work_package | Write | Create or update WP (returns cascade state changes) |
| create_or_update_phase | Write | Create/update phase, optional batch task creation |
| create_or_update_task | Write | Create/update task (returns cascade state changes) |
| batch_update_task_states | Write | Update multiple task states in one call (idempotent) |
| manage_work_package_dependency | Write | Add/remove WP dependency with auto-block/unblock cascades (idempotent) |
| manage_task_dependency | Write | Add/remove task dependency with auto-block/unblock cascades (idempotent) |
| scaffold_work_package | Write | One-call WP creation with phases, tasks, dependencies |
| get_activity_logs | Read | Paginated HTTP request logs |

## MCP Tool Annotations
All tools use MCP SDK annotations:
- `Title` — human-readable display name on all 17 tools
- `ReadOnly = true` — on all 7 read tools
- `Destructive = false` — on all 10 write tools (none delete data)
- `Idempotent = true` — on create_or_update_project, batch_update_task_states, manage_*_dependency
- `OpenWorld = false` — on all 17 tools (closed domain)

## MCP-Specific Enums (Inputs/)
Constrained string parameters replaced with enum types for schema-level validation:
- `DependencyAction` — Add, Remove (used by manage_*_dependency tools)
- `StateFilterCategory` — Active, Inactive, Terminal (used by list tools)
- `EntityTypeFilter` — Task, Wp, Issue (used by get_next_actions)

## MCP Response Pattern
- Write ops return `OperationResult` JSON with responseType, message, id, nextStep, stateChanges
- `Success(id, message, nextStep?, stateChanges?)` for entity ops
- `SuccessMessage(message)` for informational ops
- Fields `id`, `nextStep`, `stateChanges` omitted when null (JsonIgnoreCondition.WhenWritingNull)

## MCP Input Types (Inputs/)
MCP tool parameters use MCP-specific input types (never shared DTOs directly):
- `FileReferenceInput` — file reference params (maps to FileReferenceDto)
- `AcceptanceCriterionInput` — acceptance criteria params (maps to AcceptanceCriterionDto)
- `PhaseTaskInput` — create_or_update_phase task params (maps to CreateTaskRequest or UpsertTaskInPhaseDto)
- `ScaffoldPhaseInput` / `ScaffoldTaskInput` — scaffold_work_package params (maps to ScaffoldPhaseRequest)
- `BatchTaskStateInput` — batch_update_task_states params (required TaskId + State)

## Shared Infrastructure
- `McpInputParser` (Helpers/): NullIfEmpty, IsTerminalState, mapping methods (MCP inputs → shared DTOs)
- Tool classes split by entity: ProjectTools, IssueTools, WorkPackageTools, PhaseTools, TaskTools, ActivityLogTools
