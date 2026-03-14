---
name: pm-plan
description: >-
  Plan new work by creating issues or feature requests from a natural language
  description. Researches the domain online, analyzes the codebase for context,
  and decomposes complex descriptions into multiple entities when sensible.
  Use when the user describes work needed, a bug to fix, a feature to add,
  or says things like "we need...", "there's a problem with...", "I want to add...",
  "plan out...", or "let's track...". Also triggers for vague requests that need
  clarification before becoming actionable work items.
argument-hint: <description of work needed>
---

# Plan Work from Description

Transform a natural language description into well-structured tracking entities (issues and/or feature requests). The goal is to produce entities that are detailed enough for `/pm-scaffold` to generate realistic work packages from — so invest in understanding the problem space before creating anything.

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- If no project found, offer to register it first

## Step 2: Understand What Already Exists

Before asking questions or creating anything, build context from two sources:

### 2a: Codebase Analysis (proportional to complexity)

Calibrate the depth of exploration to the request:

**Simple bug reports** (clear error message, known location, reproduction steps): Quick lookup — a single Grep/Glob to confirm the affected file exists. Spend under a minute here. The goal is just to populate `affectedComponent` accurately, not to diagnose the root cause.

**Moderate requests** (CRUD addition, extend existing feature): Targeted exploration — find the existing pattern to follow, note the relevant files and conventions. A few Grep/Glob calls.

**Complex features** (new system, integration, cross-cutting concern): Deep exploration — understand the relevant architectural layers, existing infrastructure that can be reused, and how the feature fits into the current design. Use Grep/Glob and Read to trace through related code.

In all cases:
1. Use Grep/Glob to find files related to `$ARGUMENTS`
2. Identify what already exists — existing implementations inform classification and description quality
3. Note patterns, conventions, and layers involved

### 2b: Existing Tracked Items

Load current items to avoid duplicates and understand project context:

1. Call `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Active"` + `"Inactive"`
2. Call `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Active"` + `"Inactive"`
3. Compile a deduplication list of names, descriptions, and states

If a closely matching item already exists, surface it to the user before creating anything new.

## Step 3: Research the Domain

For non-trivial work — especially new features, integrations, or unfamiliar domains — use online research to inform the planning. This produces better descriptions, more accurate scoping, and catches edge cases early.

**When to research:**
- The description mentions a technology, library, or integration you should verify (e.g., "add OAuth support", "export to Parquet", "integrate with Stripe")
- The work involves patterns or best practices worth confirming (e.g., "add rate limiting", "implement RBAC")
- The user's description is vague about implementation and external context could help clarify options

**When to skip:**
- Pure bug reports with clear reproduction steps
- Simple CRUD additions following existing patterns
- Work that's entirely internal to the existing codebase

**How to research:**
- Use `WebSearch` with targeted queries about the technology/pattern
- Use `WebFetch` to pull relevant documentation pages
- Synthesize findings into actionable context that improves the entity descriptions

Fold research findings into the entity description — mention relevant libraries, standards, common pitfalls, or architectural considerations discovered.

## Step 4: Decompose into Work Items

Analyze `$ARGUMENTS` and determine whether this is a **single item** or **multiple distinct items**.

### Signals that work should be decomposed:

- Conjunctions connecting distinct capabilities: "add X **and** Y", "we need A, B, and C"
- Different affected areas: "fix the login page **and** add export to the dashboard"
- Mixed types: description contains both bugs and features
- Large scope with natural boundaries: "add a notification system" might decompose into "notification infrastructure", "email notifications", "in-app notifications"

### Signals to keep as one item:

- Tightly coupled changes that don't make sense independently
- Single capability described from multiple angles
- Small scope even if touching multiple files

**If decomposing**, present the breakdown to the user for confirmation before creating anything. Use the `AskUserQuestion` tool:
- Question: "I see {N} distinct items in this description. Here's how I'd break them down:\n\n{numbered list with type and name for each}\n\nDoes this look right?"
- Header: "Work breakdown"
- Options: `[{label: "Create all", description: "Create all {N} items as described"}, {label: "Adjust", description: "Let me modify the breakdown first"}, {label: "Keep as one", description: "Create a single item instead"}]`

If the user selects "Adjust", ask what they'd like to change and iterate.

## Step 5: Classify Each Item

For each item (whether single or decomposed), determine the type:

**Issue indicators** (create an Issue):
- Bug, defect, broken, error, crash, regression, failing, wrong, incorrect
- Performance: slow, timeout, latency, memory leak, high CPU
- Security: vulnerability, CVE, injection, XSS, auth bypass
- Technical debt: refactor, cleanup, deprecated, legacy

**Feature Request indicators** (create a Feature Request):
- Feature, add, new, enhance, improve, support, enable, implement
- User stories: "as a...", "I want...", "we need..."
- Capabilities: dashboard, page, export, import, notification, integration

**If ambiguous**, use the `AskUserQuestion` tool:
- Question: "Is '{itemName}' a bug/issue to fix, or a new feature/enhancement?"
- Header: "Work type"
- Options: `[{label: "Bug/Issue", description: "Something broken, slow, or insecure"}, {label: "Feature/Enhancement", description: "A new capability or improvement"}]`

### Map to specific types

**Issue types:**
- Bug/broken/error/crash → IssueType: `Bug`
- Regression/was-working → IssueType: `Regression`
- Slow/timeout/performance → IssueType: `PerformanceIssue`
- Security/vulnerability → IssueType: `SecurityVulnerability`
- Refactor/cleanup/debt → IssueType: `TechnicalDebt`

**Feature categories:**
- New capability → FeatureCategory: `Feature`
- Extends existing → FeatureCategory: `Enhancement`
- Makes existing better → FeatureCategory: `Improvement`

### Map severity (issues only)

- Crash, data loss, security breach → `Critical`
- Broken functionality, major regression → `Major`
- Cosmetic, minor inconvenience → `Minor`
- Typo, trivial cosmetic → `Trivial`

### Map priority (all entities)

- Urgent, ASAP, critical, blocker → `Critical`
- Important, should, high → `High`
- Would be nice, eventually → `Low`
- Default → `Medium`

## Step 6: Clarify Gaps

After classification but before creation, check if anything important is missing or ambiguous. Ask concisely — combine related questions into a single `AskUserQuestion` call rather than asking one at a time.

**Worth clarifying:**
- **Scope ambiguity**: "improve the dashboard" → which aspect? Performance? UI? New features?
- **Boundary ambiguity**: "add export" → which entities? What formats? Where in the UI?
- **Audience ambiguity**: "make it easier to use" → for whom?
- **Priority conflicts**: description mixes urgency levels

**Not worth asking about:**
- Implementation details that `/pm-scaffold` will figure out from the codebase
- Fields that can be reasonably inferred from context
- Things the user clearly specified already

If the description is clear and specific, skip this step entirely.

## Step 7: Confirm Before Creating

Present the full plan using the `AskUserQuestion` tool:

**For a single item:**
- Question: "Create {type} '{name}' with {priority} priority?"
- Header: "Confirm"
- Options: `[{label: "Create", description: "Proceed"}, {label: "Adjust", description: "Change something first"}, {label: "Cancel", description: "Don't create"}]`

**For multiple items:**
- Question: "Create the following {N} items?\n\n{numbered list with type, name, and priority for each}"
- Header: "Confirm ({N} items)"
- Options: `[{label: "Create all", description: "Create all {N} items"}, {label: "Select", description: "Let me pick which ones to create"}, {label: "Cancel", description: "Don't create anything"}]`

## Step 8: Create Entities

### For Issues

Call `mcp__pinkrooster__create_or_update_issue` with:
- `projectId`
- `name`: concise, descriptive title
- `description`: comprehensive description incorporating codebase context and research findings. Include: what's wrong, where it manifests, impact, and any relevant technical context discovered during analysis
- `issueType`: mapped type
- `severity`: mapped severity
- `priority`: mapped priority
- `affectedComponent`: if identifiable from codebase analysis
- `stepsToReproduce`: if the description includes or implies reproduction steps

### For Feature Requests

Call `mcp__pinkrooster__create_or_update_feature_request` with:
- `projectId`
- `name`: descriptive title following pattern `{Feature}: {qualifier}`
- `description`: comprehensive specification incorporating research findings. Include: what the feature does, why it matters, how it fits with existing functionality, and any technical considerations discovered
- `category`: mapped category
- `priority`: mapped priority
- `businessValue`: derived from the "why" — who benefits and what becomes possible
- `userStories`: array of structured stories extracted from the description `[{"role": "...", "goal": "...", "benefit": "..."}]`. Derive 2-4 stories covering distinct user roles or capabilities. Make them concrete and specific.
- `requester`: "Claude Code" (or user name if known)

### Duplicate and Overlap Handling

Before each create call, check against the deduplication list from Step 2.

**Exact duplicate** (same bug, same feature, same scope): Do not create a new entity. Surface the existing item to the user and suggest next steps (scaffold, refine, or update it).

**Partial overlap** (existing item covers part of the scope, or the new request expands on it): Prefer **updating the existing entity** over creating a new one. Call the update endpoint with `projectId` + the existing entity's ID + expanded fields. This avoids fragmentation — one well-maintained item is better than two overlapping ones.

Use the `AskUserQuestion` tool to confirm:
- Question: "Found {existingId} '{existingName}' ({state}) which overlaps with this request. How should I handle it?"
- Header: "Overlap found"
- Options: `[{label: "Update existing", description: "Expand {existingId} with the new details (recommended)"}, {label: "Create new", description: "Create a separate item — the scope is different enough"}, {label: "Skip", description: "The existing item already covers this"}]`

**No match**: Proceed silently.

## Step 9: Report Results

```
## Created {count} Item(s)

| # | Type | ID | Name | Priority |
|---|------|----|------|----------|
| 1 | {Issue/FR} | {entityId} | {name} | {priority} |
| ... |

### Research Applied
- {key finding 1 from online research, if any}
- {key finding 2, if any}

### Next Steps
- **Refine** (FRs): `/pm-refine-fr {frId}` — add user stories and detail
- **Scaffold**: `/pm-scaffold {entityId}` — create implementation work package
- **Triage**: `/pm-triage` — review priorities across all items
- **Status**: `/pm-status` — project overview
```

## Step 10: Offer Next Steps

Use the `AskUserQuestion` tool:
- Question: "What would you like to do next?"
- Header: "Next step"

Build options based on what was created:
- If single item: offer Scaffold, Refine (FR only), or Done
- If multiple items: offer "Scaffold first item", "Triage all", or Done

**If Scaffold**: Delegate to `/pm-scaffold {entityId}`
**If Refine**: Delegate to `/pm-refine-fr {frId}`
**If Triage**: Delegate to `/pm-triage`
**If Done**: Close with a summary of entity IDs for future reference

## Constraints

- Always confirm classification before creating entities — the user should see what's about to be created
- Derive as much structured data as possible from natural language — don't leave fields empty when they can be inferred
- Report all entity IDs prominently so the user can reference them
- When decomposing, each item should be independently meaningful — don't create items that only make sense together
- Fold research findings into entity descriptions rather than keeping them separate
- Never create entities without first checking for duplicates
- **Prefer updating existing items over creating new ones** when there is overlap — fragmented tracking is worse than a comprehensive single item
- **Proportional effort is critical**: a bug report with clear reproduction steps should take under 30 seconds of analysis (quick file lookup, create issue, done). Reserve deep codebase exploration and online research for complex features and integrations. The user should not wait 3 minutes for a simple bug to be logged.
- If the project has many open items, suggest `/pm-triage` after creation to help prioritize
