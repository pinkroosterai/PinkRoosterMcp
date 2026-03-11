---
name: pm-scaffold
description: >-
  Scaffold a complete work package with phases, tasks, and dependencies
  based on a feature description or linked issue/FR. Analyzes the
  codebase to produce realistic target files and implementation notes.
  Auto-transitions linked entities to planning states.
disable-model-invocation: true
argument-hint: <description | issue-id | fr-id>
---

# Scaffold Work Package

Create a complete work package with phases, tasks, dependencies, and target files by analyzing the codebase and requirements.

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

**If free-text description**:
- Use `$ARGUMENTS` directly as the feature description
- Derive a concise WP name from the description

## Step 3: Analyze Codebase

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

## Step 4: Design Work Package Structure

Build the WP following the project's vertical slice pattern:

**Phase ordering** (skip phases not needed for this feature):
1. Shared + Data Layer
2. API Layer
3. MCP Layer
4. Dashboard
5. Integration Testing

**For each phase**:
- Name and description
- Acceptance criteria (what must be true when phase is done)

**For each task within a phase**:
- `name`: concise action (e.g., "Add WorkPackageExport entity")
- `description`: what specifically needs to be done
- `implementationNotes`: concrete guidance referencing existing patterns
- `targetFiles`: actual file paths found during codebase analysis
- `dependsOnTaskIndices`: 0-based indices of tasks within the same phase that must complete first

## Step 5: Estimate Complexity

Rate 1-10 based on:
- Number of layers affected
- Number of new files vs modifications
- Whether new patterns are needed or existing ones can be followed
- Database migration complexity
- UI complexity

Provide a `estimationRationale` explaining the score.

## Step 6: Create Work Package

Call `mcp__pinkrooster__scaffold_work_package` with:
- `projectId`
- `name`: derived from requirements
- `description`: detailed WP description
- `phases`: the designed phase structure (from Step 4)
- `type`: `Feature` (default), `BugFix` (if from bug issue), `Refactor` (if refactoring)
- `priority`: from the source entity, or `Medium` for free-text
- `estimatedComplexity`: from Step 5
- `estimationRationale`: from Step 5
- `linkedIssueId`: if scaffolding from an issue
- `linkedFeatureRequestId`: if scaffolding from an FR

## Step 7: Auto-Transition Linked Entities

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

## Step 8: Report

```
## Scaffolded: {wpId} "{wpName}"

### Structure
- **Complexity**: {N}/10 — {rationale}
- **Phases**: {count}
- **Tasks**: {totalTasks}
- **Dependencies**: {totalDependencies}

### Phase Breakdown
**Phase 1: {name}** ({taskCount} tasks)
- {taskId} "{taskName}" — {targetFiles summary}
- ...

**Phase 2: {name}** ({taskCount} tasks)
- ...

### Linked Entity
- {issueId or frId}: "{name}" ({state/status})

### State Transitions
- {issueId or frId}: {oldState} → {newState} (if auto-transitioned)

### Next Steps
- Start implementation: `/pm-next Task`
- View details: `/pm-status`
- Implement specific task: `/pm-implement {first-task-id}`
```

## Constraints

- ALWAYS analyze the codebase before scaffolding — never guess at file paths
- Use `targetFiles` with real paths found during analysis
- Follow the vertical slice pattern from scaffold-patterns.md
- Keep tasks granular but not trivial — each task should represent meaningful work
- Include `implementationNotes` with specific guidance for every task
- Do not create phases for layers that aren't affected by the feature
- **Auto-transition** linked entities to appropriate planning states without asking
