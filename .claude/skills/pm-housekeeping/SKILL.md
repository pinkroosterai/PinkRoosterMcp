---
name: pm-housekeeping
description: >-
  Identify and remove stale, cancelled, rejected, or superseded PinkRooster
  entities (issues, feature requests, work packages). Keeps the project board
  focused on active, relevant work. Use when the user says "housekeeping",
  "remove stale items", "clean the board", "delete cancelled items",
  "prune old entities", or "tidy up the project".
argument-hint: "[--dry-run]"
---

# Project Board Housekeeping

Scan a project for stale, cancelled, rejected, or superseded entities and safely
remove them after user confirmation. Keeps project boards focused on active,
relevant work.

For **codebase** cleanup (dead code, unused imports), use `/pm-cleanup` instead.

## Step 0: Parse Arguments

Parse `$ARGUMENTS` for flags:

- **`--dry-run`**: Show candidates without deleting anything
- If no arguments, run in normal mode (with confirmation before deletion)

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- Note the counts for context (issues total, FRs total, WPs total)

## Step 2: Load All Items

Make these calls in parallel to get all items across all states:

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

### Category 4: Stale Items
- WPs with tasks marked `Implementing` for >14 days with no updates
  (call `mcp__pinkrooster__get_work_package_details` to check task timestamps)
- Issues in `Implementing` state with no linked WP and no updates for >14 days

### Exclusions
- Never suggest deleting Projects
- Never suggest deleting active or inactive (in-progress) items unless stale (Category 4)
- Never suggest deleting items that are blocking other non-terminal items
- Never suggest deleting items with linked non-terminal WPs

## Step 4: Present Candidates

```
## Housekeeping Candidates — {projectId}

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
```

**If no candidates found**:
"No housekeeping candidates found. Project board is clean."

**If `--dry-run`**:
"Dry run complete. {candidateCount} items would be eligible for removal. Run without `--dry-run` to proceed."

**Otherwise**: Use `AskUserQuestion`:
- Question: "Which items should I delete?"
- Header: "Delete"
- multiSelect: true
- Options: Build from candidates (up to 4):
  `[{label: "#1 {id}", description: "{name} ({type}, {state}) — {reason}"},
    ...,
    {label: "All ({N})", description: "Delete all {N} candidates"},
    {label: "None", description: "Cancel — keep all items"}]`

If more than 4 candidates, batch the options:
  - "All Cancelled + Replaced ({N})" (safe removals)
  - "All candidates ({N})"
  - "Let me pick individually"
  - "None"

## Step 5: Delete Selected Items

For each selected item, call `mcp__pinkrooster__delete_entity` with:
- `entityType`: `Issue`, `FeatureRequest`, or `WorkPackage`
- `entityId`: the item's composite ID (e.g., `proj-1-issue-3`, `proj-1-fr-2`, `proj-1-wp-1`)

Collect results (success/failure) for each deletion.

## Step 6: Report Results

```
## Housekeeping Complete — {projectId}

### Deleted {count} items
| # | ID | Name | Type | Result |
|---|-----|------|------|--------|
| 1 | {id} | {name} | Issue/FR/WP | Deleted |
| ... |

### Project After Housekeeping
- Issues: {newCount} (was {oldCount})
- Feature Requests: {newCount} (was {oldCount})
- Work Packages: {newCount} (was {oldCount})

### Next Steps
- View project status: `/pm-status`
- Triage remaining items: `/pm-triage`
- Clean up codebase debt: `/pm-cleanup`
- Start next work item: `/pm-next`
```

If "None" was selected:
"No items deleted. All candidates remain for future review."

## Constraints

- ALWAYS confirm before deletion — never delete without user approval
- Never delete Projects (too destructive, cascades to everything)
- Never delete active or in-progress items unless flagged as stale (>14 days no update)
- Never delete items that are blocking other non-terminal items
- Always warn that WP deletion cascades to phases and tasks
- Always warn that Issue/FR deletion clears WP links
- `--dry-run` shows candidates without prompting for deletion
- If an item fails to delete, report the error and continue with remaining items
