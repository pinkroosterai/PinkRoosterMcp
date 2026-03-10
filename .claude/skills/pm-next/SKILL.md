---
name: pm-next
description: >-
  Pick up the next highest-priority task and start implementing it.
  Fetches task details, reads relevant code, transitions to Implementing,
  and begins the work. Auto-transitions linked entities to active states.
disable-model-invocation: true
argument-hint: [entity-type: Task | Wp | Issue | FeatureRequest]
---

# Start Next Priority Item

Get the highest-priority actionable item, load its full context, transition to active state, and begin working.

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`

## Step 2: Get Priority Queue

Call `mcp__pinkrooster__get_next_actions` with:
- `projectId` from Step 1
- `limit`: 5
- `entityType`: `$ARGUMENTS` if provided (Task, Wp, Issue, FeatureRequest), otherwise omit for all types

## Step 3: Present Options

Show the top items to the user:

```
## Next Actions

1. [{priority}] {entityId} "{name}" ({state}) — {type}
2. [{priority}] {entityId} "{name}" ({state}) — {type}
3. ...

Which item to start? (number, or Enter for #1)
```

If only one item, auto-select it. If no items, report: "No actionable items found. Run `/pm-status` for full overview."

## Step 4: Load Full Context

Based on the selected item's entity type:

**If Task** (ID format: `proj-N-wp-N-task-N`):
1. Extract WP ID by trimming `-task-N` suffix
2. Call `mcp__pinkrooster__get_work_package_details` with the WP ID
3. Find the selected task in the response's `phases[].tasks[]`
4. Extract:
   - Task: `name`, `description`, `implementationNotes`, `targetFiles`, `blockedBy`
   - Phase: `name`, `acceptanceCriteria`
   - WP: `name`, `description`, `plan`, `linkedIssueId`, `linkedFeatureRequestId`
5. Check `blockedBy` — if any blocker is non-terminal, report:
   "This task is blocked by {blockerId}. Consider working on the blocker first, or pick a different item."

**If Issue** (ID format: `proj-N-issue-N`):
1. Call `mcp__pinkrooster__get_issue_details` with the issue ID
2. Extract: `name`, `description`, `stepsToReproduce`, `expectedBehavior`, `actualBehavior`, `affectedComponent`

**If Feature Request** (ID format: `proj-N-fr-N`):
1. Call `mcp__pinkrooster__get_feature_request_details` with the FR ID
2. Extract: `name`, `description`, `userStory`, `businessValue`, `acceptanceSummary`
3. Check for linked work packages — if none, suggest: "This FR has no work package. Run `/pm-scaffold {frId}` to create one."

## Step 5: Read Target Code

**For Tasks**: Read each file in `targetFiles` using the Read tool. If no target files specified, use the task description to identify relevant files via Grep/Glob.

**For Issues**: If `affectedComponent` is set, search for it with Grep. Read the relevant files.

**For Feature Requests**: Analyze the description to identify which codebase areas are relevant. Read key files.

Use Serena's `get_symbols_overview` on the most relevant files for structural understanding.

## Step 6: Transition State + Auto-Activate Related Entities

Transition the selected item to an active state, and automatically activate all related entities up the chain. No user confirmation needed — starting work on a task inherently means the parent entities are active.

**For Tasks**:
1. Call `mcp__pinkrooster__create_or_update_task` with `taskId` and `state: "Implementing"`
2. **Auto-activate WP**: If the WP state is inactive (NotStarted or Blocked):
   - Call `mcp__pinkrooster__create_or_update_work_package` with `projectId`, `workPackageId`, and `state: "Implementing"`
   - Report: "Auto-activated WP {wpId} → Implementing"
3. **Auto-activate linked Issue**: If WP has a `linkedIssueId`, call `mcp__pinkrooster__get_issue_details` to check its state
   - If the issue is NotStarted or Blocked (not already active or terminal):
     - Call `mcp__pinkrooster__create_or_update_issue` with `projectId`, `issueId`, and `state: "Implementing"`
     - Report: "Auto-activated linked issue {issueId} → Implementing"
4. **Auto-activate linked FR**: If WP has a `linkedFeatureRequestId`, call `mcp__pinkrooster__get_feature_request_details` to check its status
   - If the FR is in an inactive state (Proposed or Deferred):
     - Call `mcp__pinkrooster__create_or_update_feature_request` with `projectId`, `featureRequestId`, and `status: "InProgress"`
     - Report: "Auto-activated linked FR {frId} → InProgress"

**For Issues**:
- Call `mcp__pinkrooster__create_or_update_issue` with `projectId`, `issueId`, and `state: "Implementing"`

**For Feature Requests**:
- Call `mcp__pinkrooster__create_or_update_feature_request` with `projectId`, `featureRequestId`, and `status: "InProgress"`

## Step 7: Begin Implementation

Summarize the context loaded and begin implementing:

```
## Starting: {entityId} "{name}"

### Context
- WP: {wpName} (if task)
- Phase: {phaseName} (if task)
- Description: {description}
- Implementation Notes: {notes} (if task)
- Target Files: {list} (if task)

### State Transitions
- {entityId} → Implementing
- {wpId} → Implementing (if auto-activated)
- {linkedIssueId} → Implementing (if auto-activated)
- {linkedFrId} → InProgress (if auto-activated)

### Plan
{Describe your implementation approach based on the loaded context}
```

Then proceed with the actual implementation using Edit, Write, and other tools.

## After Implementation

Summarize what was changed and suggest completion:

"Implementation complete. Run `/pm-done {entityId}` to mark as completed."
