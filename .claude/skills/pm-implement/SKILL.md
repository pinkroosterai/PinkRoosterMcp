---
name: pm-implement
description: >-
  Implement a task, phase, or entire work package. Reads context,
  analyzes target files, implements code changes, runs tests, and
  updates task states. Auto-transitions linked entities on start
  and completion. Use when the user says "implement", "start working
  on", "execute", "build this", or references a task/phase/WP ID
  and wants code changes made.
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
1. **Work Package**: `name`, `description`, `plan`, `type`, `priority`, `state`, `linkedIssueIds`, `linkedFeatureRequestIds`
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

**Also create a rollback point** so we can revert if things go wrong:

```bash
git stash push -m "pm-implement-checkpoint-{wpId}" --include-untracked 2>/dev/null; git stash pop 2>/dev/null
```

This ensures any unstaged user work is safe. Then note the current commit hash as the rollback target:

```bash
git rev-parse HEAD
```

1. **Auto-activate WP**: If the WP state is inactive (NotStarted or Blocked):
   - Call `mcp__pinkrooster__create_or_update_work_package` with `projectId`, `workPackageId`, and `state: "Implementing"`
   - Report: "Auto-activated WP {wpId} → Implementing"

2. **Auto-activate linked Issues**: For each issue ID in `linkedIssueIds`:
   - Call `mcp__pinkrooster__get_issue_details` to check state
   - If the issue is NotStarted or Blocked (not already active or terminal):
     - Call `mcp__pinkrooster__create_or_update_issue` with `projectId`, `issueId`, and `state: "Implementing"`
     - Report: "Auto-activated linked issue {issueId} → Implementing"

3. **Auto-activate linked FRs**: For each FR ID in `linkedFeatureRequestIds`:
   - Call `mcp__pinkrooster__get_feature_request_details` to check status
   - If the FR is in an inactive state (Proposed or Deferred):
     - Call `mcp__pinkrooster__create_or_update_feature_request` with `projectId`, `featureRequestId`, and `status: "InProgress"`
     - Report: "Auto-activated linked FR {frId} → InProgress"

## Step 5: Execute Task Queue

For each task in the queue, run the **Task Execution Loop** (Steps 5a–5h):

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

### Step 5b: Research Before Implementation (when warranted)

Before reading target code, check if the task involves patterns or technologies not already established in the codebase. This step prevents wasted implementation time — getting the approach right upfront is cheaper than debugging a wrong approach.

**When to research:**
- The task's `implementationNotes` mention a library, pattern, or API you haven't seen in the codebase (e.g., "use HMAC-SHA256 signing", "implement exponential backoff", "add RFC 7807 ProblemDetails")
- The task creates a new type of infrastructure the codebase hasn't used before (e.g., background services, WebSocket handlers, middleware)
- The task references external standards or specifications

**When to skip (most tasks):**
- The task follows an existing pattern (another controller, another dashboard page, another test file)
- The `implementationNotes` reference specific existing files to follow
- The task is a simple modification (add a field, fix a guard, update a route)

**How to research:**
- Use `WebSearch` with targeted queries (e.g., "ASP.NET Core background service IHostedService pattern")
- Use `WebFetch` for specific library documentation
- Keep it brief — 1-2 searches, extract the key pattern, move on. Research should take seconds, not minutes.

### Step 5c: Read Target Code

**If `targetFiles` is provided**:
1. Read each file using the Read tool
2. For each file, use Serena's `mcp__serena__get_symbols_overview` to understand structure
3. If a file doesn't exist yet (new file to create), note it

**If `targetFiles` is empty**:
1. Analyze the task description and implementation notes
2. Use Grep/Glob to find related files in the codebase
3. Read the most relevant files

**Always**: Use Serena's `mcp__serena__find_symbol` or `mcp__serena__find_referencing_symbols` to understand how existing code connects to what needs changing.

### Step 5d: Transition to Implementing

Call `mcp__pinkrooster__create_or_update_task` with:
- `taskId`: the current task ID
- `state`: `Implementing`

Report: "Task {taskId} → Implementing ({currentIndex}/{totalInQueue})"

### Step 5e: Present Implementation Plan

```
## [{currentIndex}/{totalInQueue}] Implementing: {taskId} "{taskName}"

### Context
- **WP**: {wpName}
- **Phase**: {phaseName}
- **Description**: {taskDescription}
- **Implementation Notes**: {implementationNotes}
- **Target Files**: {list of files}
- **Research**: {key finding, if research was performed}

### Implementation Plan
1. {Step-by-step plan based on task details and code analysis}
2. ...
```

### Step 5f: Implement

Execute the implementation plan:
1. Make code changes using Edit/Write tools
2. Follow the project's coding conventions (see CLAUDE.md):
   - C#: 4-space indent, nullable enabled
   - TypeScript: 2-space indent
   - Follow existing patterns in the codebase
3. Do NOT over-engineer — implement exactly what the task requires

### Step 5g: Run Tests (targeted, then broad)

After implementation, run tests in two tiers to balance speed and confidence:

**Tier 1: Targeted tests** (run first — fast feedback):

For .NET changes, identify the most relevant test file:
```bash
# If the task touches a specific service/controller, run its test class
dotnet test --filter "FullyQualifiedName~{RelevantTestClass}" PinkRooster.slnx
```

For Dashboard changes:
```bash
cd src/dashboard && npx vitest run {relevant-test-file} --reporter=verbose
```

If targeted tests pass, proceed to Tier 2. If they fail, fix and re-run targeted tests first.

**Tier 2: Broad tests** (run at phase boundaries or after Tier 1 passes):

- **After every task in task mode**: run the full relevant suite (`dotnet test` or `npm test`)
- **In phase/WP mode**: run targeted tests after each task, full suite only at **phase boundaries** (after the last task in a phase completes). This keeps the feedback loop fast for individual tasks while still catching cross-cutting regressions before moving to the next phase.

If tests fail:
1. Analyze the failure output
2. Fix the issue
3. Re-run the failing tests
4. If fix attempts exceed 3 rounds, trigger the rollback procedure (see Step 5g-rollback)

### Step 5g-rollback: Rollback on Persistent Failure

If a task's implementation breaks the build or tests and 3 fix attempts haven't resolved it:

1. **Stash the broken changes**: `git stash push -m "pm-implement-failed-{taskId}"`
2. **Report clearly**:
   ```
   ## Implementation Failed: {taskId} "{taskName}"

   Failed after 3 fix attempts. Changes stashed as `pm-implement-failed-{taskId}`.

   ### Error
   {last test/build error output}

   ### What was attempted
   1. {fix attempt 1 summary}
   2. {fix attempt 2 summary}
   3. {fix attempt 3 summary}

   ### Recovery options
   - Review stashed changes: `git stash show -p "stash@{0}"`
   - Apply and fix manually: `git stash pop`
   - Discard and retry: `git stash drop`
   - Skip this task and continue: `/pm-implement {next-task-or-phase-id}`
   ```
3. **Do NOT mark the task as Completed** — leave it in Implementing state
4. **In phase/WP mode**: Stop processing the queue. The user needs to intervene.
5. **In task mode**: Report and stop.

### Step 5h: Mark Task Complete & Report Cascades

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
1. For each issue ID in the WP's `linkedIssueIds` — if the issue is not terminal:
   - Call `mcp__pinkrooster__create_or_update_issue` with `state: "Completed"`
   - Report: "Auto-completed linked issue {issueId}"
2. For each FR ID in the WP's `linkedFeatureRequestIds` — if the FR is not terminal:
   - Call `mcp__pinkrooster__create_or_update_feature_request` with `status: "Completed"`
   - Report: "Auto-completed linked FR {frId}"

**Update local state**: Mark this task as completed in the in-memory queue so subsequent dependency checks reflect the new state.

### Step 5i: Phase Verification Gate (Phase/WP Mode Only)

When `stateChanges` from Step 5h contains a **phase auto-complete**, run verification before proceeding to the next phase's tasks:

1. Check `stateChanges` for any entry where `entityType` is `Phase` and `newState` is `Completed`
2. For each auto-completed phase that has acceptance criteria:
   - Invoke `/pm-verify {phaseId}` to verify all criteria
   - If **all criteria pass**: report "Phase {phaseId} verified — proceeding to next phase" and continue
   - If **any criteria fail**: pause execution and report:
     ```
     Phase {phaseId} verification failed:
     - {criterionName}: {failureReason}

     Fix the issues and re-run: `/pm-implement {remaining-phase-or-wp-id}`
     Or skip verification and complete: `/pm-done {phaseId}`
     ```
     **Stop processing** the queue — do not proceed to subsequent phases.
3. If the auto-completed phase has **no acceptance criteria**, skip verification and continue.

This gate ensures each phase's acceptance criteria are met before moving on. In **task mode**, this step is skipped entirely (single-task execution doesn't trigger phase completion).

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
| {id}    | {name}    | Failed (stashed) |

### Stats
- **Implemented**: {count}
- **Skipped (blocked)**: {count}
- **Already terminal**: {count}
- **Failed**: {count}

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
- **Task mode only**: Mark complete: `/pm-done {taskId}`
- **Phase/WP mode**: Verify criteria: `/pm-verify {wpId}` → then `/pm-done {wpId}`
- **Standalone WP completion**: If WP auto-completed, finalize linked entities: `/pm-done {wpId}`
- Continue with next priority: `/pm-next`
- Check overall progress: `/pm-status`
```

## Constraints

- In **task mode**, NEVER auto-complete the task — always let the user confirm via `/pm-done`
- In **phase/WP mode**, tasks ARE auto-completed as part of the execution loop (the user opted into batch execution by providing a phase/WP ID)
- If a task is blocked by something outside the queue, SKIP it — do not bypass blockers
- Follow existing code patterns — read before writing
- Run targeted tests after every task, full suite at phase boundaries (phase/WP mode) or after every task (task mode)
- Keep changes focused on exactly what each task describes
- Re-fetch WP details if needed to get updated state after cascades
- **Auto-activate** parent entities (WP, linked Issue/FR) at the START without asking
- **Auto-complete** linked entities (Issue/FR) on WP completion without asking
- **Rollback on persistent failure** — stash broken changes after 3 fix attempts rather than leaving a broken codebase
- **Research is proportional** — most tasks follow existing patterns and need no research. Only look things up when the task involves genuinely unfamiliar technology.
