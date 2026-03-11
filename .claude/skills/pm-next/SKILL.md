---
name: pm-next
description: >-
  Pick up the next highest-priority work package and start implementing it.
  Loads WP context including linked issue/FR and user stories, transitions
  to Implementing, auto-activates linked entities, then delegates to
  /pm-implement for task-level execution. Use --auto to loop until all
  work is done.
argument-hint: "[entity-type: Wp | Issue | FeatureRequest] [--auto]"
---

# Start Next Priority Work Package

Get the highest-priority actionable work package, load its full context, transition to active state, and begin implementing.

## Step 0: Parse Flags

Parse `$ARGUMENTS` for flags and entity type:

- **`--auto` flag**: If present, enable auto-loop mode (see Auto-Loop Mode below). Remove the flag before further parsing.
- **Entity type**: Remaining argument (Wp, Issue, FeatureRequest), or default to `Wp`.

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`

## Step 2: Get Priority Queue

Call `mcp__pinkrooster__get_next_actions` with:
- `projectId` from Step 1
- `limit`: 5
- `entityType`: parsed entity type from Step 0, default `Wp`

## Step 3: Present Options

**If `--auto` is NOT set** (interactive mode):

Show the top items to the user:

```
## Next Actions

1. [{priority}] {entityId} "{name}" ({state}) — {type}
2. [{priority}] {entityId} "{name}" ({state}) — {type}
3. ...

Which item to start? (number, or Enter for #1)
```

If only one item, auto-select it. If no items, report: "No actionable work packages found. Run `/pm-status` for full overview."

**If `--auto` is set** (auto-loop mode):

Auto-select the #1 highest-priority item. Do not prompt the user. If no items, the loop ends (see Auto-Loop Mode).

## Step 4: Load Full Context

Based on the selected item's entity type:

**If Work Package** (ID format: `proj-N-wp-N`):
1. Call `mcp__pinkrooster__get_work_package_details` with the WP ID
2. Extract:
   - WP: `name`, `description`, `plan`, `type`, `priority`, `state`, `estimatedComplexity`
   - Phases: all `phases[]` with task counts, acceptance criteria
   - Tasks: summary of non-terminal tasks across phases (count, states)
   - Dependencies: `blockedBy` WPs — if any blocker is non-terminal:
     - **In auto mode**: skip this WP silently and pick the next item from the queue
     - **In interactive mode**: report "This WP is blocked by {blockerId}. Consider working on the blocker first, or pick a different item."
3. **Load linked Issue** (if `linkedIssueId` exists):
   - Call `mcp__pinkrooster__get_issue_details` — extract `name`, `description`, `stepsToReproduce`, `affectedComponent`
4. **Load linked FR** (if `linkedFeatureRequestId` exists):
   - Call `mcp__pinkrooster__get_feature_request_details` — extract `name`, `description`, `userStories` (array of role/goal/benefit), `businessValue`, `acceptanceSummary`
   - Display user stories as context: "As a [role], I want [goal], so that [benefit]" for each story

**If Issue** (ID format: `proj-N-issue-N`):
1. Call `mcp__pinkrooster__get_issue_details` with the issue ID
2. Extract: `name`, `description`, `stepsToReproduce`, `expectedBehavior`, `actualBehavior`, `affectedComponent`
3. Check for linked work packages — if none, suggest: "This issue has no work package. Run `/pm-scaffold {issueId}` to create one."

**If Feature Request** (ID format: `proj-N-fr-N`):
1. Call `mcp__pinkrooster__get_feature_request_details` with the FR ID
2. Extract: `name`, `description`, `userStories` (array of role/goal/benefit), `businessValue`, `acceptanceSummary`
3. Display user stories as context: "As a [role], I want [goal], so that [benefit]" for each story
4. Check for linked work packages — if none, suggest: "This FR has no work package. Run `/pm-scaffold {frId}` to create one."

## Step 5: Transition State + Auto-Activate Related Entities

Transition the selected item to an active state, and automatically activate all related entities. No user confirmation needed — starting work inherently means parent entities are active.

**For Work Packages**:
1. **Activate WP**: If the WP state is inactive (NotStarted or Blocked):
   - Call `mcp__pinkrooster__create_or_update_work_package` with `projectId`, `workPackageId`, and `state: "Implementing"`
   - Report: "WP {wpId} → Implementing"
2. **Auto-activate linked Issue**: If WP has a `linkedIssueId` and the issue is NotStarted or Blocked:
   - Call `mcp__pinkrooster__create_or_update_issue` with `projectId`, `issueId`, and `state: "Implementing"`
   - Report: "Auto-activated linked issue {issueId} → Implementing"
3. **Auto-activate linked FR**: If WP has a `linkedFeatureRequestId` and the FR is in an inactive state (Proposed or Deferred):
   - Call `mcp__pinkrooster__create_or_update_feature_request` with `projectId`, `featureRequestId`, and `status: "InProgress"`
   - Report: "Auto-activated linked FR {frId} → InProgress"

**For Issues**:
- Call `mcp__pinkrooster__create_or_update_issue` with `projectId`, `issueId`, and `state: "Implementing"`

**For Feature Requests**:
- Call `mcp__pinkrooster__create_or_update_feature_request` with `projectId`, `featureRequestId`, and `status: "InProgress"`

## Step 6: Present Summary and Delegate to pm-implement

Summarize the context loaded and hand off to `/pm-implement` for task-level execution:

```
## Starting: {wpId} "{wpName}"

### Context
- **Type**: {wpType} | **Priority**: {priority} | **Complexity**: {estimatedComplexity}/10
- **Description**: {description}
- **Linked Issue**: {issueId} "{issueName}" (if present)
- **Linked FR**: {frId} "{frName}" (if present)
- **User Stories**: (if linked FR has user stories)
  - As a {role}, I want {goal}, so that {benefit}
  - ...

### Structure
- **Phases**: {phaseCount}
- **Tasks**: {nonTerminalCount} remaining of {totalTasks} total

### State Transitions
- {wpId} → Implementing
- {linkedIssueId} → Implementing (if auto-activated)
- {linkedFrId} → InProgress (if auto-activated)
```

Then delegate task execution by invoking `/pm-implement {wpId}` to execute all tasks across phases.

## Step 7: After Implementation

When `/pm-implement` completes, mark the WP done:
- Call `/pm-done {wpId}` to finalize completion and report cascades.

**If `--auto` is set**: proceed to Auto-Loop Mode. Otherwise:

"Work package complete. Run `/pm-status` to check project progress."

---

## Auto-Loop Mode

When `--auto` is set, after each WP completes (Step 7), loop back to continue with the next item:

### Loop Iteration

1. Report a progress separator:
   ```
   ---
   ## Auto-loop: completed {completedCount} items. Checking for more...
   ```
2. Re-run **Step 2** (Get Priority Queue) with the same `projectId` and entity type
3. If items remain:
   - Auto-select #1 (no user prompt)
   - Continue from **Step 4** through **Step 7**
   - Increment `completedCount`
4. If no items remain: exit the loop (see Loop Exit)

### Blocked Item Handling in Auto Mode

When the top item is blocked:
1. Skip it and try the next item in the queue (items #2, #3, etc.)
2. If ALL items in the queue are blocked, exit the loop:
   "All remaining items are blocked. Completed {completedCount} items this session."

### Loop Exit

When no more actionable items exist:

```
## Auto-loop Complete

### Session Summary
- **Items completed**: {completedCount}
- **Completed**: {list of entityId + name pairs}

### Remaining
- **Blocked**: {count} items still blocked
- **Total open**: {count} across all entity types

Run `/pm-status` for full project overview.
```

### Safety Rails

- **No infinite loops**: The loop always terminates — either items run out or all remaining are blocked
- **Re-fetch every iteration**: Always call `get_next_actions` fresh to reflect cascading state changes (auto-unblocks from completed work may surface new items)
- **Build + test per WP**: Each `/pm-implement` cycle includes build and test — failures stop the current WP and pause the loop for user intervention
- **Commit per WP**: After each successful WP, create a git commit with a conventional message summarizing the changes. This keeps work atomic and recoverable.

## Constraints

- **Default to Work Packages** — when no entity type is specified, filter by `Wp`
- Tasks are too fine-grained for `/pm-next` — use `/pm-implement` for task-level execution
- If a WP is blocked by another WP, suggest working on the blocker first (or skip in auto mode)
- For Issues/FRs without linked WPs, suggest `/pm-scaffold` before implementation
- **Auto-activate** parent entities (WP, linked Issue/FR) at the START without asking
- **`--auto` mode is fully autonomous** — no user prompts during the loop, auto-select #1, auto-commit after each WP
