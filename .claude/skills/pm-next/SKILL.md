---
name: pm-next
description: >-
  Pick up the next highest-priority work item and start implementing it.
  Finds WPs, unscaffolded Issues, and FRs by priority. Auto-scaffolds
  Issues/FRs without work packages, then implements. Use --auto for
  fully autonomous execution until all work is done.
argument-hint: "[entity-type: Wp | Issue | FeatureRequest] [--auto]"
---

# Pick Up Next Work Item

Find the highest-priority actionable item, prepare it for implementation (scaffolding if needed), and execute it.

## Step 1: Parse Arguments & Resolve Project

Parse `$ARGUMENTS`:
- **`--auto`**: Fully autonomous mode — no prompts, auto-select, auto-scaffold, auto-commit per WP. Remove the flag before further parsing.
- **Entity type filter**: Optional `Wp`, `Issue`, or `FeatureRequest`. Default: omit (returns all types mixed by priority).

Resolve project:
- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` → extract `projectId`

Initialize: `completedItems = []`, `skippedItems = []`

## Step 2: Find Next Item

Call `mcp__pinkrooster__get_next_actions` with:
- `projectId` from Step 1
- `limit`: 5
- `entityType`: the specified filter, or **omit entirely** to get all actionable WPs, Issues (without linked WPs), and FRs (without linked WPs), sorted by priority

This returns a **queue** of candidates. Process them in order (Steps 3–6). If a candidate is skipped (blocked, scaffold failure), try the next one in the queue before re-fetching.

**If empty queue**:
- Auto mode → jump to Step 7 (Exit)
- Interactive → "No actionable items found. Run `/pm-status` for overview."

### Select

- **Auto mode**: Take #1 from the queue. No prompt.
- **Interactive**: Present the queue and let the user pick (default #1, auto-select if only one).

```
## Next Actions

1. [{priority}] {entityId} "{name}" ({state}) — {type}
2. ...

Which item? (number, or Enter for #1)
```

## Step 3: Normalize to Work Package

Every item must become a WP before implementation. This step resolves the selected item to a concrete WP with phases and tasks.

### If WP (`proj-N-wp-N`)

1. Call `mcp__pinkrooster__get_work_package_details` with the WP ID
2. **Blocked check**: If `blockedBy` contains any non-terminal WP:
   - Auto mode → add to `skippedItems` with reason "blocked by {blockerId}", try next candidate from Step 2 queue
   - Interactive → "Blocked by {blockerId}. Work on the blocker first, or pick a different item."
3. Load linked Issues (if `linkedIssueIds` is non-empty): call `mcp__pinkrooster__get_issue_details` for each
4. Load linked FRs (if `linkedFeatureRequestIds` is non-empty): call `mcp__pinkrooster__get_feature_request_details` for each — note user stories for context
5. Proceed to Step 4 with this WP.

### If Issue (`proj-N-issue-N`)

1. Call `mcp__pinkrooster__get_issue_details`
2. **If linked WPs exist**: pick the first non-terminal WP → load its details → proceed as WP path above (skip steps 1-2, go straight to blocked check)
3. **If no linked WP**: auto-scaffold:
   - Invoke `/pm-scaffold {issueId}`
   - **In auto mode**: scaffold must proceed without prompting (skip quality warnings for sparse data)
   - On success: extract the new WP ID → load its details → proceed to Step 4
   - On failure: auto mode → add to `skippedItems` with reason "scaffold failed", try next candidate. Interactive → report error.

### If Feature Request (`proj-N-fr-N`)

1. Call `mcp__pinkrooster__get_feature_request_details` — note user stories
2. **If linked WPs exist**: pick the first non-terminal WP → load its details → proceed as WP path above
3. **If no linked WP**: auto-scaffold:
   - Invoke `/pm-scaffold {frId}`
   - **In auto mode**: scaffold must proceed without prompting (skip quality warnings for sparse data)
   - On success: extract the new WP ID → load its details → proceed to Step 4
   - On failure: auto mode → add to `skippedItems` with reason "scaffold failed", try next candidate. Interactive → report error.

> **Note**: If all candidates in the queue are exhausted (all blocked or failed), auto mode re-fetches in Step 2. If re-fetch also returns nothing actionable → Step 7 (Exit).

## Step 4: Activate Entities

We now have a resolved WP. Activate all inactive entities without prompting.

1. **WP** (if NotStarted or Blocked): call `mcp__pinkrooster__create_or_update_work_package` with `state: "Implementing"` → report "{wpId} → Implementing"
2. **Linked Issues** (for each in `linkedIssueIds`, if NotStarted or Blocked): call `mcp__pinkrooster__create_or_update_issue` with `state: "Implementing"` → report "Auto-activated {issueId} → Implementing"
3. **Linked FRs** (for each in `linkedFeatureRequestIds`, if Proposed or Deferred): call `mcp__pinkrooster__create_or_update_feature_request` with `status: "InProgress"` → report "Auto-activated {frId} → InProgress"

Brief summary:

```
## Implementing: {wpId} "{wpName}"
- Priority: {priority} | Complexity: {estimatedComplexity}/10 | Type: {wpType}
- Tasks: {nonTerminalCount} remaining across {phaseCount} phases
- Linked: {issueId and/or frId if any}
- User Stories: (if FR has user stories)
  - As a {role}, I want {goal}, so that {benefit}
```

## Step 5: Implement

Invoke `/pm-implement {wpId}` to execute all tasks across all phases.

`/pm-implement` autonomously handles:
- Task dependency ordering and sequential execution
- Reading target files, implementing code changes
- Building and running tests after each task (with auto-fix on failure)
- Transitioning tasks to Implementing → Completed
- Phase verification gates (acceptance criteria checks between phases)
- Phase/WP auto-completion via state cascades
- Auto-completion of linked Issue and FR when the WP cascades to Completed

### When `/pm-implement` returns

Check the WP's final state by calling `mcp__pinkrooster__get_work_package_details`:

**WP is Completed** (normal case — all tasks done, cascades fired):
- Add to `completedItems`
- Proceed to Step 6 (Commit)

**WP is NOT Completed** (some tasks were blocked by external deps, or a phase verification gate failed):
- Commit whatever changes were made (partial work is still valuable)
- Auto mode → add to `skippedItems` with reason "incomplete: {details}", proceed to Step 6 then loop back to Step 2
- Interactive → report status and suggest: "Continue with `/pm-implement {wpId}`, or `/pm-next` for different work."

**Build/test failures that `/pm-implement` could not resolve**:
- This is the one **hard stop** in auto mode. The agent could not fix the code.
- Commit changes so far (even if broken — the user needs to see the attempt)
- Report the failure clearly with test output and what was tried
- **Exit the auto loop**. User must intervene.

## Step 6: Commit

After each WP (completed or partial), commit all changes:

1. Stage all files changed during this WP's implementation
2. Create a commit using conventional format:

```
<type>(<scope>): <summary of what was implemented>

Implements {wpId} "{wpName}".
{Linked: issueId "issueName" (if any)}
{Linked: frId "frName" (if any)}

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
```

Commit type by WP type: `feat` (Feature), `fix` (BugFix), `refactor` (Refactor), `chore` (Chore/Spike).

If no files were changed (e.g., all tasks were skipped), skip the commit.

## Step 7: Loop or Exit

### Auto mode

**If the WP completed or was skipped** (not a hard failure): loop back to Step 2.

Report between iterations:
```
---
## Auto-loop: {completedItems.length} completed, {skippedItems.length} skipped. Checking for more...
---
```

**Exit conditions** (any of these ends the loop):

| Condition | Output |
|-----------|--------|
| No actionable items from Step 2 | "No more actionable items." |
| All candidates blocked/failed | "All remaining items are blocked or failed to scaffold." |
| Hard build/test failure | "Stopping: unresolvable build/test failure in {wpId}." |

On exit, print the session summary:

```
## Auto-loop Complete

### Completed ({completedItems.length})
- {wpId} "{wpName}" (feat/fix/refactor)
- ...

### Skipped ({skippedItems.length})
- {entityId} "{name}" — {reason}
- ...

### Project State
Run `/pm-status` for full overview.
```

### Interactive mode

"Work package complete. Run `/pm-next` for more or `/pm-status` for overview."

---

## Auto Mode Contract

`--auto` guarantees a hands-off experience. The agent follows these rules:

1. **Never prompt the user** — every decision point has an auto-mode default
2. **Skip, don't stall** — blocked items, scaffold failures, and incomplete WPs are skipped. They'll resurface in future iterations when conditions change.
3. **Only stop on unresolvable failures** — build/test errors that can't be auto-fixed are the single hard stop. Everything else is skippable.
4. **Commit after every WP** — keeps work atomic and recoverable, even for partial implementations
5. **Re-fetch every iteration** — completing a WP may auto-unblock other WPs via dependency cascades. Fresh data from `get_next_actions` reflects this.
6. **Priority governs order** — the API returns items sorted by priority. The agent always takes the top actionable item.
7. **Propagate auto-mode to delegated skills** — when invoking `/pm-scaffold` or `/pm-implement`, suppress all interactive prompts (quality warnings, confirmations, "proceed?" questions). Proceed with available data.

## Constraints

- Everything becomes a WP before implementation — Issues/FRs without WPs are auto-scaffolded
- Do NOT call `/pm-verify` separately — `/pm-implement` handles phase verification gates internally
- Do NOT call `/pm-done` separately — task/phase/WP completion happens via cascades inside `/pm-implement`, including auto-completion of linked Issues and FRs
- Tasks are too granular for this skill — use `/pm-implement {taskId}` directly for single-task work
- When delegating to `/pm-scaffold` in auto mode, skip all quality checks and user prompts
