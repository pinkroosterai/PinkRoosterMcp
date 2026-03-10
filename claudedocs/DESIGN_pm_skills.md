# Design: Project Management Skills for Claude Code

**Date**: 2026-03-10
**Status**: Proposed
**Scope**: `.claude/skills/pm-*` — project-scoped skills committed to version control

---

## 1. Problem Statement

The PinkRooster MCP server provides 18 project management tools, but using them effectively requires knowing the right tool chains, parameter formats, and orchestration patterns. Today, every session starts from scratch — the user must manually invoke `get_project_status`, interpret the output, decide what to work on, look up task details, implement code, then remember to update states.

**Goal**: Create a set of Claude Code skills that orchestrate PinkRooster MCP tools alongside code analysis and editing tools, providing end-to-end workflows for managing and implementing projects.

---

## 2. Design Principles

1. **Chain, don't wrap** — Skills orchestrate multi-step tool chains (MCP → code analysis → edit → MCP), not thin wrappers around single tools.
2. **Complement SuperClaude** — The existing `/sc:*` plugin handles general dev workflows (research, design, test). PM skills manage **project state** through PinkRooster and bridge to code implementation.
3. **Fewer, more powerful** — 7 focused skills covering the full lifecycle, not 15 micro-skills.
4. **Inline by default** — Most skills need conversation context to be useful. Only fork for pure analysis.
5. **State-aware** — Skills that modify entities always report cascade state changes back to the user.
6. **Project path is known** — All skills use `!`pwd`` to inject the current working directory, eliminating the need to pass `projectPath` manually.

---

## 3. Skill Inventory

| # | Skill | Purpose | Trigger | Execution |
|---|-------|---------|---------|-----------|
| 1 | `/pm-status` | Project dashboard with actionable summary | Auto + manual | Inline |
| 2 | `/pm-plan` | Create feature request/issue + scaffold WP | Manual only | Inline |
| 3 | `/pm-next` | Get and start highest-priority task | Manual only | Inline |
| 4 | `/pm-implement` | Implement a specific task by ID | Manual only | Inline |
| 5 | `/pm-done` | Complete tasks, report cascades | Manual only | Inline |
| 6 | `/pm-scaffold` | Scaffold WP with codebase-aware phases/tasks | Manual only | Inline |
| 7 | `/pm-triage` | Review and prioritize open issues/FRs | Manual only | Forked (Explore) |

### 3.1 Interaction with SuperClaude

| PM Skill | Complements | Distinction |
|----------|-------------|-------------|
| `/pm-plan` | `/sc:design` | PM creates tracking entities; design does architecture |
| `/pm-implement` | `/sc:implement` | PM reads task context from PinkRooster then implements; sc:implement works from conversation |
| `/pm-triage` | `/sc:analyze` | PM triages project management entities; analyze reviews code quality |
| `/pm-scaffold` | `/sc:workflow` | PM creates PinkRooster WPs; workflow creates abstract plans |

---

## 4. Detailed Skill Specifications

### 4.1 `/pm-status` — Project Dashboard

**File**: `.claude/skills/pm-status/SKILL.md`

**Intent**: Quickly understand project health, what's active, what's blocked, and what needs attention next.

**Frontmatter**:
```yaml
name: pm-status
description: >-
  Show project status dashboard with issue/FR/WP counts, active work items,
  blocked items, and priority next actions. Use when the user asks about
  project status, progress, what's happening, or what needs attention.
argument-hint: [limit]
```

**Tool chain**:
1. `mcp__pinkrooster__get_project_status(projectPath: !`pwd`)` — Get counts + item lists
2. `mcp__pinkrooster__get_next_actions(projectId, limit: $ARGUMENTS or 10)` — Priority queue
3. Format as structured dashboard

**Output format**:
```
## Project: PinkRoosterMcp (proj-1)

### Health Summary
| Entity | Active | Blocked | Completed | Total |
|--------|--------|---------|-----------|-------|
| Issues | 3 | 1 | 12 | 16 |
| FRs | 2 | 0 | 5 | 7 |
| WPs | 4 | 2 | 8 | 14 |

### Blocked Items (need attention)
- **proj-1-wp-5** "Dashboard CRUD" — blocked by proj-1-wp-4
- **proj-1-issue-8** "Login timeout" — Blocked state

### Next Actions (top 5)
1. [Critical] proj-1-wp-3-task-7 "Add validation middleware" (Implementing)
2. [High] proj-1-issue-4 "Fix cascade bug" (Designing)
3. [Medium] proj-1-fr-2 "Export to CSV" (Approved, no linked WP)
...
```

**Key behaviors**:
- Auto-triggerable when user asks about status/progress
- If project not found, prompt to register via `create_or_update_project`
- Highlight blocked items prominently — they represent bottlenecks
- Show FRs without linked WPs as planning opportunities

---

### 4.2 `/pm-plan` — Plan Work from Description

**File**: `.claude/skills/pm-plan/SKILL.md`

**Intent**: Take a natural language description of needed work and create the appropriate tracking entities (issue or feature request), then optionally scaffold a work package.

**Frontmatter**:
```yaml
name: pm-plan
description: >-
  Plan new work by creating an issue or feature request and optionally
  scaffolding a work package with phases and tasks. Takes a description
  of what needs to be done.
disable-model-invocation: true
argument-hint: <description of work>
```

**Tool chain**:
1. Classify: Is this a bug/defect (→ Issue) or enhancement/feature (→ Feature Request)?
2. `mcp__pinkrooster__get_project_status(projectPath: !`pwd`)` — Get projectId
3. Create entity:
   - Bug → `mcp__pinkrooster__create_or_update_issue(projectId, name, description, issueType, severity)`
   - Feature → `mcp__pinkrooster__create_or_update_feature_request(projectId, name, description, category)`
4. Ask user: "Scaffold a work package with implementation tasks?"
5. If yes → analyze codebase, then `mcp__pinkrooster__scaffold_work_package(...)` with phases/tasks
6. Link WP to the issue/FR

**Decision tree for classification**:
```
$ARGUMENTS contains:
  bug, fix, broken, error, crash, regression → Issue (Bug)
  performance, slow, timeout → Issue (PerformanceIssue)
  security, vulnerability, CVE → Issue (SecurityVulnerability)
  refactor, cleanup, debt → Issue (TechnicalDebt)
  feature, add, new, enhance, improve, want → Feature Request
  ambiguous → Ask user
```

**Key behaviors**:
- Always confirm the classification before creating entities
- For feature requests, derive `businessValue` and `userStory` from description
- When scaffolding, analyze the codebase to produce realistic `targetFiles` for tasks
- Set appropriate `priority` based on severity/urgency signals in the description
- Report created entity IDs so user can reference them later

---

### 4.3 `/pm-next` — Start Next Priority Task

**File**: `.claude/skills/pm-next/SKILL.md`

**Intent**: Get the highest-priority actionable item, load its context, transition it to active state, and begin working on it.

**Frontmatter**:
```yaml
name: pm-next
description: >-
  Pick up the next highest-priority task and start implementing it.
  Fetches task details, reads relevant code, transitions to Implementing,
  and begins the work.
disable-model-invocation: true
argument-hint: [entity-type-filter]
```

**Tool chain**:
1. `mcp__pinkrooster__get_project_status(projectPath: !`pwd`)` — Get projectId
2. `mcp__pinkrooster__get_next_actions(projectId, limit: 5, entityType: $ARGUMENTS or null)` — Get top items
3. Present top items to user, ask which to start (or auto-pick #1)
4. Load context:
   - If task → `mcp__pinkrooster__get_work_package_details(wpId)` for full WP context
   - If issue → `mcp__pinkrooster__get_issue_details(issueId)`
   - If FR → `mcp__pinkrooster__get_feature_request_details(frId)`
5. Read target files and related code using Serena/Read tools
6. Transition state:
   - Task → `mcp__pinkrooster__create_or_update_task(taskId, state: "Implementing")`
   - Issue → `mcp__pinkrooster__create_or_update_issue(projectId, issueId, state: "Implementing")`
7. Begin implementation, referencing task description + implementation notes

**Key behaviors**:
- Always show the user what was picked and why before starting
- Load the full WP tree so Claude understands how this task fits into the bigger picture
- Read target files to understand existing code before modifying
- If the task has dependencies, verify all blockers are complete first
- After implementing, prompt user to run `/pm-done` to mark complete

---

### 4.4 `/pm-implement` — Implement Specific Task

**File**: `.claude/skills/pm-implement/SKILL.md`

**Intent**: Given a specific task ID, load its full context, understand the requirements, implement the code changes, run tests, and update state.

**Frontmatter**:
```yaml
name: pm-implement
description: >-
  Implement a specific task by ID. Reads task details and WP context,
  analyzes target files, implements code changes, runs tests, and
  updates task state.
disable-model-invocation: true
argument-hint: <task-id e.g. proj-1-wp-2-task-5>
```

**Tool chain**:
1. Parse task ID from `$ARGUMENTS`
2. `mcp__pinkrooster__get_work_package_details(wpId)` — Full WP tree for context
3. Extract the specific task + its phase + acceptance criteria
4. Check dependencies: Are all blocking tasks complete?
5. If blocked → report and suggest working on the blocker instead
6. Read target files listed in the task
7. Analyze surrounding code with Serena (`get_symbols_overview`, `find_symbol`)
8. `mcp__pinkrooster__create_or_update_task(taskId, state: "Implementing")` — Mark started
9. Implement the changes following `implementationNotes`
10. Run relevant tests (`dotnet test` or `npm test`)
11. If tests pass → prompt for `/pm-done`
12. If tests fail → fix and retry

**Key behaviors**:
- Extract the WP-level `plan` and phase `acceptanceCriteria` for full context
- Use task's `targetFiles` as the primary entry points for code analysis
- Follow the project's coding conventions from CLAUDE.md
- After implementation, summarize what changed and what was tested
- Never auto-complete — always let the user confirm via `/pm-done`

---

### 4.5 `/pm-done` — Complete Tasks

**File**: `.claude/skills/pm-done/SKILL.md`

**Intent**: Mark one or more tasks as completed and report all cascading state changes (phase auto-complete, WP auto-complete, dependent unblocking).

**Frontmatter**:
```yaml
name: pm-done
description: >-
  Mark tasks as completed and report cascading state changes.
  Accepts task IDs or "all" for the current work package.
  Reports phase/WP auto-completion and dependent unblocking.
disable-model-invocation: true
argument-hint: <task-id...> | all <wp-id>
```

**Tool chain (single task)**:
1. Parse task ID from `$ARGUMENTS`
2. `mcp__pinkrooster__create_or_update_task(taskId, state: "Completed")`
3. Report state changes from response

**Tool chain (multiple tasks)**:
1. Parse work package ID and task IDs from `$ARGUMENTS`
2. `mcp__pinkrooster__batch_update_task_states(workPackageId, tasks: [{taskId, state: "Completed"}, ...])`
3. Report consolidated state changes

**Tool chain ("all" mode)**:
1. `mcp__pinkrooster__get_work_package_details(wpId)` — Get all tasks
2. Filter to non-terminal tasks
3. Confirm with user: "Mark N tasks as Completed?"
4. `mcp__pinkrooster__batch_update_task_states(...)` with all task IDs
5. Report cascades

**Output format**:
```
## Completed

- proj-1-wp-2-task-5 "Add validation middleware" → Completed

## Cascading State Changes

- **Phase auto-complete**: proj-1-wp-2-phase-2 "Implementation" → Completed
  (all 4 tasks now terminal)
- **WP auto-complete**: proj-1-wp-2 "Validation Feature" → Completed
  (all 3 phases now terminal)
- **Auto-unblock**: proj-1-wp-3 "Dashboard Integration" → Implementing
  (blocker proj-1-wp-2 completed, restored from previous active state)
```

**Key behaviors**:
- Always report cascading changes prominently — they're the most important output
- When auto-unblock happens, suggest working on the unblocked item next
- Support completing issues and FRs too, not just tasks:
  - Issue: `create_or_update_issue(projectId, issueId, state: "Completed")`
  - FR: `create_or_update_feature_request(projectId, frId, status: "Completed")`
- If completing an issue, prompt to fill `rootCause` and `resolution` if empty

---

### 4.6 `/pm-scaffold` — Scaffold Work Package

**File**: `.claude/skills/pm-scaffold/SKILL.md`
**Supporting files**: `.claude/skills/pm-scaffold/scaffold-patterns.md`

**Intent**: Analyze requirements and the codebase to create a detailed work package with realistic phases, tasks, dependencies, and target files.

**Frontmatter**:
```yaml
name: pm-scaffold
description: >-
  Scaffold a complete work package with phases, tasks, and dependencies
  based on a feature description. Analyzes the codebase to produce
  realistic target files and implementation notes for each task.
disable-model-invocation: true
argument-hint: <description or issue-id or fr-id>
```

**Tool chain**:
1. `mcp__pinkrooster__get_project_status(projectPath: !`pwd`)` — Get projectId
2. If `$ARGUMENTS` is an entity ID, load its details for requirements
3. Analyze codebase architecture:
   - Use Serena `get_symbols_overview` on relevant modules
   - Use Grep/Glob to find related files
   - Identify the layers that need changes (API, Data, Shared, MCP, Dashboard)
4. Design WP structure following project patterns:
   - Phase 1: Data Layer (entities, migrations, DTOs)
   - Phase 2: API Layer (service, controller, tests)
   - Phase 3: MCP Layer (tools, inputs, responses)
   - Phase 4: Dashboard (pages, hooks, API client)
   - Phase 5: Integration Testing
5. For each task:
   - Name and description
   - `targetFiles` with specific file paths found in codebase analysis
   - `implementationNotes` with concrete guidance
   - `dependsOnTaskIndices` for intra-phase ordering
6. `mcp__pinkrooster__scaffold_work_package(projectId, name, description, phases, ...)`
7. If requirements came from an issue/FR, link it: `linkedIssueId` or `linkedFeatureRequestId`
8. Report created entity map

**Scaffold patterns** (in `scaffold-patterns.md`):

```
## Vertical Slice Pattern (this project's standard)
Phase 1: Shared + Data
  - Add/modify entity
  - Create migration
  - Add DTOs
Phase 2: API
  - Service interface + implementation
  - Controller endpoints
  - Register DI
Phase 3: MCP
  - Tool class
  - Input types
  - Response mapping
Phase 4: Dashboard
  - API client functions
  - React hooks
  - Pages (list + detail + create)
Phase 5: Testing
  - API integration tests
  - Dashboard unit tests
```

**Key behaviors**:
- Always analyze the codebase before scaffolding — don't guess at file paths
- Follow the existing vertical slice pattern visible in the codebase
- Set realistic `estimatedComplexity` (1-10) with rationale
- Add cross-phase dependencies where later phases depend on earlier ones
- If scaffolding from an FR, set FR status to `Scheduled`

---

### 4.7 `/pm-triage` — Triage Issues and Feature Requests

**File**: `.claude/skills/pm-triage/SKILL.md`

**Intent**: Review all open issues and feature requests, assess their current state, suggest priority adjustments, and recommend next steps.

**Frontmatter**:
```yaml
name: pm-triage
description: >-
  Review and prioritize open issues and feature requests.
  Analyzes severity, age, and codebase impact to recommend
  priority adjustments and next steps.
disable-model-invocation: true
context: fork
agent: Explore
```

**Tool chain** (runs in forked Explore agent):
1. `mcp__pinkrooster__get_project_status(projectPath: !`pwd`)` — Get projectId
2. `mcp__pinkrooster__get_issue_overview(projectId, stateFilter: "active")` — Active issues
3. `mcp__pinkrooster__get_issue_overview(projectId, stateFilter: "inactive")` — Inactive issues
4. `mcp__pinkrooster__get_feature_requests(projectId, stateFilter: "active")` — Active FRs
5. `mcp__pinkrooster__get_feature_requests(projectId, stateFilter: "inactive")` — Inactive FRs
6. For each item, analyze:
   - Age (created date → now)
   - Priority vs severity alignment
   - Whether it has a linked work package
   - Related code health (Grep for affected components)
7. Format triage report with recommendations

**Output format**:
```
## Triage Report

### High Priority (act now)
| ID | Name | Age | Priority | Issue |
|----|------|-----|----------|-------|
| proj-1-issue-4 | Fix cascade bug | 5d | Critical | No linked WP — needs scaffolding |

### Should Prioritize
| ID | Name | Age | Priority | Issue |
|----|------|-----|----------|-------|
| proj-1-fr-2 | Export to CSV | 12d | Medium | Approved but no WP — consider scheduling |

### Stale (consider closing)
| ID | Name | Age | Priority | Issue |
|----|------|-----|----------|-------|
| proj-1-issue-1 | Old formatting bug | 45d | Low | NotStarted for 45 days — still relevant? |

### Recommendations
1. Scaffold WP for proj-1-issue-4 (Critical, 5 days old, no WP)
2. Update proj-1-fr-2 to Scheduled and link to upcoming WP
3. Review proj-1-issue-1 — close or reprioritize
```

**Key behaviors**:
- Runs in forked Explore agent for isolated analysis (doesn't pollute main context)
- Read-only — never modifies entities directly
- Returns actionable recommendations the user can execute with other PM skills
- Flags priority/severity misalignment (e.g., Critical severity but Low priority)

---

## 5. Directory Structure

```
.claude/
└── skills/
    ├── pm-status/
    │   └── SKILL.md
    ├── pm-plan/
    │   └── SKILL.md
    ├── pm-next/
    │   └── SKILL.md
    ├── pm-implement/
    │   └── SKILL.md
    ├── pm-done/
    │   └── SKILL.md
    ├── pm-scaffold/
    │   ├── SKILL.md
    │   └── scaffold-patterns.md
    └── pm-triage/
        └── SKILL.md
```

---

## 6. Tool Access Matrix

| Skill | PinkRooster MCP Tools | Serena | Standard Tools | Bash |
|-------|----------------------|--------|----------------|------|
| pm-status | get_project_status, get_next_actions | — | — | `pwd` |
| pm-plan | get_project_status, create_or_update_issue, create_or_update_feature_request, scaffold_work_package | get_symbols_overview, find_symbol | Read, Grep, Glob | `pwd` |
| pm-next | get_project_status, get_next_actions, get_work_package_details, get_issue_details, get_feature_request_details, create_or_update_task, create_or_update_issue | get_symbols_overview, find_symbol | Read, Grep, Glob, Edit, Write | `pwd`, `dotnet test`, `npm test` |
| pm-implement | get_work_package_details, create_or_update_task | get_symbols_overview, find_symbol, find_referencing_symbols | Read, Grep, Glob, Edit, Write | `dotnet test`, `npm test`, `dotnet build` |
| pm-done | get_work_package_details, create_or_update_task, batch_update_task_states, create_or_update_issue, create_or_update_feature_request | — | — | — |
| pm-scaffold | get_project_status, get_issue_details, get_feature_request_details, scaffold_work_package, create_or_update_feature_request | get_symbols_overview | Read, Grep, Glob | `pwd` |
| pm-triage | get_project_status, get_issue_overview, get_feature_requests, get_issue_details, get_feature_request_details | — | Grep | `pwd` |

---

## 7. MCP Tool Chain Patterns

These are the recurring tool chain patterns skills should follow:

### Pattern A: Project Resolution (used by all skills)
```
get_project_status(projectPath: <cwd>)
  → extracts projectId
  → if not found: prompt create_or_update_project
```

### Pattern B: Task Context Loading
```
get_work_package_details(wpId)
  → extract task from phases[].tasks[]
  → read task.targetFiles via standard Read/Serena
  → understand WP.plan + phase.acceptanceCriteria for context
```

### Pattern C: Entity State Transition
```
create_or_update_task(taskId, state: newState)
  → response includes stateChanges[]
  → report each stateChange to user
  → if auto-unblock occurred, suggest next work item
```

### Pattern D: Batch Completion
```
get_work_package_details(wpId)
  → filter tasks where state is non-terminal
  → batch_update_task_states(wpId, tasks)
  → report consolidated cascades
```

### Pattern E: Codebase-Aware Scaffolding
```
Analyze codebase (Serena/Grep/Glob)
  → identify affected layers
  → determine file paths per layer
  → map dependencies between layers
  → scaffold_work_package(projectId, name, description, phases[
      { name, tasks[{ name, description, targetFiles, implementationNotes, dependsOnTaskIndices }] }
    ])
```

---

## 8. Error Handling Strategy

All skills must handle these MCP error scenarios:

| Error | Handling |
|-------|----------|
| Project not found | Offer to register: `create_or_update_project(name, description, projectPath)` |
| Entity not found | Report with suggestion (check ID format, list alternatives) |
| Invalid ID format | Report format hint (e.g., "Expected 'proj-{N}-wp-{N}-task-{N}'") |
| Circular dependency | Report and suggest alternative dependency structure |
| Docker containers down | Detect connection refused, suggest `make up` or `docker compose up -d` |
| API health check failing | Suggest waiting 15s then retry, or check `docker compose ps` |

---

## 9. UX Guidelines

1. **Always show entity IDs** — Users need IDs to reference entities in follow-up commands
2. **Report cascades prominently** — State changes affecting other entities are the most important output
3. **Confirm before writes** — Ask before creating/modifying entities (except state transitions which are explicit)
4. **Link back to dashboard** — After creating entities, mention they're viewable at `localhost:3000`
5. **Suggest next skill** — After completing a workflow, suggest the logical next step:
   - After `/pm-plan` → "Run `/pm-scaffold` to create implementation tasks"
   - After `/pm-implement` → "Run `/pm-done proj-1-wp-2-task-5` to mark complete"
   - After `/pm-done` with unblocked items → "proj-1-wp-3 was unblocked. Run `/pm-next` to start it"
   - After `/pm-triage` → "Run `/pm-scaffold proj-1-issue-4` to create a WP for the top priority"

---

## 10. Implementation Sequence

Skills should be implemented in this order (each builds on patterns established by the previous):

1. **`/pm-status`** — Establishes project resolution pattern (Pattern A)
2. **`/pm-done`** — Establishes state transition + cascade reporting (Pattern C, D)
3. **`/pm-next`** — Combines Pattern A + B + C
4. **`/pm-implement`** — Full implementation workflow with Pattern B + code tools
5. **`/pm-plan`** — Entity creation workflows
6. **`/pm-scaffold`** — Most complex: codebase analysis + scaffolding (Pattern E)
7. **`/pm-triage`** — Forked analysis skill (independent of the others)

---

## 11. Testing Strategy

Skills don't have unit tests per se. Validation approach:

1. **Smoke test each skill** with the actual PinkRooster project (`make up` required)
2. **Verify tool chains** by running each skill and confirming all MCP calls succeed
3. **Test error paths** by invoking with bad IDs, when Docker is down, etc.
4. **Verify cascade reporting** by completing tasks that trigger auto-complete/auto-unblock
5. **Test argument handling** — skills with `$ARGUMENTS` should handle: no args, single arg, multiple args

---

## 12. Future Considerations

- **Skill hooks**: Add pre/post hooks for logging skill invocations to PinkRooster activity log
- **Cross-project support**: Current design assumes single project at `pwd`. Could extend to multi-project with `$1` as project path
- **Integration with `feature-discovery` agent**: The existing custom agent could feed into `/pm-plan` for automated feature tracking
- **Dashboard deep links**: Generate clickable URLs to specific dashboard pages when reporting entities
- **Batch orchestration**: A `/pm-sprint` skill that picks up multiple tasks and implements them sequentially with state tracking
