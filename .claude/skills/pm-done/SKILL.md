---
name: pm-done
description: >-
  Mark tasks, issues, or feature requests as completed and report
  cascading state changes. Automatically completes linked entities
  when a work package auto-completes.
argument-hint: <task-id | issue-id | fr-id> | all <wp-id>
---

# Complete Work Items

Mark entities as completed and report all cascading state changes. Automatically propagate completion to linked entities without asking.

## Determine Mode

Parse `$ARGUMENTS` to detect the mode:

1. **"all <wp-id>"** — Batch-complete all non-terminal tasks in a work package
2. **Single entity ID** — Complete one task, issue, or feature request
3. **Multiple entity IDs** — Batch-complete multiple tasks (must be in same WP)

Detect entity type from ID format:
- `proj-N-wp-N-task-N` → Task
- `proj-N-issue-N` → Issue
- `proj-N-fr-N` → Feature Request
- `proj-N-wp-N` → Work Package (used with "all" mode)

## Mode: Single Task

1. Call `mcp__pinkrooster__create_or_update_task` with:
   - `taskId`: the provided task ID
   - `state`: `Completed`
2. Report the result and any `stateChanges` from the response
3. **Run Auto-Complete Linked Entities** (see below)

## Mode: Single Issue

1. Call `mcp__pinkrooster__create_or_update_issue` with:
   - `projectId`: extracted from the issue ID
   - `issueId`: the provided issue ID
   - `state`: `Completed`
2. Report the result

**Note**: Do NOT prompt about missing rootCause/resolution. The user can add those separately before calling `/pm-done`. Only ask if the user explicitly requests help documenting the fix.

## Mode: Single Feature Request

1. Call `mcp__pinkrooster__create_or_update_feature_request` with:
   - `projectId`: extracted from the FR ID
   - `featureRequestId`: the provided FR ID
   - `status`: `Completed`

## Mode: Multiple Tasks (same WP)

1. Extract the work package ID from the first task ID (strip `-task-N`)
2. Call `mcp__pinkrooster__batch_update_task_states` with:
   - `workPackageId`: extracted WP ID
   - `tasks`: array of `{ taskId, state: "Completed" }` for each provided task ID
3. Report consolidated `stateChanges`
4. **Run Auto-Complete Linked Entities** (see below)

## Mode: "all <wp-id>"

1. Call `mcp__pinkrooster__get_work_package_details` with the WP ID
2. Collect all tasks that are NOT in a terminal state (Completed, Cancelled, Replaced)
3. If no non-terminal tasks found, report: "All tasks in {wpId} are already terminal."
4. Show the user which tasks will be completed and ask for confirmation:
   "Complete these {N} tasks? (y/n)"
   - List each task: `{taskId} "{name}" (currently {state})`
5. If confirmed, call `mcp__pinkrooster__batch_update_task_states` with all task IDs
6. Report consolidated `stateChanges`
7. **Run Auto-Complete Linked Entities** (see below)

## Auto-Complete Linked Entities

**This runs automatically after any task or batch completion that produces a WP auto-complete cascade.** No user confirmation needed — the WP completing means the work is done.

1. Check `stateChanges` for any entry where `entityType` is `WorkPackage` and `newState` is `Completed`
2. For each auto-completed WP:
   a. Call `mcp__pinkrooster__get_work_package_details` with the WP ID to get `linkedIssueId` and `linkedFeatureRequestId`
   b. **If `linkedIssueId` exists**: Call `mcp__pinkrooster__get_issue_details` with the issue ID
      - If the issue is NOT in a terminal state (Completed/Cancelled/Replaced):
        - Call `mcp__pinkrooster__create_or_update_issue` with `projectId`, `issueId`, and `state: "Completed"`
        - Report: "Auto-completed linked issue {issueId} '{name}'"
   c. **If `linkedFeatureRequestId` exists**: Call `mcp__pinkrooster__get_feature_request_details` with the FR ID
      - If the FR is NOT in a terminal state (Completed/Rejected):
        - Call `mcp__pinkrooster__create_or_update_feature_request` with `projectId`, `featureRequestId`, and `status: "Completed"`
        - Report: "Auto-completed linked feature request {frId} '{name}'"

## Report Cascading State Changes

This is the most important output. Format cascades clearly:

```
## Completed
- {entityId} "{name}" -> Completed

## Cascading State Changes
(If stateChanges array is present and non-empty)

- **Phase auto-complete**: {phaseId} "{name}" -> Completed
  (all tasks in phase reached terminal state)
- **WP auto-complete**: {wpId} "{name}" -> Completed
  (all phases reached terminal state)
- **Auto-unblock**: {entityId} "{name}" -> {newState}
  (blocker {blockerId} completed, restored from Blocked)

## Linked Entity Updates
(If any linked issues/FRs were auto-completed)

- **Issue auto-complete**: {issueId} "{name}" -> Completed
  (linked WP {wpId} completed)
- **FR auto-complete**: {frId} "{name}" -> Completed
  (linked WP {wpId} completed)
```

If no cascading changes occurred, show: "No cascading state changes."

## Suggest Next Steps

- If an auto-unblock occurred: "**{entityId}** was unblocked. Run `/pm-next` to start it."
- If a WP was auto-completed: "Work package complete. Run `/pm-status` to see project progress."
- If nothing was unblocked: "Run `/pm-status` to see updated project progress."
