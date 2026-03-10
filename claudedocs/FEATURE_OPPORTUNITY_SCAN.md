# Feature Opportunity Scan — MCP Tools for AI Agent Usability

---

## Application Profile

**Domain:** AI-agent-driven software development project management platform
**Primary users:** AI coding agents (Claude, GPT, etc.) managing development projects through MCP tools; human stakeholders observing progress via a read-only dashboard
**Core capabilities:**
- Project registration and overview (issues + work packages combined)
- Issue tracking with full lifecycle (creation, triage, resolution, audit trail)
- Work package management with hierarchical decomposition (WP → phases → tasks)
- Dependency management with automatic cascade propagation (auto-block, auto-unblock, upward completion)
- State-driven timestamps (started, completed, resolved) computed automatically
- Batch task state updates with consolidated cascade reporting
- Issue ↔ Work Package traceability (bidirectional)
- Read-only dashboard with deletion capability for human oversight
- Full-field audit logging on issues

**Integrations present:**
- MCP protocol (Streamable HTTP + SSE) for AI agent communication
- PostgreSQL 17 for persistence
- REST API as intermediary between MCP and database

**Current MCP tool count:** 14 tools across 6 tool classes

---

## Feature Capability Map (Current State)

| Capability Area | What Exists | What Agents Can Do |
|---|---|---|
| **Project management** | Create/update projects, get overview | Upsert projects, view combined issue+WP summary |
| **Issue tracking** | Full CRUD, state management, audit | Create, update (PATCH), view detail/list |
| **Work packages** | Full CRUD, hierarchical decomposition | Create, update, view detail/list |
| **Phases** | Create/update with batch task creation | Create phases, optionally batch-create tasks |
| **Tasks** | Full CRUD, batch state updates | Create, update, batch-update states |
| **Dependencies** | WP-WP and Task-Task, circular detection | Add/remove, see auto-block/unblock cascades |
| **Traceability** | Issue→WP linking (bidirectional read) | Link WP to issue, see linked WPs on issue |
| **Audit** | Full field-level audit on issues | **No MCP tool** — audit only via dashboard |
| **Activity log** | HTTP request logging | Paginated log viewing |

---

## Recommendations

### #1 — One-Call Work Package Scaffolding

**Category:** F. Bulk Operations & Efficiency
**Benefits:** AI agents creating implementation plans currently need 4–10 sequential tool calls to set up a work package with phases and tasks. A single scaffolding call eliminates round-trips and saves context window tokens on repeated OperationResult parsing.
**Value:** High

**Current state:**
To create a work package with 3 phases and 9 tasks, an agent must:
1. Call `create_or_update_work_package` → get WP ID
2. Call `create_or_update_phase` with tasks for phase 1 → get phase ID
3. Call `create_or_update_phase` with tasks for phase 2
4. Call `create_or_update_phase` with tasks for phase 3
5. Optionally call `manage_task_dependency` for each inter-phase dependency

That's 4 minimum calls with 4 separate response payloads. For a typical AI implementation workflow, this is the most common operation — and the most expensive in terms of context window.

**Proposed feature:**
A `scaffold_work_package` MCP tool that accepts a complete work package definition in a single call: WP metadata, phases with acceptance criteria, tasks per phase, and task-to-task dependencies. Returns the created WP ID plus all generated phase and task IDs in a compact response. The API handles sequencing internally (create WP → phases → tasks → dependencies) in a single transaction.

Scope in: WP + phases + tasks + acceptance criteria + intra-WP task dependencies in one call.
Scope out: Cross-WP dependencies (those still require `manage_work_package_dependency`). Updating existing WPs (use `create_or_update_work_package` for that).

**User stories:**
- "As an AI agent planning a feature implementation, I want to create a complete work package structure in one call so that I use fewer context tokens and reduce latency."
- "As an AI agent, I want all phase and task IDs returned in a structured response so that I can immediately reference them for follow-up operations."

**Depends on:** Nothing — new tool + new API endpoint.

---

### #2 — Compact Project Status Summary ✅ IMPLEMENTED

**Category:** E. Reporting, Analytics & Insights
**Benefits:** AI agents resuming a project session need to quickly understand where things stand. The current `get_project_overview` returns every active issue and work package as full objects — extremely token-expensive for the "what's the status?" question that opens most agent sessions.
**Value:** High

**Current state:**
`get_project_overview` makes 7 internal API calls and returns a response containing full `IssueOverviewItem` and `WorkPackageOverviewItem` arrays. For a project with 20 active issues and 15 active work packages, this can easily consume 3,000–5,000 tokens — just to answer "how are things going?"

There is no lightweight alternative. The agent must either accept the token cost or build its own summary from the raw data, which wastes reasoning tokens.

**Proposed feature:**
A `get_project_status` MCP tool returning a concise, structured summary optimized for agent decision-making:
- Overall progress: total issues/WPs, counts by state category (active/inactive/terminal), percentage complete
- Blocked items: count and IDs of blocked WPs and tasks (just IDs and names, not full objects)
- Recently changed: last N state transitions across all entities (from StateChanges/audit data)
- Stale items: entities in active states with no updates in the last N days
- Dependency bottlenecks: entities that are blocking the most other entities

Target response size: under 500 tokens for a typical project.

Scope in: Counts, percentages, blocked/stale item summaries, top bottlenecks.
Scope out: Full entity details (agent can drill into specifics with existing detail tools).

**User stories:**
- "As an AI agent starting a session, I want a token-efficient project status so that I can decide what to focus on without consuming my context window."
- "As an AI agent, I want to see what's blocked and stale so that I can prioritize unblocking work."

**Depends on:** Nothing — new tool + new API endpoint. Some fields (stale items, bottlenecks) require new queries but no schema changes.

---

### #3 — Acceptance Criteria Verification

**Category:** A. Workflow Completion & Lifecycle Gaps
**Benefits:** AI agents implementing features need to verify that acceptance criteria are met before marking phases complete. The data model already has `VerificationResult` and `VerifiedAt` fields on acceptance criteria, but no MCP tool can update them — the verification workflow is dead-ended.
**Value:** High

**Current state:**
Phases have acceptance criteria with three fields that exist in the database but are never written to by any MCP tool:
- `VerificationMethod` — set at creation (Manual, AutomatedTest, AgentReview)
- `VerificationResult` — always null (no tool to set it)
- `VerifiedAt` — always null (no tool to set it)

An AI agent completing a phase has no way to record whether its acceptance criteria passed. The data model supports a verification workflow that doesn't exist.

**Proposed feature:**
A `verify_acceptance_criteria` MCP tool that accepts a phase ID and an array of verification results. For each criterion (matched by name), the agent provides a pass/fail result and optional notes. The tool updates `VerificationResult` and sets `VerifiedAt` timestamps.

Optionally: if all criteria pass, auto-transition the phase to Completed (mirroring the task→phase cascade pattern).

Scope in: Update verification results per criterion, set verified timestamps.
Scope out: Changing the criteria definitions themselves (that's done via `create_or_update_phase`).

**User stories:**
- "As an AI agent finishing implementation of a phase, I want to record verification results against each acceptance criterion so that the project has a quality audit trail."
- "As an AI agent, I want the phase to auto-complete when all criteria pass so that I don't have to make a separate state-update call."

**Depends on:** May need a new API endpoint for batch criterion updates (current phase PATCH does full replacement of criteria, which would lose task associations). Alternatively, extend existing phase update to support partial criterion updates.

---

### #4 — Cross-Entity Search

**Category:** B. User Self-Service & Empowerment
**Benefits:** AI agents frequently need to find entities by name, state, target file, or other attributes. Currently, the only way is to list everything and parse. A search tool saves context tokens and provides instant answers.
**Value:** High

**Current state:**
An agent wanting to answer "which tasks target `src/PinkRooster.Api/Services/IssueService.cs`?" must:
1. Call `get_work_packages` to list all WPs
2. Call `get_work_package_details` for each relevant WP
3. Parse every task's `targetFiles` array to find matches

For a project with 10 work packages, that's 11 tool calls and thousands of tokens — to answer a simple lookup question. The same friction applies to finding issues by name, tasks by state across WPs, or anything not directly addressable by ID.

**Proposed feature:**
A `search_project` MCP tool that searches across entities within a project. Accepts:
- `query` — free text to match against names and descriptions
- `entityType` — optional filter: "issue", "workpackage", "phase", "task"
- `state` — optional state filter
- `targetFile` — optional file path (searches task targetFiles)

Returns compact results: entity ID, name, state, type/priority, parent context (e.g., which WP a task belongs to). Limit to top 20 results. Maximum ~800 tokens response.

Scope in: Search by text, state, entity type, and target file path within a single project.
Scope out: Cross-project search, full-text indexing, fuzzy matching (simple ILIKE/contains is sufficient).

**User stories:**
- "As an AI agent, I want to find all tasks targeting a specific file so that I can update their status after modifying that file."
- "As an AI agent, I want to search for issues by keyword so that I can check for duplicates before creating a new one."

**Depends on:** New API endpoint with query capabilities. No schema changes needed.

---

### #5 — Priority-Ordered Next Actions

**Category:** J. Natural Feature Extensions
**Benefits:** AI agents managing projects need to decide what to work on next. Currently, they must fetch all active items, check dependencies, and reason about priority — expensive in both tokens and reasoning effort. A "what's next?" tool does this computation server-side.
**Value:** High

**Current state:**
To determine what to work on next, an agent must:
1. `get_project_overview` or `get_work_packages` + `get_issue_overview` (expensive)
2. Filter to items that are not blocked and not terminal
3. Sort by priority
4. Check that dependencies are satisfied
5. Reason about which is most important

This is 1-3 tool calls plus significant reasoning, repeated at the start of every session.

**Proposed feature:**
A `get_next_actions` MCP tool that returns a prioritized list of actionable items:
- Unblocked tasks sorted by priority, then by WP priority, then by sort order
- Unblocked work packages that need their first phase/task created
- Active issues without linked work packages (need implementation planning)
- Recently unblocked items (flagged so agent knows what just became available)

Returns top N items (configurable, default 10) with: entity ID, name, type, priority, parent context, and a short reason why it's suggested (e.g., "High priority, no blockers", "Just unblocked by completion of proj-1-wp-2-task-3").

Scope in: Server-side prioritization of actionable work items.
Scope out: AI-generated priority recommendations, deadline awareness (no deadline fields exist).

**User stories:**
- "As an AI agent starting a work session, I want to see the highest-priority actionable items so that I can immediately begin productive work."
- "As an AI agent, I want to know what just became unblocked so that I can pick up newly available work."

**Depends on:** #2 (shares the query infrastructure for blocked/unblocked status analysis), but could be built independently.

---

### #6 — Issue Audit Trail via MCP

**Category:** C. Visibility, History & Audit
**Benefits:** AI agents updating issues have no way to see what changed previously. The audit log API endpoint exists and the dashboard displays it, but there is no MCP tool. This creates a blind spot where agents may repeat changes or miss context about why a field was changed.
**Value:** Medium

**Current state:**
The API endpoint `GET /api/projects/{projectId}/issues/{issueNumber}/audit` exists and returns full field-level change history (`IssueAuditLogResponse` with FieldName, OldValue, NewValue, ChangedBy, ChangedAt). The dashboard displays this data. But the `PinkRoosterApiClient` has no method calling this endpoint, and no MCP tool exposes it.

The endpoint is fully functional — this is purely a missing MCP bridge.

**Proposed feature:**
A `get_issue_audit_trail` MCP tool that returns the audit log for an issue. Accepts:
- `issueId` — standard format `proj-{N}-issue-{N}`
- `limit` — optional, default 20 (most recent entries)

Returns a compact list of changes: timestamp, field name, old→new value, changed by. Omit unchanged/null old values on creation entries to save tokens.

Scope in: Issue audit log retrieval with optional limit.
Scope out: WP/task audit logs (those audit tables exist but don't have API endpoints yet — that would be a larger effort).

**User stories:**
- "As an AI agent, I want to see what changed on an issue recently so that I can understand its current context before making updates."
- "As an AI agent, I want to verify that my previous update was applied correctly by checking the audit trail."

**Depends on:** Nothing — API endpoint already exists. Just needs client method + MCP tool.

---

### #7 — Work Package Completion Readiness Check

**Category:** A. Workflow Completion & Lifecycle Gaps
**Benefits:** AI agents need to know if a work package is ready to be marked complete — are all tasks done? Are all acceptance criteria verified? Are there dangling blocked items? Currently, the agent must fetch the full WP tree and compute this manually, wasting tokens on data parsing.
**Value:** Medium

**Current state:**
To check if a work package can be completed, an agent must:
1. `get_work_package_details` (returns the full tree — all phases, tasks, dependencies, criteria)
2. Manually check: all tasks in terminal states? All phases terminal? Any blocked items? Acceptance criteria verified?

The full detail response for a WP with 3 phases and 12 tasks can be 2,000+ tokens. The agent only needs a yes/no answer with reasons.

**Proposed feature:**
A `check_work_package_readiness` MCP tool that analyzes a work package and returns:
- `ready`: boolean — can this WP be completed?
- `blockers`: list of reasons why not (if any):
  - "3 tasks still in active states: [task IDs]"
  - "Phase 2 has 1 unverified acceptance criterion"
  - "Task proj-1-wp-3-task-7 is blocked by proj-1-wp-3-task-2"
- `summary`: compact progress (e.g., "8/12 tasks complete, 2/3 phases complete")

Target response: under 300 tokens.

Scope in: Task/phase completion status, dependency status, acceptance criteria verification status.
Scope out: Quality judgment (e.g., "is the implementation good enough?") — only structural readiness.

**User stories:**
- "As an AI agent, I want to quickly check if a work package is ready to close so that I don't waste time investigating when there are still open tasks."
- "As an AI agent, I want to know exactly what's blocking completion so that I can address the remaining items directly."

**Depends on:** Nothing — new tool + new API endpoint using existing data.

---

### #8 — Batch Issue State Updates

**Category:** F. Bulk Operations & Efficiency
**Benefits:** Tasks have `batch_update_task_states`, but issues have no equivalent. AI agents resolving multiple related issues (e.g., closing duplicates, bulk-triaging incoming bugs) must make individual update calls. This is the same efficiency gap that `batch_update_task_states` solved for tasks.
**Value:** Medium

**Current state:**
When an agent determines that 5 issues should be closed as duplicates, or triages 8 new issues with initial severity/priority assignments, each requires a separate `add_or_update_issue` call. Each call returns an `OperationResult` consuming ~100 tokens. For 8 issues, that's 8 calls and ~800 tokens of responses.

**Proposed feature:**
A `batch_update_issues` MCP tool that accepts:
- `projectId` — standard format
- `issues` — JSON array of `[{"issueId": "...", "state": "...", "priority": "...", ...}]`

Applies partial updates to each issue, returns a consolidated response: count updated, per-issue old→new state, any validation errors. Same PATCH semantics as individual updates — null fields are not changed.

Scope in: Batch state changes and field updates for multiple issues.
Scope out: Batch issue creation (different enough to be a separate tool if needed).

**User stories:**
- "As an AI agent triaging multiple incoming issues, I want to set their severity and priority in one call so that I can move quickly through triage."
- "As an AI agent, I want to close multiple related issues as duplicates in one operation."

**Depends on:** New API endpoint, similar pattern to `batch-states` on tasks.

---

### #9 — Focused Dependency Graph View

**Category:** C. Visibility, History & Audit
**Benefits:** AI agents managing complex projects with many dependencies need to understand the blocking chain. Currently, dependency info is buried inside full WP/task detail responses. A focused dependency view saves tokens and makes impact analysis possible.
**Value:** Medium

**Current state:**
To understand "what is blocking what?" across a project, an agent must:
1. `get_work_packages` to list all WPs
2. `get_work_package_details` for each WP to see its `blockedBy`/`blocking` and task-level dependencies

For 10 work packages, that's 11 calls and potentially 10,000+ tokens — just to build a mental model of the dependency graph.

**Proposed feature:**
A `get_dependency_graph` MCP tool that returns the complete dependency structure for a project in a compact format:
- WP-level dependencies: `[{from: "wp-1", to: "wp-2", reason: "...", fromState: "...", toState: "..."}]`
- Task-level dependencies (optional, if requested): same format within each WP
- Blocked chains: sequences like "task-5 → task-3 → task-1 (root blocker)"
- Critical path: the longest chain of incomplete dependencies

Response format optimized for compactness — just IDs, states, and relationships. No descriptions, timestamps, or other metadata.

Scope in: All WP and task dependencies within a project, blocking chain analysis.
Scope out: Cross-project dependencies (not supported), visual rendering (that's a dashboard concern).

**User stories:**
- "As an AI agent, I want to see the full dependency graph so that I can identify which blocker to resolve first for maximum impact."
- "As an AI agent, I want to know the critical path so that I can prioritize work that unblocks the most downstream items."

**Depends on:** New API endpoint with graph traversal logic. No schema changes.

---

### #10 — Recent Changes Feed

**Category:** C. Visibility, History & Audit
**Benefits:** AI agents resuming a project after interruption need to know what changed since they last worked. Currently, there's no way to ask "what happened since timestamp X?" — the agent must re-read everything and compare with its memory, or rely on the activity log which only shows HTTP requests without semantic meaning.
**Value:** Medium

**Current state:**
The `get_activity_logs` tool shows raw HTTP requests (method, path, status code, duration) — this tells an agent that `PATCH /api/projects/1/issues/3` was called, but not what changed. The issue audit log has semantic change information but isn't exposed via MCP and is per-entity only.

An agent returning to a project must call `get_project_overview` (expensive) and diff against its memory of the previous state — if it even has that memory across sessions.

**Proposed feature:**
A `get_recent_changes` MCP tool that returns a chronological feed of meaningful state changes across a project since a given timestamp:
- Entity created/updated/deleted events
- State transitions with old→new values
- Dependency additions/removals
- Cascade events (auto-block, auto-unblock, auto-complete)

Accepts: `projectId`, `since` (ISO timestamp), `limit` (default 50).
Returns compact events: timestamp, entity ID, entity type, change type, summary.

Scope in: All entity types, state changes, dependency changes, creation events.
Scope out: Field-level diffs on every update (too verbose — agent can use audit tools for specific entities).

**User stories:**
- "As an AI agent resuming a project session, I want to see what changed since my last interaction so that I can quickly get back up to speed."
- "As an AI agent, I want to know if any cascade events occurred from my last batch update so that I can verify the project is in the expected state."

**Depends on:** New API endpoint. Could leverage existing audit log tables (IssueAuditLog, WorkPackageAuditLog, etc.) plus the activity log for creation/deletion events. May need a unified query view.

---

### #11 — Move Task Between Phases

**Category:** A. Workflow Completion & Lifecycle Gaps
**Benefits:** AI agents refining implementation plans often need to reorganize tasks. Currently, moving a task from one phase to another requires deleting and recreating it — which loses the audit trail, any dependency relationships, and state timestamps.
**Value:** Medium

**Current state:**
Tasks belong to a phase and cannot be reassigned. If an agent creates a task in Phase 1 but later realizes it belongs in Phase 2, the only option is:
1. Note the task's details
2. Delete the task via dashboard (MCP has no delete tool)
3. Recreate the task in the correct phase
4. Re-add any dependencies

This loses: audit history, state timestamps (startedAt, completedAt), creation timestamp, and any file attachments. It also requires a human to perform the delete step since MCP tools can't delete.

**Proposed feature:**
A `move_task` MCP tool that reassigns a task to a different phase within the same work package. Accepts:
- `taskId` — the task to move
- `targetPhaseId` — the destination phase

Preserves all task data, audit history, dependencies, and timestamps. Updates the task's phase association atomically. Triggers upward cascade checks on both the source phase (might auto-complete if it was the last active task) and the target phase (might need to un-complete if it was terminal).

Scope in: Move within the same work package, preserve all data, cascade checks.
Scope out: Move across work packages (different numbering systems, more complex).

**User stories:**
- "As an AI agent reorganizing an implementation plan, I want to move a task to a different phase without losing its history and dependencies."
- "As an AI agent, I want phase completion status to automatically adjust when tasks move in or out."

**Depends on:** New API endpoint with phase reassignment logic. Needs careful handling of upward cascade on both source and target phases.

---

### #12 — Agent Session Context Snapshot

**Category:** G. Personalization, Preferences & Customization
**Benefits:** AI agents working across multiple sessions lose project context between conversations. A snapshot tool lets agents save and restore their working context — which entities they were focused on, what they planned to do next, and any notes — without relying on the host application to persist this.
**Value:** Medium

**Current state:**
When an AI agent's conversation ends, all project context is lost. The next session must start from scratch: call `get_project_overview`, re-read relevant entities, and rebuild understanding. There is no mechanism to record "I was working on WP-3, tasks 5-8 are next, and I noticed issue-7 might be a duplicate of issue-3."

The `OperationResult` `nextStep` field provides per-operation guidance but nothing persists across sessions.

**Proposed feature:**
Two MCP tools:
- `save_session_context` — accepts a project ID and a structured JSON blob containing: focused entity IDs, planned next actions, agent notes, and a session label. Stored as a new lightweight entity linked to the project.
- `get_session_context` — retrieves the most recent (or labeled) session context for a project.

The context is agent-written, agent-read. The system doesn't interpret it — it just stores and retrieves it. Think of it as a sticky note the agent leaves for its future self.

Scope in: Save/retrieve structured JSON context per project, support multiple labeled sessions.
Scope out: Auto-generating context (the agent decides what to save), sharing context between different agents.

**User stories:**
- "As an AI agent ending a session, I want to save my working context so that I can resume efficiently next time."
- "As an AI agent, I want to retrieve my previous session's notes so that I don't repeat analysis work."

**Depends on:** New entity (ProjectSessionContext or similar), migration, API endpoints, and MCP tools. Lightweight schema: project FK, label, JSON blob, timestamp.

---

---

## Final Summary

### Top 3 Highest-Value Features

1. **One-call work package scaffolding** (#1) ✅ — Let agents create a complete implementation plan (WP + phases + tasks + dependencies) in a single tool call instead of 4–10, dramatically reducing context window consumption and latency for the most common agent workflow.

2. **Compact project status summary** (#2) ✅ — Give agents a 500-token project health check instead of the current 3,000–5,000-token full overview, enabling efficient session starts and mid-session check-ins without blowing the context budget.

3. **Acceptance criteria verification** (#3) — Complete the quality gate workflow that the data model already supports but no tool activates, letting agents record pass/fail results and auto-complete phases when all criteria are met.

### Theme Analysis

The gaps concentrate in two areas:

**Token efficiency for common workflows.** The current toolset was designed for functional completeness — every entity can be created, read, and updated. But the tools were not optimized for the reality of AI agent context windows. The most frequent operations (starting a session, creating an implementation plan, checking progress) consume far more tokens than necessary because they return full entity objects when agents only need summaries or counts. Features #1, #2, #5, and #7 all address this theme.

**Incomplete lifecycle stages.** Several workflows start strong but end abruptly. Acceptance criteria can be defined but never verified (#3). Audit trails exist but are invisible to agents (#6). Tasks can be created in phases but never moved (#11). Session context is built up by agents but lost between conversations (#12). The data model often already supports the missing capability — it just needs a tool to activate it.

### Domain Comparison

For an AI-agent-oriented project management platform, PinkRooster has strong fundamentals: comprehensive entity model, automatic state cascades, dependency management with circular detection, and consistent API patterns. Compared to what mature AI-agent project management tools offer:

- **Entity management:** On par — full CRUD for all entity types with proper audit trails.
- **Batch operations:** Partial — task batch updates exist but no equivalent for issues or scaffolding.
- **Agent ergonomics:** Below expectations — no compact summaries, no search, no "what's next?" intelligence. These are table-stakes for context-window-constrained AI agents.
- **Workflow completeness:** Below expectations — the acceptance criteria verification gap is notable because the schema was designed for it but nothing activates it.
- **Session continuity:** Missing entirely — no mechanism for agents to persist working context across sessions, which is a fundamental need for AI agents that operate in discrete conversation turns.

The largest gap relative to market expectations is **agent ergonomics** — the tools work but aren't optimized for how AI agents actually operate (token-constrained, session-bounded, preference for server-side computation over client-side reasoning).

### Recommended Sequencing

**Phase 1 — Agent Ergonomics (highest standalone value, no dependencies)**
1. **#2 — Compact Project Status** — Immediately reduces token waste for every session start
2. **#1 — Work Package Scaffolding** — Immediately reduces calls for the most common write workflow
3. **#6 — Issue Audit Trail via MCP** — Lowest effort (endpoint already exists), fills a visible gap

**Phase 2 — Workflow Completion (builds on existing capabilities)**
4. **#3 — Acceptance Criteria Verification** — Completes the quality gate lifecycle
5. **#7 — Completion Readiness Check** — Pairs naturally with #3 (verify criteria → check readiness → complete)
6. **#5 — Priority-Ordered Next Actions** — Benefits from status infrastructure built in #2

**Phase 3 — Advanced Agent Intelligence (builds on Phase 1–2)**
7. **#4 — Cross-Entity Search** — New query infrastructure, independent but benefits from having more data to search
8. **#8 — Batch Issue State Updates** — Follows established pattern from task batch updates
9. **#9 — Dependency Graph View** — Advanced analysis for complex projects

**Phase 4 — Agent Session Management (standalone, build when needed)**
10. **#10 — Recent Changes Feed** — Useful once projects have enough history
11. **#11 — Move Task Between Phases** — Niche but important for plan refinement
12. **#12 — Agent Session Context** — New entity and schema; build when session continuity becomes a pain point
