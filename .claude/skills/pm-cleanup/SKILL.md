---
name: pm-cleanup
description: >-
  Two-mode cleanup: (1) Codebase mode — analyze code for dead code, unused imports,
  inconsistencies, and structural debt, then scaffold a work package with cleanup tasks.
  (2) Project mode — identify and remove stale, cancelled, or rejected PinkRooster
  entities. Use when the user says "clean up", "remove dead code", "tidy up",
  "clean the project", or "remove stale items".
argument-hint: "[--code] [--project] [--dry-run] [--scope path/to/dir]"
---

# Project & Codebase Cleanup

Two complementary cleanup modes that keep both the codebase and the project board clean.
By default, runs both modes. Use flags to run one at a time.

## Step 0: Parse Arguments

Parse `$ARGUMENTS` for flags:

- **`--code`**: Run codebase cleanup only (dead code, unused imports, structural issues)
- **`--project`**: Run project board cleanup only (stale entities in PinkRooster)
- **`--dry-run`**: Show findings/candidates without creating WPs or deleting entities
- **`--scope <path>`**: Limit codebase analysis to a specific directory (e.g., `src/PinkRooster.Api`)
- **No flags**: Run both modes sequentially (codebase first, then project board)

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- Note the counts for context

## Step 2: Load Existing Issues (Deduplication)

Load existing issues to avoid creating duplicate cleanup tasks:

1. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Active"`
2. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Inactive"`

Compile a deduplication list of existing issue names and descriptions.

---

# MODE A: Codebase Cleanup (runs when `--code` or no flag)

## Step 3A: Launch Codebase Analysis Agent

Spawn an Explore agent with `subagent_type: "Explore"` and thoroughness "very thorough":

```
Analyze the codebase at {project_path}{scope_suffix} for cleanup opportunities.

Focus on these categories:

### 1. Dead Code
- Unused private methods, fields, properties, or classes
- Methods defined on interfaces but never called (check all implementations)
- Unreachable code paths (after unconditional returns, dead branches)
- Commented-out code blocks (>3 lines)
- Unused enum values or constants

### 2. Unused Imports & Dependencies
- Using directives that don't resolve to any symbol in the file
- NuGet/npm packages in project files not referenced by any source file
- Orphaned configuration entries (appsettings keys never read)

### 3. Inconsistent Patterns
- Mixed approaches to the same problem (some files use pattern A, others pattern B)
- Naming convention violations (methods, variables, files not matching project style)
- Duplicated utility code that should be consolidated

### 4. Structural Debt
- Empty files, empty classes, or stub implementations left behind
- Overly large files (>500 lines) that should be split
- Misplaced code (files in wrong directory per project architecture)
- Obsolete test fixtures or mock data

For each finding, report:
1. File path and line number(s)
2. What should be cleaned up (specific, not vague)
3. Category: DeadCode, UnusedImports, Inconsistency, StructuralDebt
4. Effort: Trivial (1-line fix), Small (under 10 lines), Medium (multi-file)
5. Safety: Safe (zero risk), NeedsReview (might affect behavior), Risky (public API)
6. A concrete fix description

Skip trivial style issues a linter handles. Focus on things that reduce maintenance
burden, eliminate confusion, or improve code health.
```

## Step 4A: Collect, Verify, and Deduplicate Findings

When the agent completes:

1. **Verify Critical Findings**: For any finding marked Medium effort or NeedsReview/Risky
   safety, read the actual code to confirm the issue is real. Remove false positives.

2. **Cross-reference with existing issues**: Check each finding against the deduplication
   list from Step 2. Skip findings already tracked.

3. **Filter noise**: Remove findings that are:
   - Too trivial to track (single unused import in one file)
   - Already handled by existing linter rules or CI
   - False positives (the code IS used via reflection, DI, or convention)

4. **Group by action**: Cluster related findings into logical cleanup tasks. For example:
   - "Remove 5 unused using directives across API controllers" → 1 task
   - "Delete 3 dead helper methods in StateTransitionHelper" → 1 task
   - "Consolidate duplicate validation logic in IssueService and FRService" → 1 task

   Each group becomes one task in the scaffolded WP. Target 3-8 tasks total — not one
   task per finding, but not one mega-task for everything either.

5. **Sort by impact**: Safe + Trivial/Small first (quick wins), then Medium, then Risky last.

## Step 5A: Present Findings and Get Confirmation

```
## Codebase Cleanup Analysis

**Scope**: {scope or "entire project"}
**Findings**: {totalCount} across {categoryCount} categories

### Cleanup Tasks (grouped)

| # | Task | Files | Category | Effort | Safety |
|---|------|-------|----------|--------|--------|
| 1 | {grouped task description} | {file count} | {category} | {effort} | {safety} |
| 2 | ... | | | | |

### Skipped ({count})
- {finding} — already tracked as {existingIssueId}
- {finding} — false positive
```

**If `--dry-run`**: Show the table and stop.
"Dry run complete. {taskCount} cleanup tasks identified. Run without `--dry-run` to scaffold a work package."

**Otherwise**: Use `AskUserQuestion`:
- Question: "Which cleanup tasks should I scaffold into a work package?"
- Header: "Scaffold cleanup"
- Options:
  - "All Safe tasks ({N})" — only Safe findings (Recommended)
  - "All tasks ({N})" — includes NeedsReview items
  - "Let me pick" — then ask per-task
  - "None — just keep the report"

## Step 6A: Scaffold Cleanup Work Package

For confirmed tasks, call `mcp__pinkrooster__scaffold_work_package` with:

- `projectId`
- `name`: "Codebase cleanup: {scope or 'project-wide'}"
- `description`: Summary of what will be cleaned and why
- `type`: `Chore`
- `priority`: `Low`
- `estimatedComplexity`: Based on total effort (1-3 for mostly trivial, 4-6 for mixed)
- `estimationRationale`: "{N} cleanup tasks across {M} files, mostly {effort level}"
- `phases`: Single phase "Cleanup" with:
  - Each grouped finding becomes one task with:
    - `name`: Concise action (e.g., "Remove unused helper methods in StateCascadeService")
    - `description`: What to remove/change and why
    - `implementationNotes`: Specific file paths, line numbers, and what to do
    - `targetFiles`: Actual file paths from the analysis
  - Acceptance criteria:
    - "Solution builds with zero errors after cleanup"
    - "All existing tests pass unchanged"
    - "No new warnings introduced"

Report the scaffolded WP:
```
## Scaffolded: {wpId} "Codebase cleanup"

- **Tasks**: {count}
- **Files affected**: {count}
- **Estimated effort**: {complexity}/10

### Next Steps
- Implement cleanup: `/pm-implement {wpId}` or `/pm-next`
- View details: `/pm-status`
```

---

# MODE B: Project Board Cleanup (runs when `--project` or no flag)

## Step 3B: Load All Items

Make these calls to get all items across all states:

1. `mcp__pinkrooster__get_issue_overview` with `projectId` (no filter — all states)
2. `mcp__pinkrooster__get_feature_requests` with `projectId` (no filter — all states)
3. `mcp__pinkrooster__get_work_packages` with `projectId` (no filter — all states)

## Step 4B: Identify Cleanup Candidates

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
- Never suggest deleting active or inactive (in-progress) items unless stale
- Never suggest deleting items that are blocking other non-terminal items
- Never suggest deleting items with linked non-terminal WPs

## Step 5B: Present Candidates

```
## Project Board Cleanup — {projectId}

**Scanned**: {issueCount} issues, {frCount} FRs, {wpCount} WPs
**Candidates found**: {candidateCount}

| # | ID | Name | Type | State/Status | Reason |
|---|-----|------|------|-------------|--------|
| 1 | {id} | {name} | Issue/FR/WP | {state} | {reason} |
| ... |

### Warnings
- Deleting a WP also deletes all its phases and tasks
- Deleting an Issue/FR clears links from associated WPs (WPs are NOT deleted)
```

**If `--dry-run`**: Show the table and stop.

**If no candidates**: "No cleanup candidates found. Project board is clean."

**Otherwise**: Use `AskUserQuestion`:
- Question: "Which items should I delete?"
- Header: "Delete"
- multiSelect: true
- Options: Build from candidates (up to 4):
  `[{label: "#1 {id}", description: "{name} ({type}, {state}) — {reason}"},
    ...,
    {label: "All ({N})", description: "Delete all cleanup candidates"},
    {label: "None", description: "Cancel — keep all items"}]`

## Step 6B: Delete Selected Items

For each selected item, call `mcp__pinkrooster__delete_entity` with:
- `entityType`: `Issue`, `FeatureRequest`, or `WorkPackage`
- `entityId`: the item's composite ID

Collect results (success/failure) for each deletion.

## Step 7B: Report Results

```
## Project Board Cleanup Complete

### Deleted {count} items
| # | ID | Name | Type | Result |
|---|-----|------|------|--------|
| 1 | {id} | {name} | {type} | Deleted |
| ... |

### Project After Cleanup
- Issues: {newCount} (was {oldCount})
- Feature Requests: {newCount} (was {oldCount})
- Work Packages: {newCount} (was {oldCount})
```

---

# Step 8: Combined Summary (when both modes ran)

```
## Cleanup Complete — {projectId}

### Codebase Cleanup
- Scaffolded: {wpId} with {taskCount} cleanup tasks
- Start implementing: `/pm-implement {wpId}`

### Project Board Cleanup
- Deleted: {deletedCount} stale entities
- Remaining: {issueCount} issues, {frCount} FRs, {wpCount} WPs

### Next Steps
- Implement cleanup tasks: `/pm-implement {wpId}` or `/pm-next`
- View project status: `/pm-status`
- Triage remaining items: `/pm-triage`
- Re-run cleanup: `/pm-cleanup`
```

## Constraints

- **Codebase mode**: Never directly modify code — always scaffold a WP so changes go
  through the normal implement → test → commit pipeline via `/pm-implement`
- **Project mode**: ALWAYS confirm before deletion — never delete without user approval
- Never delete Projects (too destructive, cascades to everything)
- Never delete active/in-progress items unless flagged as stale (>14 days no update)
- Always warn that WP deletion cascades to phases and tasks
- Always warn that Issue/FR deletion clears WP links
- `--dry-run` shows findings/candidates without creating WPs or deleting entities
- Verify findings before including them — false positives waste implementation time
- Group related findings into logical tasks (3-8 tasks per WP, not 1:1 with findings)
- Maximum 10 tasks per cleanup WP — if more findings exist, note them for a follow-up run
- If an entity deletion fails, report the error and continue with remaining items
