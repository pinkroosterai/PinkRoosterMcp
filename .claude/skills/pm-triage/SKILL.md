---
name: pm-triage
description: >-
  Review and prioritize open issues and feature requests. Analyzes
  severity, age, and codebase impact to recommend priority adjustments
  and next steps.
context: fork
agent: Explore
---

# Triage Issues and Feature Requests

Analyze all open issues and feature requests to produce a prioritized triage report.
This is a READ-ONLY analysis — never modify any entities.

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- Note the counts for context

## Step 2: Load All Open Items

Make these four calls:
1. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Active"`
2. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Inactive"`
3. `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Active"`
4. `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Inactive"`

## Step 3: Analyze Each Item

For every issue and feature request, assess:

1. **Age**: Days since `createdAt`. Flag items older than 14 days as aging, older than 30 as stale.
2. **Priority alignment**: Does the priority match the severity/urgency?
   - Critical severity + Low/Medium priority = misaligned (flag)
   - Low severity + Critical/High priority = potentially over-prioritized (flag)
3. **Work package linkage**: Does it have linked work packages?
   - Active issues/FRs without linked WPs = planning gap (flag)
   - Approved/Scheduled FRs without linked WPs = ready for scaffolding (flag)
4. **State staleness**: Items in NotStarted for >14 days need attention
5. **Blocked items**: Items in Blocked state without clear path to unblock

If the item has an `affectedComponent` (issues), use Grep to check code health in that area.

## Step 4: Categorize into Tiers

### Tier 1: High Priority (act now)
- Critical severity issues
- Items blocking other work
- Priority/severity misalignment favoring underpriced critical items
- Active items without linked WPs that are >7 days old

### Tier 2: Should Prioritize
- Major severity issues without WPs
- Approved/Scheduled FRs ready for scaffolding
- Items aging (14-30 days) in NotStarted

### Tier 3: Stale (consider closing)
- Items in NotStarted for >30 days
- Low-severity items with no activity
- Deferred FRs older than 30 days

## Step 5: Format Report

```
## Triage Report — {projectId}
**Date**: {today}
**Items analyzed**: {count} issues + {count} feature requests

### Tier 1: Act Now
| ID | Name | Type | Age | Priority | Severity | Issue |
|----|------|------|-----|----------|----------|-------|
| {id} | {name} | Issue/FR | {N}d | {priority} | {severity} | {reason} |

### Tier 2: Should Prioritize
| ID | Name | Type | Age | Priority | Issue |
|----|------|------|-----|----------|-------|

### Tier 3: Stale
| ID | Name | Type | Age | Priority | Issue |
|----|------|------|-----|----------|-------|

### Misalignment Flags
(Items where priority doesn't match severity)
- {id}: {severity} severity but {priority} priority — consider adjusting

### Recommendations
1. {Actionable recommendation referencing specific entity IDs}
2. {Suggest specific PM skill based on the situation}
3. ...

**Skill suggestions per situation:**
- Items with linked WPs ready for work → `/pm-next` or `/pm-implement {wpId}`
- Items needing work packages → `/pm-scaffold {entityId}`
- Items that appear done but aren't marked → `/pm-done {entityId}`
- New work to track → `/pm-plan <description>`
- Stale items to close → cancel via dashboard (http://localhost:3000)
```

## Constraints

- This skill runs in a forked Explore agent — you have NO conversation history
- NEVER modify entities — this is strictly read-only analysis
- Always produce the full report even if some tiers are empty (show "None")
- Include specific entity IDs in all recommendations so the user can act on them
- Always suggest the most appropriate PM skill for each recommendation: `/pm-next` for ready items, `/pm-implement` for specific WPs, `/pm-scaffold` for unplanned items, `/pm-done` for completable items, `/pm-plan` for new work
