---
name: pm-plan
description: >-
  Plan new work by creating an issue or feature request from a natural
  language description. Optionally scaffold a work package with phases
  and tasks.
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

**If ambiguous**, use the `AskUserQuestion` tool:
- Question: "Is this a bug/issue to fix, or a new feature/enhancement to build?"
- Header: "Work type"
- Options: `[{label: "Bug/Issue", description: "Something broken, slow, or insecure that needs fixing"}, {label: "Feature/Enhancement", description: "A new capability or improvement to build"}]`

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

## Step 2: Clarify Ambiguities

Before classifying, check if the description is missing critical information. Ask the user to clarify if any of these apply:

**Scope ambiguity** — the description could mean very different things:
- "Improve the dashboard" → Which part? Performance? UI? New features?
- "Fix the login" → What's broken? Error message? Redirect? Credentials?

**Boundary ambiguity** — unclear what's in/out of scope:
- "Add export functionality" → Which entities? What formats? Where in the UI?
- "Support notifications" → Email? In-app? Push? What triggers them?

**Audience ambiguity** — unclear who the work is for:
- "Make it easier to use" → For whom? End users? Developers? Admins?

**Priority signals conflict** — description mixes urgency levels:
- "Nice to have but also kind of urgent" → Clarify actual priority

**Ask concisely** — use the `AskUserQuestion` tool to combine related clarifications (up to 4 questions per call). Example:
- Question 1: "Which dashboard pages should this cover?" / Header: "Scope" / Options: [{label: "All pages", ...}, {label: "Specific pages", ...}]
- Question 2: "Should this include API changes?" / Header: "Layers" / Options: [{label: "Frontend only", ...}, {label: "Frontend + API", ...}]

**If the description is clear and specific**, skip this step — do not ask unnecessary questions.

## Step 3: Confirm Classification

Present the classification to the user using the `AskUserQuestion` tool:
- Question: "Create {entityType} '{name}' with priority {priority}? (Severity: {severity, if issue})"
- Header: "Confirm"
- Options: `[{label: "Create", description: "Proceed with the classification above"}, {label: "Adjust type", description: "Change the entity type or classification"}, {label: "Cancel", description: "Do not create anything"}]`

## Step 4: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- If no project found, offer to register it first

## Step 5: Create Entity

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

## Step 5.5: Check for Duplicates

Before creating, cross-reference existing items to avoid duplicates:

1. Call `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Active"` + `"Inactive"`
2. Call `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Active"` + `"Inactive"`
3. Compare the derived name and description against existing items
4. If a potential duplicate is found, use the `AskUserQuestion` tool:
   - Question: "Similar item found: {existingId} '{existingName}' ({state}). How would you like to proceed?"
   - Header: "Duplicate?"
   - Options: `[{label: "Create anyway", description: "Create a new item despite the similarity"}, {label: "View existing", description: "Show details of the existing item before deciding"}, {label: "Skip", description: "Don't create — use the existing item instead"}]`
5. If the user selects "View existing", show the existing item details and re-ask

If no duplicates, proceed silently.

## Step 6: Report and Offer Next Steps

```
## Created

**{entityType}**: {entityId} "{name}"
- Priority: {priority}
- State: NotStarted / Proposed
- View at: http://localhost:3000/projects/{projNum}/issues/{num} (or feature-requests)

### Next Steps
- **Refine** (if Feature Request): Add user stories and business value: `/pm-refine-fr {entityId}`
- **Scaffold**: Create a work package with implementation tasks: `/pm-scaffold {entityId}`
- **Triage**: Review priorities across all items: `/pm-triage`
```

## Step 7: Optional Scaffolding or Refinement

Use the `AskUserQuestion` tool to offer next steps:
- Question: "What would you like to do next with {entityId}?"
- Header: "Next step"
- Options: `[{label: "Scaffold", description: "Create a work package with implementation tasks: /pm-scaffold {entityId}"}, {label: "Refine", description: "Add user stories and business value: /pm-refine-fr {entityId} (FR only)"}, {label: "Skip", description: "Done for now — refine or scaffold later"}]`

**If Scaffold**: Delegate to `/pm-scaffold {entityId}`
**If Refine** (FR only): Delegate to `/pm-refine-fr {entityId}` — enriches the FR with user stories, business value, and acceptance criteria before scaffolding
**If Skip**: "Entity created. You can refine with `/pm-refine-fr {entityId}` or scaffold with `/pm-scaffold {entityId}` later."

## Constraints

- Always confirm classification before creating entities
- Never create both an issue AND a feature request for the same work
- Derive as much structured data as possible from the natural language description
- Report the entity ID prominently so the user can reference it
- After creation, suggest `/pm-triage` if the project has many open items to help the user prioritize
- Always check for duplicates before creating — prevent redundant tracking items
