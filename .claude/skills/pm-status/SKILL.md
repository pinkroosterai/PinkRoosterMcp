---
name: pm-status
description: >-
  Show project status dashboard with issue/FR/WP counts, active items,
  blocked items, and priority next actions. Use when the user asks about
  project status, progress, what's happening, what needs attention, or
  what to work on.
argument-hint: [limit]
---

# Project Status Dashboard

Show a formatted project status dashboard by chaining PinkRooster MCP tools.

## Step 1: Resolve Project

Get the project status using the current working directory:

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId` from the response (e.g., `proj-1`)
- If no project found, tell the user and offer to register it:
  "No project found. Run `mcp__pinkrooster__create_or_update_project` with name, description, and projectPath to register."

## Step 2: Get Next Actions

Call `mcp__pinkrooster__get_next_actions` with:
- `projectId` from Step 1
- `limit`: use `$ARGUMENTS` if provided, otherwise default to 10

## Step 3: Format Dashboard

Present the results in this exact format:

```
## Project: {name} ({projectId})

### Health Summary
| Entity | Active | Blocked | Completed | Total |
|--------|--------|---------|-----------|-------|
| Issues | ... | ... | ... | ... |
| Feature Requests | ... | ... | ... | ... |
| Work Packages | ... | ... | ... | ... |

### Blocked Items
(List any blocked issues, WPs, or tasks. If none, show "None — no blockers.")
- **{entityId}** "{name}" — blocked by {blocker} | state: Blocked
(If blocked items exist, add: "Deep-dive analysis: `/pm-triage`")

### Next Actions (top {limit})
1. [{priority}] {entityId} "{name}" ({state}) — {type}
2. ...

Start working: `/pm-next` | Implement specific item: `/pm-implement {topEntityId}`

### Planning Opportunities
(Feature requests that are Approved/Scheduled but have no linked work package)
- **{frId}** "{name}" — {status}, no linked WP → `/pm-scaffold {frId}`
(If none, show "None — all tracked items have work packages.")
```

## Constraints

- Never modify any entities — this skill is read-only
- Always show the blocked items section even if empty (say "None")
- Always show planning opportunities even if empty (say "None")
- If the project has no data yet, show empty tables and suggest: "Create work items with `/pm-plan <description>`"
- Format priority tags: [Critical], [High], [Medium], [Low]
- Always include actionable skill suggestions in each section so the user can act immediately
