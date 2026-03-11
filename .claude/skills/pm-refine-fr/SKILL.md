---
name: pm-refine-fr
description: >-
  Refine a feature request by analyzing its current state, rewriting the
  description with full detail, filling in missing fields (business value,
  acceptance summary, category, priority), and adding structured user stories.
argument-hint: <fr-id>
---

# Refine Feature Request

Analyze an existing feature request, rewrite it with comprehensive detail, fill in gaps, and add user stories.

## Step 1: Resolve Project

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`
- If no project found, tell the user and offer to register it

## Step 2: Load Feature Request

Call `mcp__pinkrooster__get_feature_request_details` with the FR ID from `$ARGUMENTS`.

If no ID is provided, call `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Inactive"` to list candidates, then ask the user which one to refine.

Record the current state of all fields:
- name, description, category, priority, status
- businessValue, acceptanceSummary, requester
- userStories (count and content)
- attachments
- linkedWorkPackages

## Step 3: Analyze the Codebase for Context

To write a well-informed FR, understand the project context:

1. Use Serena's `get_symbols_overview` or `list_dir` to understand the relevant areas of the codebase mentioned in the FR description
2. Check existing PM skills, patterns, and conventions if the FR relates to a new skill or workflow
3. Review similar existing FRs via `mcp__pinkrooster__get_feature_requests` to understand the level of detail expected and avoid overlap

This step is proportional to the FR's scope — a small enhancement needs less exploration than a new major feature.

## Step 4: Rewrite the Feature Request

Call `mcp__pinkrooster__create_or_update_feature_request` with `projectId`, `featureRequestId`, and improved fields:

### Name
- Make it descriptive and specific (not generic like "Test FR")
- Pattern: `{feature-name}: {brief qualifier}` (e.g., "pm-explore: AI-Driven Feature Discovery Skill")

### Description
Expand the description into a comprehensive specification:
- **What**: Clear explanation of what the feature does
- **Workflow**: Numbered steps showing how the feature operates end-to-end
- **Configuration**: Any settings, arguments, or options
- **Integration points**: Which existing systems/tools/files it touches (reads and writes)
- **Explicit exclusions**: What this feature is NOT (prevents scope creep)

Keep the original author's intent — refine and expand, don't reimagine.

### Business Value
If missing or weak, derive from the description:
- Who benefits and why?
- What problem does this solve?
- What becomes possible that wasn't before?

### Acceptance Summary
If missing, write 5-8 concrete, testable acceptance criteria numbered as a list.

### Category
If missing or generic, classify:
- `Feature` — entirely new capability
- `Enhancement` — extends existing functionality
- `Improvement` — makes existing functionality better without adding new capabilities

### Priority
If missing, infer from business value and scope. Don't change existing priority without flagging it.

## Step 5: Add User Stories

Analyze the refined description to identify distinct user roles and capabilities, then add user stories via `mcp__pinkrooster__manage_user_stories` with `action: "Add"`.

**Guidelines for user stories:**
- Aim for 3-6 stories per FR (enough to cover the feature, not so many they become redundant)
- Each story should represent a distinct user role OR a distinct capability
- Use concrete, specific language — not vague platitudes
- The `goal` should describe observable behavior, not internal implementation
- The `benefit` should explain the "so what?" — why does the user care?

**Common role patterns:**
- Primary user of the feature (the one who triggers it)
- Secondary beneficiary (someone who benefits from the output)
- Maintainer/admin (someone who configures or manages it)
- Stakeholder (someone who reviews or approves results)

**If user stories already exist**, review them:
- If they are well-written, keep them and only add missing perspectives
- If they are vague or low-quality, use `action: "Update"` with the `index` to improve them
- If they are redundant, use `action: "Remove"` with the `index`

## Step 6: Present Summary

Show the user what changed:

```
## Refined: {frId} "{new name}"

### Fields Updated
| Field | Before | After |
|-------|--------|-------|
| Name | {old} | {new} |
| Description | {length}ch → {length}ch | Expanded |
| Business Value | {empty/existed} | {set/updated} |
| Acceptance Summary | {empty/existed} | {set/updated} |
| Category | {old} | {new or unchanged} |
| Priority | {old} | {new or unchanged} |

### User Stories ({count})
| # | Role | Goal | Benefit |
|---|------|------|---------|
| 1 | {role} | {goal} | {benefit} |
| ... | | | |

Next steps: `/pm-scaffold {frId}` to create a work package
```

## Constraints

- Preserve the original author's intent — enhance, don't replace the core idea
- Never change the FR's `status` — refinement is about content, not workflow state
- Never remove existing user stories unless they are clearly redundant or wrong — prefer updating
- Always read the FR details first before making any changes
- If the FR is already well-specified (detailed description, business value, acceptance criteria, user stories), tell the user and suggest only minor improvements rather than rewriting
- Cross-reference existing FRs to avoid suggesting overlapping scope in the description
- Always show the before/after summary so the user can verify the refinement
