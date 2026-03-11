---
name: pm-cleanup
description: >-
  Identify and remove stale, cancelled, or rejected items from a project.
  Scans for cleanup candidates, presents them for confirmation, and safely
  deletes selected items.
argument-hint: "[--dry-run]"
---

# Project Cleanup

Scan a project for stale, cancelled, rejected, or superseded items and safely remove them
after user confirmation. Keeps project boards focused on active, relevant work.

## Step 0: Parse Arguments

Parse `!arguments` for flags:

- **`--dry-run`**: If present, show cleanup candidates without deleting anything
- If no arguments, run in normal mode (with confirmation before deletion)

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- Note the counts for context

## Step 2: Load All Items

Make these calls to get all items across all states:

1. `mcp__pinkrooster__get_issue_overview` with `projectId` (no filter — all states)
2. `mcp__pinkrooster__get_feature_requests` with `projectId` (no filter — all states)
3. `mcp__pinkrooster__get_work_packages` with `projectId` (no filter — all states)

## Step 3: Identify Cleanup Candidates

Scan all items and categorize cleanup candidates by reason:

### Category 1: Cancelled Items
- Work packages in `Cancelled` state
- Issues in `Cancelled` state

### Category 2: Rejected/Deferred FRs
- Feature requests in `Rejected` status
- Feature requests in `Deferred` status older than 30 days

### Category 3: Replaced Items
- Work packages in `Replaced` state
- Issues in `Replaced` state

### Category 4: Orphaned Terminal Items
- Completed work packages with no linked Issue or FR (optional — flag but don't auto-suggest)

### Exclusions
- Never suggest deleting Projects
- Never suggest deleting active or inactive (in-progress) items
- Never suggest deleting items that are blocking other non-terminal items

## Step 4: Present Candidates

```
## Cleanup Candidates — {projectId}

**Scanned**: {issueCount} issues, {frCount} FRs, {wpCount} WPs
**Candidates found**: {candidateCount}

| # | ID | Name | Type | State/Status | Reason |
|---|-----|------|------|-------------|--------|
| 1 | {id} | {name} | Issue/FR/WP | {state} | {reason} |
| 2 | {id} | {name} | Issue/FR/WP | {state} | {reason} |
| ... |

### Warnings
- Deleting a WP also deletes all its phases and tasks
- Deleting an Issue/FR clears links from associated WPs (WPs are NOT deleted)

Select items to delete:
- Enter numbers (e.g., "1, 3, 5") or "all" to delete all
- Enter "none" to cancel
```

If no candidates found:
"No cleanup candidates found. Project is clean."

If `--dry-run`:
"Dry run complete. {candidateCount} items would be eligible for cleanup. Run without --dry-run to proceed."

## Step 5: Delete Selected Items

For each selected item, call `mcp__pinkrooster__delete_entity` with:
- `entityType`: `Issue`, `FeatureRequest`, or `WorkPackage` (matching the item type)
- `entityId`: the item's ID (e.g., `proj-1-issue-3`, `proj-1-fr-2`, `proj-1-wp-1`)

Collect results (success/failure) for each deletion.

## Step 6: Report Results

```
## Cleanup Complete — {projectId}

### Deleted {count} items
| # | ID | Name | Type | Result |
|---|-----|------|------|--------|
| 1 | {id} | {name} | Issue/FR/WP | Deleted |
| ... |

### Project After Cleanup
- Issues: {newCount} (was {oldCount})
- Feature Requests: {newCount} (was {oldCount})
- Work Packages: {newCount} (was {oldCount})

### Next Steps
- View project status: `/pm-status`
- Triage remaining items: `/pm-triage`
- Discover new feature opportunities: `/pm-explore`
- Start next work item: `/pm-next`
```

If "none" was selected:
"No items deleted. All candidates remain for future review."

## Constraints

- ALWAYS confirm before deletion — never delete without user approval
- Never delete Projects (too destructive, cascades to everything)
- Never delete active or in-progress items
- Always warn that WP deletion cascades to phases and tasks
- Always warn that Issue/FR deletion clears WP links
- `--dry-run` shows candidates without prompting for deletion
- If an item fails to delete, report the error and continue with remaining items
