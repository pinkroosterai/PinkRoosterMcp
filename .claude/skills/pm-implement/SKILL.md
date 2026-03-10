---
name: pm-implement
description: >-
  Implement a task, phase, or entire work package. Reads context,
  analyzes target files, implements code changes, runs tests, and
  updates task states. Auto-transitions linked entities on start
  and completion.
disable-model-invocation: true
argument-hint: <task-id | phase-id | wp-id> [--dry-run]
---

# Implement Task, Phase, or Work Package

Given a task ID, phase ID, or work package ID, load full context from PinkRooster, understand requirements, implement code changes, run tests, and update state. For phases and WPs, tasks are executed sequentially in dependency order.

## Step 1: Parse Input & Detect Mode

Parse `$ARGUMENTS` to determine execution mode:

**Task mode** — format `proj-{N}-wp-{N}-task-{N}`:
- Extract WP ID: strip `-task-{N}` suffix
- Single task execution

**Phase mode** — format `proj-{N}-wp-{N}-phase-{N}`:
- Extract WP ID: strip `-phase-{N}` suffix
- Execute all non-terminal tasks in the specified phase

**WP mode** — format `proj-{N}-wp-{N}`:
- Execute all non-terminal tasks across all phases in order

**`--dry-run` flag**: If present, perform Steps 1-3 only — show the execution plan without implementing. Remove the flag before ID parsing.

If the ID format doesn't match any pattern, report:
"Invalid ID format. Expected one of: `proj-N-wp-N-task-N`, `proj-N-wp-N-phase-N`, or `proj-N-wp-N`"

## Step 2: Load Full WP Context

Call `mcp__pinkrooster__get_work_package_details` with the extracted WP ID.

From the response, extract:
1. **Work Package**: `name`, `description`, `plan`, `type`, `priority`, `state`, `linkedIssueId`, `linkedFeatureRequestId`
2. **All Phases**: `phases[]` with their tasks, ordered by phase number
3. **Phase Details**: each phase's `name`, `description`, `acceptanceCriteria`
4. **Task Details**: each task's `name`, `description`, `implementationNotes`, `targetFiles`, `attachments`, `blockedBy`, `state`

## Step 3: Build Execution Queue

### Task Mode
- Find the single target task in `phases[].tasks[]`
- Queue = `[targetTask]`

### Phase Mode
- Find the target phase by matching the phase ID
- Collect all tasks in that phase where `state` is NOT terminal (Completed/Cancelled/Replaced)
- Sort by dependency order: tasks with no `blockedBy` first, then tasks whose blockers appear earlier in the queue
- Queue = sorted non-terminal tasks from that phase

### WP Mode
- Iterate phases in order (Phase 1, 2, 3, ...)
- Within each phase, collect non-terminal tasks sorted by dependency order (same as phase mode)
- Queue = all non-terminal tasks across all phases, phases in order, dependency-sorted within each phase

**Present the execution plan:**

```
## Execution Plan: {mode} mode

**WP**: {wpId} "{wpName}"
**Tasks to execute**: {count} of {totalTasks}
**Skipping**: {skippedCount} already terminal

| # | Task ID | Task Name | Phase | Blocked By |
|---|---------|-----------|-------|------------|
| 1 | {id}    | {name}    | {phase} | — |
| 2 | {id}    | {name}    | {phase} | {blocker} |
| ... |
```

**If `--dry-run`**: Display the plan and STOP. Report: "Dry run complete. Remove `--dry-run` to execute."

## Step 4: Auto-Activate Parent Entities

Before executing any tasks, automatically transition parent and linked entities to active states. No user confirmation needed — the user opted into implementation by invoking this skill.

1. **Auto-activate WP**: If the WP state is inactive (NotStarted or Blocked):
   - Call `mcp__pinkrooster__create_or_update_work_package` with `projectId`, `workPackageId`, and `state: "Implementing"`
   - Report: "Auto-activated WP {wpId} → Implementing"

2. **Auto-activate linked Issue**: If WP has a `linkedIssueId`:
   - Call `mcp__pinkrooster__get_issue_details` to check state
   - If the issue is NotStarted or Blocked (not already active or terminal):
     - Call `mcp__pinkrooster__create_or_update_issue` with `projectId`, `issueId`, and `state: "Implementing"`
     - Report: "Auto-activated linked issue {issueId} → Implementing"

3. **Auto-activate linked FR**: If WP has a `linkedFeatureRequestId`:
   - Call `mcp__pinkrooster__get_feature_request_details` to check status
   - If the FR is in an inactive state (Proposed or Deferred):
     - Call `mcp__pinkrooster__create_or_update_feature_request` with `projectId`, `featureRequestId`, and `status: "InProgress"`
     - Report: "Auto-activated linked FR {frId} → InProgress"

## Step 5: Execute Task Queue

For each task in the queue, run the **Task Execution Loop** (Steps 5a–5g):

---

### Step 5a: Check Dependencies

If `task.blockedBy` is non-empty:
1. Check each blocker's state (use the WP context already loaded, or re-fetch if a previous task in the queue was a blocker that was just completed)
2. If ANY blocker is in a non-terminal state AND is NOT in the execution queue ahead of this task:
   - Report: "Skipping {taskId} — blocked by {blockerId} ({blockerName}, state: {state}) which is not in the execution queue"
   - Add to skipped list and continue to next task
3. If blockers are all terminal or were completed earlier in this queue: proceed

If `task.state` is already terminal (Completed/Cancelled/Replaced):
- Skip silently — already done

### Step 5b: Read Target Code

**If `targetFiles` is provided**:
1. Read each file using the Read tool
2. For each file, use Serena's `mcp__serena__get_symbols_overview` to understand structure
3. If a file doesn't exist yet (new file to create), note it

**If `targetFiles` is empty**:
1. Analyze the task description and implementation notes
2. Use Grep/Glob to find related files in the codebase
3. Read the most relevant files

**Always**: Use Serena's `mcp__serena__find_symbol` or `mcp__serena__find_referencing_symbols` to understand how existing code connects to what needs changing.

### Step 5c: Transition to Implementing

Call `mcp__pinkrooster__create_or_update_task` with:
- `taskId`: the current task ID
- `state`: `Implementing`

Report: "Task {taskId} → Implementing ({currentIndex}/{totalInQueue})"

### Step 5d: Present Implementation Plan

```
## [{currentIndex}/{totalInQueue}] Implementing: {taskId} "{taskName}"

### Context
- **WP**: {wpName}
- **Phase**: {phaseName}
- **Description**: {taskDescription}
- **Implementation Notes**: {implementationNotes}
- **Target Files**: {list of files}

### Implementation Plan
1. {Step-by-step plan based on task details and code analysis}
2. ...
```

### Step 5e: Implement

Execute the implementation plan:
1. Make code changes using Edit/Write tools
2. Follow the project's coding conventions (see CLAUDE.md):
   - C#: 4-space indent, nullable enabled
   - TypeScript: 2-space indent
   - Follow existing patterns in the codebase
3. Do NOT over-engineer — implement exactly what the task requires

### Step 5f: Run Tests

After implementation, run relevant tests:

**For .NET changes** (src/PinkRooster.Api, .Data, .Shared, .Mcp):
```bash
dotnet build PinkRooster.slnx
dotnet test
```

**For Dashboard changes** (src/dashboard):
```bash
cd src/dashboard && npm test
```

If tests fail:
1. Analyze the failure
2. Fix the issue
3. Re-run tests
4. Repeat until passing

### Step 5g: Mark Task Complete & Report Cascades

Call `mcp__pinkrooster__create_or_update_task` with:
- `taskId`: the current task ID
- `state`: `Completed`

**Report cascade results**: If `stateChanges` are returned (phase auto-complete, WP auto-complete, auto-unblock), display them:

```
{taskId} "{taskName}" → Completed
  Cascades:
  - {entityType} {entityId}: {oldState} → {newState} ({reason})
```

**Auto-complete linked entities on WP completion**: If `stateChanges` contains a WP auto-complete:
1. Check the WP's `linkedIssueId` — if present and the issue is not terminal:
   - Call `mcp__pinkrooster__create_or_update_issue` with `state: "Completed"`
   - Report: "Auto-completed linked issue {issueId}"
2. Check the WP's `linkedFeatureRequestId` — if present and the FR is not terminal:
   - Call `mcp__pinkrooster__create_or_update_feature_request` with `status: "Completed"`
   - Report: "Auto-completed linked FR {frId}"

**Update local state**: Mark this task as completed in the in-memory queue so subsequent dependency checks reflect the new state.

---

## Step 6: Final Summary

After all tasks in the queue have been processed:

```
## Execution Complete: {wpId or phaseId}

### Results
| Task ID | Task Name | Result |
|---------|-----------|--------|
| {id}    | {name}    | Completed |
| {id}    | {name}    | Skipped (blocked) |
| {id}    | {name}    | Already complete |

### Stats
- **Implemented**: {count}
- **Skipped (blocked)**: {count}
- **Already terminal**: {count}

### State Transitions
- WP {wpId}: {oldState} → {newState} (if changed)
- Issue {issueId}: {oldState} → {newState} (if auto-activated or auto-completed)
- FR {frId}: {oldStatus} → {newStatus} (if auto-activated or auto-completed)
- {All task/phase/WP cascades accumulated during execution}

### Changes Made
- {file}: {summary}
- ...

### Tests
- {test results summary}

### Next Steps
- Run `/pm-status` to check overall progress
- Run `/pm-next` to pick up the next priority item
```

## Constraints

- In **task mode**, NEVER auto-complete the task — always let the user confirm via `/pm-done`
- In **phase/WP mode**, tasks ARE auto-completed as part of the execution loop (the user opted into batch execution by providing a phase/WP ID)
- If a task is blocked by something outside the queue, SKIP it — do not bypass blockers
- Follow existing code patterns — read before writing
- Run tests after every task implementation (not just at the end)
- Keep changes focused on exactly what each task describes
- Re-fetch WP details if needed to get updated state after cascades
- **Auto-activate** parent entities (WP, linked Issue/FR) at the START without asking
- **Auto-complete** linked entities (Issue/FR) on WP completion without asking
