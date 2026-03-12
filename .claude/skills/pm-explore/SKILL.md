---
name: pm-explore
description: >-
  Analyze the codebase from a product manager's perspective and suggest
  realistic, user-facing feature enhancements. Cross-references existing
  items to avoid duplicates. Creates selected suggestions as feature requests.
argument-hint: "[--limit N]"
---

# Explore Feature Opportunities

Analyze the codebase like a product manager and suggest user-facing feature enhancements.
Every suggestion must be something a non-technical stakeholder would understand and care about.

## Step 0: Parse Arguments

Parse `!arguments` for flags:

- **`--limit N`**: Maximum number of suggestions to generate (default: 5, max: 10)
- If no arguments, use default limit of 5

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- Note the counts for context (how many issues, FRs, WPs exist)

## Step 2: Load Existing Tracked Items

Load all existing items to avoid suggesting duplicates. Make these calls:

1. `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Active"`
2. `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Inactive"`
3. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Active"`
4. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Inactive"`
5. `mcp__pinkrooster__get_work_packages` with `projectId`

Compile a deduplication list of all tracked item names and descriptions. You will cross-reference
this list before presenting suggestions to ensure no overlap with existing work.

## Step 3: Analyze the Codebase

Use an **Explore agent** approach — read the codebase to understand what the application does
from an **end-user perspective**, not a developer perspective.

Focus on:
- What does this application do? What problem does it solve?
- Who are the users? What are their workflows?
- What UI exists? What pages, forms, dashboards, and interactions are available?
- What data entities exist and how do users interact with them?
- What API endpoints exist and what capabilities do they expose?
- What workflows or automation exist?

Use Glob, Grep, and Read to explore:
- Dashboard pages and components (`src/dashboard/src/pages/`)
- API controllers and routes (`src/PinkRooster.Api/Controllers/`)
- MCP tools (`src/PinkRooster.Mcp/Tools/`)
- Data entities (`src/PinkRooster.Data/Entities/`)
- Shared DTOs and enums (`src/PinkRooster.Shared/`)
- Skills and workflows (`.claude/skills/`)

## Step 4: Generate Suggestions

Think like a product manager. For each suggestion, ask:
- "Would a project manager or stakeholder request this?"
- "Does this improve the user experience or unlock a new capability?"
- "Is this distinct from existing tracked items?"

For each suggestion, produce:
- **Name**: Concise, descriptive title (e.g., "Dashboard Export: CSV Download for Issue Lists")
- **Category**: `Feature` (new capability), `Enhancement` (extends existing), or `Improvement` (makes existing better)
- **Priority**: `Critical`, `High`, `Medium`, or `Low`
- **Business Value**: 1-2 sentences explaining why a stakeholder would care
- **User Stories**: 1-2 structured stories with role, goal, benefit

Generate up to `--limit` suggestions (default 5).

### Explicit Exclusions

Do NOT suggest any of the following — they are developer concerns, not product features:
- Refactors, code cleanup, or architectural changes
- Performance optimizations or caching improvements
- Library upgrades or dependency changes
- Test coverage improvements
- CI/CD pipeline changes
- Infrastructure, deployment, or DevOps concerns
- Code style, linting, or formatting improvements
- Security hardening (unless it's a user-facing security feature like 2FA)
- Documentation improvements (unless user-facing help/onboarding)

## Step 5: Cross-Reference for Duplicates

Before presenting, check each suggestion against the deduplication list from Step 2:
- If a suggestion overlaps significantly with an existing FR, issue, or WP, discard it
- If a suggestion partially overlaps, note the overlap and differentiate the scope
- Replace discarded suggestions with new ones to meet the `--limit` count

## Step 6: Present Suggestions

```
## Feature Suggestions — {projectId}

**Analyzed**: {summary of what was explored}
**Existing items**: {frCount} FRs, {issueCount} issues, {wpCount} WPs

| # | Name | Category | Priority | Business Value |
|---|------|----------|----------|----------------|
| 1 | {name} | {category} | {priority} | {businessValue} |
| 2 | {name} | {category} | {priority} | {businessValue} |
| ... |

### Details

**1. {name}**
- Category: {category} | Priority: {priority}
- Business Value: {businessValue}
- User Stories:
  - As a {role}, I want {goal}, so that {benefit}
  - As a {role}, I want {goal}, so that {benefit}

**2. {name}**
...

---

```

After presenting the table, use the `AskUserQuestion` tool to let the user select:
- Question: "Which suggestions would you like to create as feature requests?"
- Header: "Create FRs"
- multiSelect: true
- Options: Build from suggestions (up to 4), e.g. `[{label: "#1 {name}", description: "{category} | {priority}"}, {label: "#2 {name}", description: "{category} | {priority}"}, {label: "All", description: "Create all {N} suggestions as feature requests"}, {label: "None", description: "Skip creation — keep suggestions for reference only"}]`

## Step 7: Create Feature Requests

For each selected suggestion, call `mcp__pinkrooster__create_or_update_feature_request` with:
- `projectId`
- `name`: suggestion name
- `description`: expanded description combining business value and context
- `category`: mapped category
- `priority`: mapped priority
- `businessValue`: from suggestion
- `userStories`: array of structured stories `[{"Role": "...", "Goal": "...", "Benefit": "..."}]`
- `requester`: "pm-explore"

Collect all created FR IDs.

## Step 8: Report Results

```
## Created {count} Feature Requests

| # | FR ID | Name | Priority | Status |
|---|-------|------|----------|--------|
| 1 | {frId} | {name} | {priority} | Proposed |
| ... |

### Next Steps
- Refine any FR: `/pm-refine-fr {frId}`
- Scaffold a work package: `/pm-scaffold {frId}`
- View project status: `/pm-status`
- Triage and prioritize: `/pm-triage`
```

If "none" was selected:
"No feature requests created. Suggestions are available for reference above."

## Constraints

- Every suggestion must be **user-facing** — something a non-technical stakeholder would understand
- Never suggest refactors, performance optimizations, or developer tooling improvements
- Always cross-reference existing FRs/issues/WPs to avoid duplicates
- Created FRs start as `Proposed` — no auto-state propagation
- Keep user stories concrete and specific, not vague platitudes
- The `--limit` flag controls suggestion count (default 5, max 10)
- If the codebase is small or all obvious features are already tracked, it's OK to suggest fewer than the limit
- Include specific next-step commands with FR IDs so the user can act immediately
- For ambiguous suggestions (could be a bug or feature), note: "Unsure if bug or feature? Use `/pm-plan <description>` for guided classification"
