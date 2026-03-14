---
name: pm-cleanup
description: >-
  Analyze codebase for dead code, unused imports, inconsistencies, and structural
  debt, then scaffold a work package with cleanup tasks. Never modifies code
  directly — changes flow through the normal implement/test/commit pipeline.
  Use when the user says "clean up the code", "remove dead code", "tidy up",
  "find unused code", "code hygiene", or "clean up imports".
argument-hint: "[--dry-run] [--scope path/to/dir]"
---

# Codebase Cleanup

Analyze the codebase for cleanup opportunities — dead code, unused imports,
inconsistencies, and structural debt — then scaffold a tracked work package so
fixes go through the normal `/pm-implement` pipeline with tests and commits.

The key difference from `/pm-audit`: audit finds **bugs and vulnerabilities** that
create Issues. Cleanup finds **hygiene debt** that creates a single Chore WP with tasks.

## Step 0: Parse Arguments

Parse `$ARGUMENTS` for flags:

- **`--dry-run`**: Show findings without scaffolding a WP
- **`--scope <path>`**: Limit analysis to a specific directory (e.g., `src/PinkRooster.Api`)
- If the user provides a plain description like "clean up the MCP tools", infer
  `--scope src/PinkRooster.Mcp`

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`

## Step 2: Load Existing Issues (Deduplication)

Load existing issues to avoid creating duplicate cleanup tasks:

1. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Active"`
2. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Inactive"`

Compile a deduplication list of existing issue names and descriptions.

## Step 3: Launch Codebase Analysis Agent

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

## Step 4: Collect, Verify, and Deduplicate Findings

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

## Step 5: Present Findings and Get Confirmation

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

**If no findings**: "Codebase is clean — no cleanup opportunities found in {scope or 'project'}."

**Otherwise**: Use `AskUserQuestion`:
- Question: "Which cleanup tasks should I scaffold into a work package?"
- Header: "Scaffold cleanup"
- Options:
  - "All Safe tasks ({N})" — only Safe findings (Recommended)
  - "All tasks ({N})" — includes NeedsReview items
  - "Let me pick" — then ask per-task
  - "None — just keep the report"

## Step 6: Scaffold Cleanup Work Package

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
    - `{name: "Solution builds", description: "dotnet build PinkRooster.slnx completes with 0 errors after all cleanup tasks.", verificationMethod: "AutomatedTest"}`
    - `{name: "Tests pass unchanged", description: "All existing tests pass without modification — cleanup must not change behavior.", verificationMethod: "AutomatedTest"}`
    - `{name: "No new warnings", description: "Build warning count does not increase after cleanup.", verificationMethod: "AgentReview"}`

## Step 7: Report

```
## Scaffolded: {wpId} "Codebase cleanup"

- **Tasks**: {count}
- **Files affected**: {count}
- **Estimated effort**: {complexity}/10

### Task Summary
| # | Task ID | Name | Category | Effort |
|---|---------|------|----------|--------|
| 1 | {taskId} | {name} | {category} | {effort} |
| ... |

### Next Steps
- Implement cleanup: `/pm-implement {wpId}` or `/pm-next`
- View project status: `/pm-status`
- Clean up stale project entities: `/pm-housekeeping`
```

## Constraints

- **Never directly modify code** — always scaffold a WP so changes go through the
  normal implement → test → commit pipeline via `/pm-implement`
- Verify findings before including them — false positives waste implementation time
- Group related findings into logical tasks (3-8 tasks per WP, not 1:1 with findings)
- Maximum 10 tasks per cleanup WP — if more findings exist, note them for a follow-up run
- `--dry-run` shows findings without scaffolding
- A clean codebase should produce few or no tasks — don't manufacture work to fill a quota
- Skip trivial style issues that linters handle (formatting, trailing whitespace, etc.)
