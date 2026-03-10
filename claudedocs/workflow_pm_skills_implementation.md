# Implementation Workflow: PM Skills for Claude Code

**Source**: `claudedocs/DESIGN_pm_skills.md`
**Strategy**: Systematic with parallel waves
**Depth**: Deep
**Date**: 2026-03-10

---

## Execution Summary

| Metric | Value |
|--------|-------|
| Total skills | 7 |
| Total files to create | 9 (7 SKILL.md + 1 supporting + 1 directory structure) |
| Parallel waves | 4 |
| Estimated tasks | 26 |
| Dependencies | Pattern-based (Wave N+1 reuses patterns from Wave N) |

---

## Prerequisites

- [ ] Docker containers running (`make up` or `docker compose up -d`)
- [ ] MCP server healthy at `http://localhost:5200`
- [ ] Project registered in PinkRooster (has a `proj-N` ID)
- [ ] Design document reviewed: `claudedocs/DESIGN_pm_skills.md`
- [ ] Research reviewed: `claudedocs/research_claude_code_skills_authoring_20260310.md`

---

## Wave 1: Foundation (3 skills in parallel)

**Rationale**: These three skills are fully independent — no shared patterns to establish first. Wave 1 covers: read-only status, state transitions, and forked analysis.

### Wave 1A: `/pm-status` — Project Dashboard
**Pattern established**: A (Project Resolution via `!`pwd``)

| # | Task | Description | Files |
|---|------|-------------|-------|
| 1.1 | Create directory | `mkdir -p .claude/skills/pm-status` | — |
| 1.2 | Write SKILL.md | Frontmatter: name, description (auto-triggerable), argument-hint. Body: project resolution via `!`pwd``, get_project_status → get_next_actions chain, formatted dashboard output with health summary table, blocked items, next actions list. | `.claude/skills/pm-status/SKILL.md` |
| 1.3 | Smoke test | Invoke `/pm-status` and `/pm-status 5` (with limit arg). Verify: project resolves, dashboard renders, blocked items highlighted. Test error path: stop Docker, verify connection error message. | — |

**Key implementation details**:
- Use `!`pwd`` for dynamic project path injection
- Description must include keywords: "status", "progress", "what's happening", "attention"
- Format output as markdown tables for readability
- Handle project-not-found by suggesting `create_or_update_project`
- `$ARGUMENTS` defaults to 10 if not provided (limit for next actions)

---

### Wave 1B: `/pm-done` — Complete Tasks
**Pattern established**: C (State Transition + Cascade Reporting), D (Batch Completion)

| # | Task | Description | Files |
|---|------|-------------|-------|
| 2.1 | Create directory | `mkdir -p .claude/skills/pm-done` | — |
| 2.2 | Write SKILL.md | Frontmatter: name, description, disable-model-invocation, argument-hint. Body: three modes (single task ID, multiple task IDs, "all <wp-id>"). Cascade reporting template. Support for issues and FRs too. | `.claude/skills/pm-done/SKILL.md` |
| 2.3 | Smoke test | Test single task completion, batch completion, "all" mode. Verify cascade reporting: phase auto-complete, WP auto-complete, auto-unblock. Test with issue/FR completion. | — |

**Key implementation details**:
- Parse `$ARGUMENTS` to detect mode: single ID vs multiple IDs vs "all <wp-id>"
- Single task: `create_or_update_task(taskId, state: "Completed")`
- Multiple tasks: `batch_update_task_states(workPackageId, tasks)`
- "all" mode: `get_work_package_details` → filter non-terminal → confirm → batch update
- Cascade output section is the most important part — use bold formatting
- When auto-unblock detected, suggest: "Run `/pm-next` to start the unblocked item"
- For issues: prompt for `rootCause` and `resolution` if empty before completing

---

### Wave 1C: `/pm-triage` — Triage Issues/FRs
**Pattern established**: Forked Explore agent with MCP tools

| # | Task | Description | Files |
|---|------|-------------|-------|
| 3.1 | Create directory | `mkdir -p .claude/skills/pm-triage` | — |
| 3.2 | Write SKILL.md | Frontmatter: name, description, disable-model-invocation, context: fork, agent: Explore. Body: project resolution, load all active/inactive issues + FRs, age analysis, priority alignment check, triage report with three tiers + recommendations. | `.claude/skills/pm-triage/SKILL.md` |
| 3.3 | Smoke test | Invoke `/pm-triage`. Verify: runs in forked agent, produces structured report, read-only (no mutations), returns to main context with summary. | — |

**Key implementation details**:
- `context: fork` + `agent: Explore` — runs isolated
- Must use `!`pwd`` in content since forked agent loses conversation context
- Four MCP calls: issue overview (active + inactive) + feature requests (active + inactive)
- Age calculation: compare `createdAt` to current date
- Flag misalignment: Critical severity with Medium/Low priority
- Flag stale: NotStarted for >30 days
- Flag unlinked: Active issues/FRs without linked work packages
- Output: three-tier table (High Priority / Should Prioritize / Stale) + Recommendations
- Suggest PM skill follow-ups in recommendations

---

### Wave 1 Checkpoint

**Verify before proceeding to Wave 2**:
- [ ] `/pm-status` resolves project and shows dashboard
- [ ] `/pm-done <task-id>` completes a task and reports cascades
- [ ] `/pm-done all <wp-id>` batch-completes and reports cascades
- [ ] `/pm-triage` runs in forked context and returns a triage report
- [ ] All three skills appear in `/` autocomplete menu (except pm-triage which has disable-model-invocation)
- [ ] pm-status auto-triggers when asking "what's the project status?"

---

## Wave 2: Core Workflows (2 skills in parallel)

**Rationale**: Both depend on Pattern A from Wave 1. pm-next also reuses Pattern C from pm-done. pm-plan introduces entity creation patterns.

### Wave 2A: `/pm-next` — Start Next Priority Task
**Patterns reused**: A (Project Resolution), B (Task Context Loading), C (State Transition)

| # | Task | Description | Files |
|---|------|-------------|-------|
| 4.1 | Create directory | `mkdir -p .claude/skills/pm-next` | — |
| 4.2 | Write SKILL.md | Frontmatter: name, description, disable-model-invocation, argument-hint (entity-type filter). Body: project resolution, get_next_actions(limit: 5), present options, load context per entity type (WP details / issue / FR), read target files, transition to Implementing, begin work. | `.claude/skills/pm-next/SKILL.md` |
| 4.3 | Smoke test | Test with no args (all types), with "Task" filter, with "Issue" filter. Verify: presents top 5, loads full context, transitions state, starts implementation. Test blocked task detection. | — |

**Key implementation details**:
- `$ARGUMENTS` optionally filters by entity type: Task, Wp, Issue, FeatureRequest
- Present top items numbered, ask user to pick (or auto-pick #1 if only one)
- Context loading varies by type:
  - Task → extract WP ID from task ID, call `get_work_package_details`, find task in tree
  - Issue → `get_issue_details`
  - FR → `get_feature_request_details`
- Before starting work, read target files (if task) or affected component (if issue)
- Transition state BEFORE starting implementation:
  - Task: `create_or_update_task(taskId, state: "Implementing")`
  - Issue: `create_or_update_issue(projectId, issueId, state: "Implementing")`
- After implementation, suggest: "Run `/pm-done <id>` to mark complete"
- If picked item is blocked, report blockers and suggest picking a different item

---

### Wave 2B: `/pm-plan` — Plan Work from Description
**Patterns reused**: A (Project Resolution)
**New pattern**: Entity classification + creation

| # | Task | Description | Files |
|---|------|-------------|-------|
| 5.1 | Create directory | `mkdir -p .claude/skills/pm-plan` | — |
| 5.2 | Write SKILL.md | Frontmatter: name, description, disable-model-invocation, argument-hint. Body: classification decision tree (bug → Issue, feature → FR, ambiguous → ask), project resolution, entity creation with appropriate fields, optional scaffolding prompt, link WP to entity. | `.claude/skills/pm-plan/SKILL.md` |
| 5.3 | Smoke test | Test with bug description ("fix login crash"), feature description ("add CSV export"), ambiguous ("improve performance"). Verify: correct classification, entity created with proper fields, optional WP scaffolding. | — |

**Key implementation details**:
- `$ARGUMENTS` is the natural language description of work needed
- Classification keywords:
  - Issue (Bug): bug, fix, broken, error, crash, regression, failing
  - Issue (PerformanceIssue): performance, slow, timeout, latency, memory
  - Issue (SecurityVulnerability): security, vulnerability, CVE, injection, XSS
  - Issue (TechnicalDebt): refactor, cleanup, debt, deprecated
  - Feature Request: feature, add, new, enhance, improve, want, support, enable
  - Ambiguous: ask user to classify
- Always confirm classification before creating
- For FRs: derive `businessValue`, `userStory`, `category` from description
- For Issues: derive `issueType`, `severity`, `affectedComponent` from description
- After entity creation, ask: "Scaffold a work package with implementation tasks? (y/n)"
- If yes, analyze codebase and call `scaffold_work_package` with link to entity
- Report created IDs and suggest: "View at localhost:3000 or run `/pm-scaffold <id>` later"

---

### Wave 2 Checkpoint

**Verify before proceeding to Wave 3**:
- [ ] `/pm-next` picks up highest priority item and starts implementation
- [ ] `/pm-next Task` filters to tasks only
- [ ] State transitions work (Implementing on start)
- [ ] `/pm-plan "fix the login crash"` creates an Issue (Bug)
- [ ] `/pm-plan "add CSV export feature"` creates a Feature Request
- [ ] Optional WP scaffolding works when user accepts
- [ ] Entity IDs are reported and can be used with other skills

---

## Wave 3: Advanced Workflows (2 skills in parallel)

**Rationale**: pm-implement reuses Patterns A-C plus deep code analysis. pm-scaffold reuses Pattern A and introduces Pattern E (codebase-aware scaffolding). Both are independent of each other.

### Wave 3A: `/pm-implement` — Implement Specific Task
**Patterns reused**: A, B, C + deep code analysis

| # | Task | Description | Files |
|---|------|-------------|-------|
| 6.1 | Create directory | `mkdir -p .claude/skills/pm-implement` | — |
| 6.2 | Write SKILL.md | Frontmatter: name, description, disable-model-invocation, argument-hint (task-id). Body: parse task ID, extract WP ID, load WP details, find task + phase + acceptance criteria, check dependencies (if blocked → redirect), read target files, analyze code with Serena, transition to Implementing, implement following implementationNotes, run tests, summarize changes, suggest /pm-done. | `.claude/skills/pm-implement/SKILL.md` |
| 6.3 | Smoke test | Test with valid task ID. Verify: loads full context, reads target files, transitions state, implements code, runs tests, does NOT auto-complete. Test with blocked task (should redirect). Test with invalid ID (error message with format hint). | — |

**Key implementation details**:
- `$ARGUMENTS` is a task ID in format `proj-N-wp-N-task-N`
- Extract WP ID by trimming the task suffix: `proj-N-wp-N`
- Full context loading:
  1. `get_work_package_details(wpId)` → full WP tree
  2. Find the task in `phases[].tasks[]` by matching task ID
  3. Extract: task.description, task.implementationNotes, task.targetFiles
  4. Extract: phase.acceptanceCriteria, wp.plan (for broader context)
- Dependency check: if task.blockedBy is non-empty and any blocker is non-terminal → report and suggest implementing the blocker first
- Code analysis:
  1. Read each file in task.targetFiles
  2. Use Serena `get_symbols_overview` for structural understanding
  3. Use `find_symbol` / `find_referencing_symbols` for specific code paths
- Implementation: follow implementationNotes, respect CLAUDE.md conventions
- Test execution: `dotnet test` for .NET, `npm test` for dashboard
- Summary: list files changed, tests run, what was implemented
- NEVER auto-complete — always end with: "Run `/pm-done <task-id>` to mark complete"

---

### Wave 3B: `/pm-scaffold` — Scaffold Work Package
**Patterns reused**: A + new Pattern E (Codebase-Aware Scaffolding)

| # | Task | Description | Files |
|---|------|-------------|-------|
| 7.1 | Create directory | `mkdir -p .claude/skills/pm-scaffold` | — |
| 7.2 | Write SKILL.md | Frontmatter: name, description, disable-model-invocation, argument-hint. Body: project resolution, detect if argument is entity ID or description, load entity details if ID, analyze codebase to identify affected layers, design phases following vertical slice pattern (see scaffold-patterns.md), populate tasks with targetFiles and implementationNotes, call scaffold_work_package, link to issue/FR if applicable, report entity map. | `.claude/skills/pm-scaffold/SKILL.md` |
| 7.3 | Write scaffold-patterns.md | Supporting file with PinkRooster's vertical slice pattern: Shared+Data → API → MCP → Dashboard → Testing. Include file path templates per layer. | `.claude/skills/pm-scaffold/scaffold-patterns.md` |
| 7.4 | Smoke test | Test with free-text description, with issue ID, with FR ID. Verify: codebase analyzed, realistic targetFiles produced, phases follow vertical slice, dependencies set, entity linked. | — |

**Key implementation details for SKILL.md**:
- `$ARGUMENTS` can be:
  1. Entity ID (`proj-N-issue-N` or `proj-N-fr-N`) — load details for requirements
  2. Free-text description — use as WP description
- Codebase analysis phase:
  1. Read `CLAUDE.md` for architecture overview (already in context, but reference it)
  2. Use Grep/Glob to find related existing files
  3. Use Serena `get_symbols_overview` on relevant directories
  4. Map which layers need changes based on the feature scope
- Reference `scaffold-patterns.md` for phase structure
- For each task, derive:
  - `targetFiles`: actual file paths found via codebase analysis
  - `implementationNotes`: specific guidance based on existing patterns
  - `dependsOnTaskIndices`: intra-phase ordering (e.g., entity before migration before DTO)
- Set `estimatedComplexity` (1-10) with rationale
- If scaffolding from FR: update FR status to `Scheduled` via `create_or_update_feature_request`
- Report: WP ID, phase count, task count, dependency count, entity map

**Key implementation details for scaffold-patterns.md**:
```
## Vertical Slice Pattern
This project follows a strict vertical slice architecture. When scaffolding
a new feature, phases follow this order:

### Phase 1: Shared + Data Layer
Files: src/PinkRooster.Shared/*, src/PinkRooster.Data/*
Tasks: Entity class, migration, DTOs (request + response), enum values

### Phase 2: API Layer
Files: src/PinkRooster.Api/*
Tasks: Service interface + impl, Controller, DI registration, middleware

### Phase 3: MCP Layer
Files: src/PinkRooster.Mcp/*
Tasks: Tool class, Input types, Response types, API client methods

### Phase 4: Dashboard
Files: src/dashboard/src/*
Tasks: API client, hooks, list page, detail page, create page

### Phase 5: Integration Testing
Files: tests/PinkRooster.Api.Tests/*, src/dashboard/src/test/*
Tasks: API integration tests, dashboard unit tests, MSW handlers
```

---

### Wave 3 Checkpoint

**Verify before proceeding to Wave 4**:
- [ ] `/pm-implement proj-1-wp-N-task-N` loads context and implements
- [ ] Blocked tasks are detected and alternative suggested
- [ ] Tests run after implementation
- [ ] `/pm-scaffold "add activity log export"` produces realistic WP
- [ ] `/pm-scaffold proj-1-issue-N` loads issue and scaffolds linked WP
- [ ] scaffold-patterns.md is referenced and phases follow vertical slice
- [ ] targetFiles reference real paths in the codebase

---

## Wave 4: Integration & Validation

**Rationale**: All skills exist. Verify end-to-end workflows, cross-skill integration, and context budget.

| # | Task | Description |
|---|------|-------------|
| 8.1 | End-to-end: Plan → Scaffold → Implement → Done | Run `/pm-plan "add audit log export feature"` → accept scaffolding → `/pm-implement <task-id>` → `/pm-done <task-id>`. Verify full lifecycle. |
| 8.2 | End-to-end: Status → Next → Done | Run `/pm-status` → `/pm-next` → implement → `/pm-done`. Verify priority ordering drives work selection. |
| 8.3 | End-to-end: Triage → Plan → Scaffold | Run `/pm-triage` → follow recommendation → `/pm-plan` → `/pm-scaffold`. Verify triage feeds planning. |
| 8.4 | Context budget check | Run `/context` to verify skill descriptions fit within budget. Check: only pm-status description is in context (others have disable-model-invocation). |
| 8.5 | Error path validation | Test each skill with: invalid IDs, Docker down, empty project (no issues/WPs). Verify graceful error messages. |
| 8.6 | Cross-skill suggestions | Verify each skill suggests the logical next skill after completion (per UX Guidelines in design doc). |

---

## Wave 4 Checkpoint (Final)

- [ ] Full plan→scaffold→implement→done lifecycle works
- [ ] Full status→next→done lifecycle works
- [ ] Triage recommendations are actionable via other PM skills
- [ ] Context budget is within limits (run `/context`)
- [ ] All error paths produce helpful messages
- [ ] Each skill suggests the next logical skill

---

## Parallel Execution Map

```
Wave 1 (Foundation)        Wave 2 (Core)         Wave 3 (Advanced)      Wave 4 (Validation)
─────────────────          ─────────────          ─────────────────      ──────────────────
┌─────────────┐
│ pm-status   │─────┐
│ (Pattern A) │     │    ┌─────────────┐
└─────────────┘     ├───→│ pm-next     │─────┐
                    │    │ (A+B+C)     │     │    ┌─────────────┐
┌─────────────┐     │    └─────────────┘     ├───→│ pm-implement│
│ pm-done     │─────┤                        │    │ (A+B+C+code)│    ┌──────────────┐
│ (Pattern CD)│     │    ┌─────────────┐     │    └─────────────┘    │ Integration  │
└─────────────┘     ├───→│ pm-plan     │     │                   ───→│ & Validation │
                    │    │ (A+create)  │     │    ┌─────────────┐    │ (end-to-end) │
┌─────────────┐     │    └─────────────┘     ├───→│ pm-scaffold │    └──────────────┘
│ pm-triage   │─────┘                        │    │ (A+E+support│
│ (forked)    │                              │    └─────────────┘
└─────────────┘                              │
                                             │
         ◄── can run in parallel ──►         │
```

---

## File Creation Manifest

| # | File | Wave | Skill | Lines (est.) |
|---|------|------|-------|-------------|
| 1 | `.claude/skills/pm-status/SKILL.md` | 1A | pm-status | ~80 |
| 2 | `.claude/skills/pm-done/SKILL.md` | 1B | pm-done | ~120 |
| 3 | `.claude/skills/pm-triage/SKILL.md` | 1C | pm-triage | ~100 |
| 4 | `.claude/skills/pm-next/SKILL.md` | 2A | pm-next | ~120 |
| 5 | `.claude/skills/pm-plan/SKILL.md` | 2B | pm-plan | ~130 |
| 6 | `.claude/skills/pm-implement/SKILL.md` | 3A | pm-implement | ~140 |
| 7 | `.claude/skills/pm-scaffold/SKILL.md` | 3B | pm-scaffold | ~130 |
| 8 | `.claude/skills/pm-scaffold/scaffold-patterns.md` | 3B | pm-scaffold | ~60 |

**Total**: ~880 lines across 8 files, all under the 500-line SKILL.md limit.

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Context budget exceeded by skill descriptions | Only pm-status is auto-triggerable; 6/7 skills use `disable-model-invocation: true` (descriptions not loaded) |
| MCP tools unavailable (Docker down) | Every skill detects connection errors and suggests `make up` |
| `!`pwd`` doesn't resolve correctly | Test in both project root and subdirectories |
| Forked agent (pm-triage) can't access MCP tools | Verify MCP tools are available to forked Explore agents |
| Skill names conflict with SuperClaude | `pm-` prefix is unique; no `/sc:pm-*` commands exist |
| Cascade reporting is noisy | Skills format cascades with clear hierarchy and only show when present |

---

## Next Step

Run `/sc:implement` to execute this workflow, starting with Wave 1 (three skills in parallel).
