# MCP Tool Description Audit — Recommendations

> Audit date: 2026-03-10 | Based on: `claudedocs/research_tool_descriptions_20260310.md`
> Scope: All 16 MCP tools across 6 tool classes

---

## HIGH — JSON String Parameters (9 occurrences across 6 tools)

The research calls this an explicit **anti-pattern**: *"Never use JSON-encoded string parameters when structured types are available."* These force the LLM to generate valid JSON inside a string value — error-prone, no schema validation, and the description must verbally explain the schema (token-expensive and ambiguous).

| Tool | Parameter | Current Type | Should Be |
|------|-----------|-------------|-----------|
| `create_or_update_issue` | `attachments` | `string?` (JSON) | `List<FileReferenceDto>?` |
| `create_or_update_work_package` | `attachments` | `string?` (JSON) | `List<FileReferenceDto>?` |
| `scaffold_work_package` | `blockedByWorkPackageIds` | `string?` (JSON) | `List<string>?` |
| `scaffold_work_package` | `attachments` | `string?` (JSON) | `List<FileReferenceDto>?` |
| `create_or_update_phase` | `acceptanceCriteria` | `string?` (JSON) | `List<AcceptanceCriterionDto>?` |
| `create_or_update_phase` | `tasks` | `string?` (JSON) | typed list |
| `create_or_update_task` | `targetFiles` | `string?` (JSON) | `List<FileReferenceDto>?` |
| `create_or_update_task` | `attachments` | `string?` (JSON) | `List<FileReferenceDto>?` |
| `batch_update_task_states` | `tasks` | `string` (JSON) | `List<BatchTaskStateInput>` |

Note: `scaffold_work_package.phases` is already a typed `List<ScaffoldPhaseRequest>` — proof this pattern works. The `tasks` param in `create_or_update_phase` is especially problematic because it has dual schemas (create vs update format) described in a single string description.

---

## HIGH — Enum Values Listed in Descriptions Instead of Typed Enums

The research states: *"If a parameter can only accept a fixed set of values, always use the `enum` keyword. Enums are structurally enforced and dramatically reduce invalid inputs."*

Currently, enum values are listed textually in descriptions (e.g. `"State: NotStarted, Designing, Implementing, ..."`) but the parameter type is `string?`. This means no schema-level validation, the LLM can pass misspelled or invented values, and descriptions waste tokens repeating the same enum values across many tools.

| Parameter | Valid Values | Tools Affected |
|-----------|-------------|----------------|
| `state` | 9 CompletionState values | `create_or_update_issue`, `create_or_update_work_package`, `scaffold_work_package`, `create_or_update_phase`, `create_or_update_task` (5 tools) |
| `priority` | Critical, High, Medium, Low | `create_or_update_issue`, `create_or_update_work_package`, `scaffold_work_package` (3 tools) |
| `issueType` | 6 IssueType values | `create_or_update_issue` (1 tool) |
| `severity` | Critical, Major, Minor, Trivial | `create_or_update_issue` (1 tool) |
| `type` (WP) | Feature, BugFix, Refactor, Spike, Chore | `create_or_update_work_package`, `scaffold_work_package` (2 tools) |
| `stateFilter` | active, inactive, terminal | `get_issue_overview`, `get_work_packages` (2 tools) |
| `action` | add, remove | `manage_work_package_dependency`, `manage_task_dependency` (2 tools) |
| `entityType` | task, wp, issue | `get_next_actions` (1 tool) |

**Caveat**: The MCP C# SDK generates the schema from C# method signatures. Whether it supports `enum` in JSON Schema output for string params with `[AllowedValues]` or similar attributes needs investigation. If the SDK doesn't support it natively, the alternative is to keep them as strings but ensure descriptions remain concise.

---

## MEDIUM — Descriptions Missing Disambiguation ("When NOT to Use")

The research emphasizes: *"Specify when to use the function vs when not to. Agents confused by overlapping tools will waste calls."*

| Tool | Current Description | Missing Guidance |
|------|-------------------|-----------------|
| `get_issue_details` | "Returns full details for a specific issue." | Doesn't explain what "full" means or how it differs from `get_issue_overview`. Should say: "Returns all fields for a single issue by ID including state timestamps, attachments, and linked work packages. For listing multiple issues, use `get_issue_overview`." |
| `get_issue_overview` | "Returns a list of issues for a project..." | Doesn't contrast with `get_issue_details`. Should add: "Returns a compact list (ID, name, state, priority, severity). For full issue data, use `get_issue_details`." |
| `get_work_packages` | "Returns a list of work packages..." | Should add: "Returns compact list with task counts. For full WP tree (phases, tasks, deps), use `get_work_package_details`." |
| `get_work_package_details` | "Returns full details for a work package including phases, tasks..." | Already good. Could add: "Use `get_work_packages` for a compact list of all WPs first." |
| `scaffold_work_package` | Creates complete WP with phases/tasks | Should add: "For creating/updating a WP without phases or tasks, use `create_or_update_work_package` instead." |
| `create_or_update_work_package` | Creates or updates WP | Should add: "For creating a complete WP with phases and tasks in one call, use `scaffold_work_package`." |
| `get_project_status` | Good workflow position | Could add: "Does not include individual entity details — use `get_issue_details` or `get_work_package_details` to drill in." |

---

## MEDIUM — Integer Parameters Typed as String

These should be `int?` so the schema communicates the expected type. The LLM should never have to guess that `"3"` is an integer.

| Tool | Parameter | Current | Should Be |
|------|-----------|---------|-----------|
| `create_or_update_work_package` | `estimatedComplexity` | `string?` | `int?` |
| `scaffold_work_package` | `estimatedComplexity` | `string?` | `int?` |
| `create_or_update_phase` | `sortOrder` | `string?` | `int?` |
| `create_or_update_task` | `sortOrder` | `string?` | `int?` |

---

## MEDIUM — Server Instructions Underutilized

Current: `"PinkRooster MCP server providing project management tools."`

This one-liner wastes an opportunity. `ServerInstructions` is sent once during initialization and is ideal for shared domain knowledge that would otherwise be repeated in every tool description.

Suggested content:
- **Workflow guidance**: "Start with `get_project_status` to get the project ID, then use that ID with other tools."
- **ID format conventions**: "All entity IDs use human-readable formats: `proj-{N}`, `proj-{N}-issue-{N}`, `proj-{N}-wp-{N}`, `proj-{N}-wp-{N}-phase-{N}`, `proj-{N}-wp-{N}-task-{N}`."
- **State system overview**: "States fall into 3 categories: Active (Designing, Implementing, Testing, InReview), Inactive (NotStarted, Blocked), Terminal (Completed, Cancelled, Replaced)."
- **Error handling patterns**: "Write operations return OperationResult JSON with responseType, message, id, and optional stateChanges."

Moving shared domain knowledge here would **reduce per-tool description verbosity** (especially the repeated state/priority value lists) and keep individual descriptions focused.

---

## LOW — Terse Descriptions

| Tool | Current | Suggested |
|------|---------|-----------|
| `create_or_update_project` | "Creates or updates a project, matched by path. Returns the project id." | "Creates or updates a project, matched by path. Returns the project ID. Required to register a project before using other tools." |
| `get_issue_details` | "Returns full details for a specific issue." | "Returns all fields for a single issue including state timestamps, attachments, and linked work packages. For listing multiple issues, use `get_issue_overview`." |

---

## LOW — Minor Inconsistencies

1. **`get_project_status` takes `projectPath`** while all other tools take `projectId`. This is intentional (it's the entry point), but the description could make the relationship clearer: "Resolves a project by its filesystem path and returns its ID for use with other tools."

2. **`get_activity_logs` uses `JsonSerializerOptions.Web`** while all other tools use `JsonDefaults.Indented`. Activity logs get camelCase serialization; everything else uses the project's configured serializer.

3. **`create_or_update_phase.tasks` has dual semantics**: "For create: `[{name, description}]`. For update: `[{taskNumber, name}]`." Two different schemas in one parameter. If this stays as a JSON string, the description needs clearer separation. If converted to typed, consider separate parameters or separate tools.

---

## Summary

| Priority | Category | Count | Effort |
|----------|----------|-------|--------|
| **HIGH** | JSON string -> typed parameters | 9 params / 6 tools | Medium |
| **HIGH** | String -> enum parameters | 8 param types / 12+ tools | Low-Medium |
| **MEDIUM** | Add disambiguation to descriptions | 7 tools | Low (text only) |
| **MEDIUM** | String -> int parameters | 4 params / 4 tools | Low |
| **MEDIUM** | Enrich ServerInstructions | 1 file | Low |
| **LOW** | Terse descriptions | 2 tools | Low |
| **LOW** | Minor inconsistencies | 3 items | Low |

## Recommended Implementation Order

1. **Enrich ServerInstructions** — lowest effort, immediate token savings from removing repeated enum lists
2. **Improve tool descriptions** — text-only changes, no code logic changes
3. **Convert int parameters** — trivial type changes, remove `McpInputParser.ParseInt()` calls
4. **Convert enum parameters** — requires SDK investigation for JSON Schema support
5. **Convert JSON string parameters** — largest effort, most impactful for reliability
