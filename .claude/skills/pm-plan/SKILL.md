---
name: pm-plan
description: >-
  Plan new work by creating an issue or feature request from a natural
  language description. Optionally scaffold a work package with phases
  and tasks.
disable-model-invocation: true
argument-hint: <description of work needed>
---

# Plan Work from Description

Take a natural language description of needed work, classify it, create the appropriate tracking entity, and optionally scaffold a work package.

## Step 1: Classify the Work

Analyze `$ARGUMENTS` to determine if this is a bug/defect or a feature/enhancement.

**Issue indicators** (create an Issue):
- Keywords: bug, fix, broken, error, crash, regression, failing, wrong, incorrect
- Performance: slow, timeout, latency, memory leak, high CPU
- Security: vulnerability, CVE, injection, XSS, auth bypass, exploit
- Technical debt: refactor, cleanup, deprecated, debt, legacy

**Feature Request indicators** (create a Feature Request):
- Keywords: feature, add, new, enhance, improve, want, support, enable, implement
- User stories: "as a...", "I want...", "we need..."
- Capabilities: dashboard, page, export, import, notification, integration

**If ambiguous**, ask the user:
"Is this a bug/issue to fix, or a new feature/enhancement to build?"

**Map to specific types**:
- Bug/broken/error/crash → IssueType: `Bug`
- Regression/was-working → IssueType: `Regression`
- Slow/timeout/performance → IssueType: `PerformanceIssue`
- Security/vulnerability → IssueType: `SecurityVulnerability`
- Refactor/cleanup/debt → IssueType: `TechnicalDebt`
- Feature/add/new → FeatureCategory: `Feature`
- Enhance/improve → FeatureCategory: `Enhancement`
- Small improvement → FeatureCategory: `Improvement`

**Map severity** (for issues):
- crash, data loss, security breach → `Critical`
- broken functionality, major regression → `Major`
- cosmetic, minor inconvenience → `Minor`
- typo, trivial cosmetic → `Trivial`

**Map priority** from urgency signals:
- urgent, ASAP, critical, blocker → `Critical`
- important, should, high → `High`
- would be nice, eventually → `Low`
- default → `Medium`

## Step 2: Confirm Classification

Present the classification to the user before creating:

```
## Classification

**Type**: Issue (Bug) / Feature Request (Enhancement) / etc.
**Name**: {derived from description}
**Priority**: {mapped priority}
**Severity**: {mapped severity, if issue}

Proceed with creation? (y/n, or adjust)
```

## Step 3: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- If no project found, offer to register it first

## Step 4: Create Entity

**For Issues**:
Call `mcp__pinkrooster__create_or_update_issue` with:
- `projectId`
- `name`: derived concise title
- `description`: full description from `$ARGUMENTS` plus any inferred details
- `issueType`: mapped type
- `severity`: mapped severity
- `priority`: mapped priority
- `affectedComponent`: if identifiable from the description
- `stepsToReproduce`: if the description includes reproduction steps

**For Feature Requests**:
Call `mcp__pinkrooster__create_or_update_feature_request` with:
- `projectId`
- `name`: derived concise title
- `description`: full description
- `category`: mapped category
- `priority`: mapped priority
- `businessValue`: derived from the "why" in the description
- `userStories`: array of structured user stories extracted from the description, each with `role`, `goal`, `benefit` (e.g., `[{ "role": "developer", "goal": "export data as CSV", "benefit": "offline analysis in spreadsheets" }]`). Derive multiple stories if the description implies distinct user roles or capabilities.
- `requester`: "Claude Code" (or user name if known)

## Step 5: Report and Offer Scaffolding

```
## Created

**{entityType}**: {entityId} "{name}"
- Priority: {priority}
- State: NotStarted / Proposed
- View at: http://localhost:3000/projects/{projNum}/issues/{num} (or feature-requests)

Scaffold a work package with implementation tasks? (y/n)
```

## Step 6: Optional Scaffolding

If the user accepts scaffolding:
1. Analyze the codebase to understand which layers need changes
2. Use Grep/Glob to find related existing files
3. Call `mcp__pinkrooster__scaffold_work_package` with:
   - `projectId`
   - `name`: same as entity name
   - `description`: implementation plan derived from entity
   - `phases`: following the project's vertical slice pattern
   - `linkedIssueId` or `linkedFeatureRequestId`: link to the created entity
   - `priority`: same as entity priority
4. Report the created WP structure

If the user declines:
"Entity created. You can scaffold later with `/pm-scaffold {entityId}`."

## Constraints

- Always confirm classification before creating entities
- Never create both an issue AND a feature request for the same work
- Derive as much structured data as possible from the natural language description
- Report the entity ID prominently so the user can reference it
