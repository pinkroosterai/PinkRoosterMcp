---
name: pm-next
description: >-
  Pick up the next highest-priority work item and start implementing it.
  Finds WPs, unscaffolded Issues, and FRs by priority. Auto-scaffolds
  Issues/FRs without work packages, then implements. Use --auto for
  fully autonomous execution until all work is done. Use when the user
  says "what's next", "pick up work", "start implementing", "work on
  the next thing", or just "next".
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

Initialize: `completedItems = []`, `skippedItems = []`, `iterationCount = 0`

## Step 2: Find Next Item

Call `mcp__pinkrooster__get_next_actions` with:
- `projectId` from Step 1
- `limit`: 10 (fetch more candidates for smarter selection)
- `entityType`: the specified filter, or **omit entirely** to get all actionable WPs, Issues (without linked WPs), and FRs (without linked WPs), sorted by priority

This returns a **queue** of candidates.

### Smart Selection

Don't blindly take the #1 item. Evaluate the queue to find the most impactful item to work on:

1. **Blockers first**: If any item in the queue is blocking other items (check `blocking` field on WP details), prefer it over higher-priority leaf items. Completing a blocker unblocks downstream work, which has a multiplier effect on project progress.

2. **Ready WPs over unscaffolded entities**: A WP with phases and tasks is ready to implement immediately. An Issue or FR without a linked WP needs scaffolding first (extra overhead). When priorities are equal, prefer the ready WP.

3. **Priority as tiebreaker**: When impact is equal, use the API's priority ordering (Critical > High > Medium > Low).

In practice, this means: scan the top 5 candidates, check if any are blockers (call `mcp__pinkrooster__get_work_package_details` on WP candidates to check their `blocking` field), and select accordingly. If no blockers, fall through to priority ordering.

**If empty queue**:
- Auto mode → jump to Step 7 (Exit)
- Interactive → "No actionable items found. Run `/pm-status` for overview."

### Select

- **Auto mode**: Apply smart selection. No prompt.
- **Interactive**: Present the top candidates and recommendation using the `AskUserQuestion` tool:
  - Question: "Which item would you like to work on next?"
  - Header: "Next item"
  - Options: Build from the queue (up to 4). Mark the recommended item: `[{label: "#1 {entityId}", description: "[{priority}] \"{name}\" ({state}) — {type} ⭐ Recommended: {reason}"}, {label: "#2 {entityId}", description: "[{priority}] \"{name}\" ({state}) — {type}"}, ...]`
  - Explain why the recommended item was chosen (e.g., "blocks 2 other WPs" or "highest priority, ready to implement")
  - If only one candidate, auto-select it without asking.

## Step 3: Normalize to Work Package

Every item must become a WP before implementation. This step resolves the selected item to a concrete WP with phases and tasks.

### If WP (`proj-N-wp-N`)

1. Call `mcp__pinkrooster__get_work_package_details` with the WP ID (if not already loaded from smart selection)
2. **Blocked check**: If `blockedBy` contains any non-terminal WP:
   - Auto mode → add to `skippedItems` with reason "blocked by {blockerId}", try next candidate from Step 2 queue
   - Interactive → use the `AskUserQuestion` tool:
     - Question: "{wpId} is blocked by {blockerId}. What would you like to do?"
     - Header: "Blocked"
     - Options: `[{label: "Work on blocker", description: "Switch to implementing {blockerId} first"}, {label: "Pick different", description: "Go back and choose a different item from the queue"}]`
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

## Step 4: Activate Entities & Show Context

We now have a resolved WP. Activate all inactive entities without prompting, then show the user what they're about to implement.

### Activate

1. **WP** (if NotStarted or Blocked): call `mcp__pinkrooster__create_or_update_work_package` with `state: "Implementing"` → report "{wpId} → Implementing"
2. **Linked Issues** (for each in `linkedIssueIds`, if NotStarted or Blocked): call `mcp__pinkrooster__create_or_update_issue` with `state: "Implementing"` → report "Auto-activated {issueId} → Implementing"
3. **Linked FRs** (for each in `linkedFeatureRequestIds`, if Proposed or Deferred): call `mcp__pinkrooster__create_or_update_feature_request` with `status: "InProgress"` → report "Auto-activated {frId} → InProgress"

### Show Implementation Context

Present enough detail for the user to understand what's about to happen:

```
## Implementing: {wpId} "{wpName}"

### Overview
- Priority: {priority} | Complexity: {estimatedComplexity}/10 | Type: {wpType}
- Tasks: {nonTerminalCount} remaining across {phaseCount} phases
- Linked: {issueId and/or frId if any}

### Phase Breakdown
**Phase 1: {phaseName}** — {taskCount} tasks
  {task1Name} → {task2Name} → ... (dependency chain)

**Phase 2: {phaseName}** — {taskCount} tasks
  ...

### User Stories (if FR linked)
- As a {role}, I want {goal}, so that {benefit}
- ...

### Selection Rationale
- {why this item was chosen: "Highest priority", "Unblocks proj-1-wp-4 and proj-1-wp-5", etc.}
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

Increment `iterationCount`. Report progress between iterations:

```
---
## Auto-loop iteration {iterationCount}: {completedItems.length} completed, {skippedItems.length} skipped. Checking for more...
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
| # | WP | Name | Type | Commit |
|---|-----|------|------|--------|
| 1 | {wpId} | {wpName} | feat/fix/refactor | {short commit hash} |
| ... |

### Skipped ({skippedItems.length})
| # | Entity | Name | Reason |
|---|--------|------|--------|
| 1 | {entityId} | {name} | {reason} |
| ... |

### Session Stats
- Iterations: {iterationCount}
- Items completed: {completedItems.length}
- Items skipped: {skippedItems.length}

### Next Steps
- `/pm-status` — full project overview
- `/pm-triage` — review priorities and blocked items
- `/pm-next --auto` — resume autonomous work (after fixing any blockers)
```

### Interactive mode

Use the `AskUserQuestion` tool:
- Question: "Work package complete. What would you like to do next?"
- Header: "Continue?"
- Options: `[{label: "Next item", description: "Pick up the next priority item: /pm-next (Recommended)"}, {label: "Status", description: "Check project progress: /pm-status"}, {label: "Done", description: "Stop here for now"}]`

---

## Auto Mode Contract

`--auto` guarantees a hands-off experience. The agent follows these rules:

1. **Never prompt the user** — every decision point has an auto-mode default
2. **Skip, don't stall** — blocked items, scaffold failures, and incomplete WPs are skipped. They'll resurface in future iterations when conditions change.
3. **Only stop on unresolvable failures** — build/test errors that can't be auto-fixed are the single hard stop. Everything else is skippable.
4. **Commit after every WP** — keeps work atomic and recoverable, even for partial implementations
5. **Re-fetch every iteration** — completing a WP may auto-unblock other WPs via dependency cascades. Fresh data from `get_next_actions` reflects this.
6. **Smart ordering governs selection** — blockers first, ready WPs over unscaffolded entities, then priority as tiebreaker
7. **Propagate auto-mode to delegated skills** — when invoking `/pm-scaffold` or `/pm-implement`, suppress all interactive prompts (quality warnings, confirmations, "proceed?" questions). Proceed with available data.

## Constraints

- Everything becomes a WP before implementation — Issues/FRs without WPs are auto-scaffolded
- Do NOT call `/pm-verify` separately — `/pm-implement` handles phase verification gates internally
- Do NOT call `/pm-done` separately — task/phase/WP completion happens via cascades inside `/pm-implement`, including auto-completion of linked Issues and FRs
- Tasks are too granular for this skill — use `/pm-implement {taskId}` directly for single-task work
- When delegating to `/pm-scaffold` in auto mode, skip all quality checks and user prompts
- Smart selection should not add significant latency — check at most 3-5 WPs for blocker status, don't deep-dive every candidate
