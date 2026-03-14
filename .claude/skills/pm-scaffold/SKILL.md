---
name: pm-scaffold
description: >-
  Scaffold a complete work package with phases, tasks, and dependencies
  based on a feature description or linked issue/FR. Analyzes the
  codebase to produce realistic target files and implementation notes.
  Auto-transitions linked entities to planning states. Use when the user
  wants to break down work, plan implementation, create a WP, or says
  "scaffold", "break this down", "plan the implementation", or
  "create tasks for...".
argument-hint: <description | issue-id | fr-id>
---

# Scaffold Work Package

Create a complete work package with phases, tasks, dependencies, and target files by analyzing the codebase and requirements. The output should be detailed enough that `/pm-implement` can execute each task without guesswork.

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`

## Step 2: Load Requirements

Parse `$ARGUMENTS` to determine the source:

**If entity ID** (format `proj-N-issue-N` or `proj-N-fr-N`):
- Issue: Call `mcp__pinkrooster__get_issue_details` to load requirements
- FR: Call `mcp__pinkrooster__get_feature_request_details` to load requirements
- Extract: name, description, priority, state/status, userStories (array of role/goal/benefit), businessValue, acceptanceSummary, and any other detailed fields
- Use user stories to inform task design — each story may map to one or more tasks across phases

**Quality check for Feature Requests**: If the FR is missing key fields (no user stories, no business value, vague description <100 chars):
- **When called standalone (interactive)**: use the `AskUserQuestion` tool:
  - Question: "FR {frId} is sparse — missing {fields}. Refine first for better scaffolding?"
  - Header: "Sparse FR"
  - Options: `[{label: "Refine first", description: "Run /pm-refine-fr {frId} to add user stories and detail before scaffolding (Recommended)"}, {label: "Proceed anyway", description: "Scaffold now with the available data"}]`
- **When called from `/pm-next --auto` or any auto-mode workflow**: skip the warning entirely and proceed with scaffolding using available data. Do not prompt.

**If free-text description**:
- Use `$ARGUMENTS` directly as the feature description
- Derive a concise WP name from the description

## Step 3: Check for Existing Work Packages

Before creating a new WP, check if one already exists for this work:

1. Call `mcp__pinkrooster__get_work_packages` with `projectId`
2. If scaffolding from an entity ID, check if any existing WP already links to it (via `linkedIssueIds` or `linkedFeatureRequestIds`)
3. If scaffolding from free-text, check if any WP name/description closely matches

**If a linked WP exists**, use the `AskUserQuestion` tool:
- Question: "WP {wpId} '{wpName}' ({state}) already exists for {entityId}. How should I proceed?"
- Header: "Existing WP found"
- Options: `[{label: "View existing", description: "Show the existing WP details"}, {label: "Create new", description: "Create a separate WP anyway — the scope is different"}, {label: "Cancel", description: "Don't create another WP"}]`

If the user selects "View existing", call `mcp__pinkrooster__get_work_package_details` and present it, then re-ask.

## Step 4: Learn from Existing Work Packages

To produce consistent scaffolding, study how the project's existing WPs are structured:

1. Pick 1-2 completed or in-progress WPs from the list (prefer completed — they represent the project's actual standards)
2. Call `mcp__pinkrooster__get_work_package_details` on them
3. Note patterns:
   - How tasks are named and described
   - Level of detail in `implementationNotes`
   - How `targetFiles` are specified
   - Task sizing (how much work per task)
   - Acceptance criteria style and specificity
   - How dependencies are structured within phases

Match these patterns in the new WP. If no existing WPs are available, use the patterns in [scaffold-patterns.md](scaffold-patterns.md) as the reference.

## Step 5: Research the Domain (when warranted)

For features involving technologies, libraries, or patterns not already established in the codebase, research before scaffolding. Well-informed tasks save implementation time.

**When to research:**
- Integrating a new library or external service (e.g., "add Stripe billing", "webhook HMAC signing")
- Implementing a pattern the codebase hasn't used before (e.g., "add WebSocket support", "implement RBAC")
- The feature description references a standard or spec worth verifying (e.g., "RFC 7807 error responses")

**When to skip:**
- The feature follows existing patterns (another CRUD entity, another dashboard page)
- All relevant knowledge is already in the codebase
- Scaffolding from a well-detailed FR that already includes technical context

**How to research:**
- Use `WebSearch` with targeted queries (e.g., "ASP.NET Core webhook HMAC signature middleware")
- Use `WebFetch` to pull relevant library docs or API references
- Fold findings into task `implementationNotes` — mention specific libraries, patterns, or configuration approaches discovered

## Step 6: Analyze Codebase

Determine which layers of the codebase need changes:

1. Use Grep/Glob to find files related to the feature area
2. Use Serena's `mcp__serena__get_symbols_overview` on relevant directories
3. Identify which project layers are affected:
   - **Shared**: New DTOs, enums, constants, helpers?
   - **Data**: New entities, migrations, DB context changes?
   - **API**: New services, controllers, middleware?
   - **MCP**: New tools, inputs, responses?
   - **Dashboard**: New pages, hooks, API client functions?
   - **Tests**: New integration tests, unit tests?

For each affected layer, identify:
- Specific files to create or modify
- Existing patterns to follow (find similar implementations)
- Dependencies on other layers

Refer to [scaffold-patterns.md](scaffold-patterns.md) for the standard phase structure.

## Step 7: Design Work Package Structure

Build the WP following the project's vertical slice pattern:

**Phase ordering** (skip phases not needed for this feature):
1. Shared + Data Layer
2. API Layer
3. MCP Layer
4. Dashboard
5. Integration Testing

**For each phase**:
- Name and description
- Acceptance criteria (see criteria guidance below)

**For each task within a phase**:
- `name`: concise action (e.g., "Add WorkPackageExport entity")
- `description`: what specifically needs to be done
- `implementationNotes`: concrete guidance referencing existing patterns and any research findings
- `targetFiles`: actual file paths found during codebase analysis
- `dependsOnTaskIndices`: 0-based indices of tasks within the same phase that must complete first

### Acceptance Criteria Guidance

Each phase should have 2-5 acceptance criteria that are **specific, testable, and verifiable**. They define what "done" means for the phase — `/pm-verify` will check these.

**Good criteria are:**
- Observable: something you can check by looking at code, running a command, or testing an endpoint
- Specific: mention exact endpoints, file names, or behaviors — not vague quality statements
- Independent: each criterion checks one thing

**Format each criterion with:**
- `name`: short label (e.g., "Entity has audit logging")
- `description`: specific testable condition (e.g., "WorkPackageExportAuditLog table exists with FieldName, OldValue, NewValue columns and entries are created on every field change")
- `verificationMethod`: `AutomatedTest` (has a test), `AgentReview` (Claude can verify by reading code), or `Manual` (requires human testing)

**Prefer `AgentReview` for** code structure, configuration, and API contract checks.
**Prefer `AutomatedTest` for** behavior that has integration or unit tests.
**Prefer `Manual` for** UI workflows, deployment steps, and environment-dependent checks.

**Example — bad**: `{name: "API works", description: "The API endpoints work correctly"}`
**Example — good**: `{name: "CRUD endpoints respond correctly", description: "POST /api/projects/{id}/exports returns 201 with entity ID. PATCH returns 200. GET returns the entity with all fields. DELETE returns 204."}`

### Task Sizing Guidance

Each task should represent **15-45 minutes of implementation work**. This keeps tasks meaningful without being overwhelming.

**Signs a task is too large** (split it):
- Touches more than 3 files
- Involves both creating an interface AND its full implementation AND its tests
- Description includes "and" connecting distinct actions (e.g., "Create service and write tests")

**Signs a task is too small** (merge it):
- Just adding an import or a single line
- Renaming a file with no other changes
- Adding a DI registration with no other context

**Right-sized examples** from this project:
- "Create ExportService interface and implementation" (1 interface + 1 class, following existing pattern)
- "Add API integration tests for export endpoints" (1 test file, multiple test methods)
- "Create export list page with TanStack Table" (1 page component following existing list page pattern)

## Step 8: Estimate Complexity

Rate 1-10 based on:
- Number of layers affected
- Number of new files vs modifications
- Whether new patterns are needed or existing ones can be followed
- Database migration complexity
- UI complexity

Provide a `estimationRationale` explaining the score.

## Step 9: Create Work Package

Call `mcp__pinkrooster__scaffold_work_package` with:
- `projectId`
- `name`: derived from requirements
- `description`: detailed WP description
- `phases`: the designed phase structure (from Step 7)
- `type`: `Feature` (default), `BugFix` (if from bug issue), `Refactor` (if refactoring)
- `priority`: from the source entity, or `Medium` for free-text
- `estimatedComplexity`: from Step 8
- `estimationRationale`: from Step 8
- `linkedIssueIds`: `[issueId]` if scaffolding from an issue
- `linkedFeatureRequestIds`: `[frId]` if scaffolding from an FR

## Step 10: Auto-Transition Linked Entities

Automatically update the source entity state to reflect that planning/scaffolding has occurred. No user confirmation needed — scaffolding inherently means planning is underway.

**If scaffolding from a Feature Request**:
- If the FR status is Proposed or UnderReview:
  - Call `mcp__pinkrooster__create_or_update_feature_request` with `status: "Scheduled"`
  - Report: "Auto-transitioned FR {frId} → Scheduled (work package scaffolded)"
- If the FR is already Approved/Scheduled/InProgress or terminal: no change needed

**If scaffolding from an Issue**:
- If the issue state is NotStarted:
  - Call `mcp__pinkrooster__create_or_update_issue` with `projectId`, `issueId`, and `state: "Designing"`
  - Report: "Auto-transitioned issue {issueId} → Designing (work package scaffolded)"
- If the issue is already in an active or terminal state: no change needed

## Step 11: Report

```
## Scaffolded: {wpId} "{wpName}"

### Structure
- **Complexity**: {N}/10 — {rationale}
- **Phases**: {count}
- **Tasks**: {totalTasks}
- **Dependencies**: {totalDependencies}

### Phase Breakdown
**Phase 1: {name}** ({taskCount} tasks, {criteriaCount} acceptance criteria)
- {taskId} "{taskName}" — {targetFiles summary}
- ...

**Phase 2: {name}** ({taskCount} tasks, {criteriaCount} acceptance criteria)
- ...

### Research Applied
- {key finding 1, if research was performed}
- {key finding 2}

### Linked Entity
- {issueId or frId}: "{name}" ({state/status})

### State Transitions
- {issueId or frId}: {oldState} → {newState} (if auto-transitioned)

### Next Steps
- Start implementing: `/pm-implement {wpId}` or `/pm-next`
- Verify after implementation: `/pm-verify {wpId}`
- View project status: `/pm-status`
- Review backlog priorities: `/pm-triage`
```

## Constraints

- Always analyze the codebase before scaffolding — never guess at file paths
- Use `targetFiles` with real paths found during analysis
- Follow the vertical slice pattern from scaffold-patterns.md
- Match the style of existing WPs in the project — study them before designing the new one
- Keep tasks right-sized: 15-45 minutes of work each, targeting 1-3 files per task
- Include `implementationNotes` with specific guidance for every task, incorporating research findings where applicable
- Write acceptance criteria that are specific and testable — vague criteria like "works correctly" waste verification time
- Do not create phases for layers that aren't affected by the feature
- Check for existing WPs before creating duplicates
- **Auto-transition** linked entities to appropriate planning states without asking
