---
name: pm-brainstorm
description: >-
  Interactive feature brainstorming through guided discovery, web research, and
  codebase analysis. Helps users who have a vague idea ("I want to improve X",
  "what if we added something for Y") explore possibilities through Socratic
  dialogue before creating well-formed feature requests. Use this skill whenever
  the user wants to brainstorm, explore ideas, think through a feature concept,
  or says things like "brainstorm", "what could we do about...", "I have an idea",
  "let's think about...", "explore options for...", or "help me figure out what
  feature to build". Also use when the user has a rough concept but needs help
  shaping it into something concrete — this is the skill for turning fuzzy thinking
  into sharp feature requests.
argument-hint: "[topic or area to brainstorm]"
---

# Interactive Feature Brainstorming

Guide the user from a rough idea to well-formed feature requests through conversation,
research, and codebase understanding. Unlike `/pm-explore` (which autonomously generates
suggestions) or `/pm-plan` (which takes a clear description and creates entities), this
skill is for the messy early stage — when the user knows something could be better but
hasn't crystallized what "better" looks like yet.

The core loop: **Ask → Research → Ground → Refine → Create**.

## Step 0: Parse Arguments and Set Context

Parse `$ARGUMENTS` for the brainstorming topic. This might be:
- A specific area: "dashboard UX", "notifications", "reporting"
- A problem statement: "it's hard to track progress across work packages"
- A user segment: "features for new users", "admin workflows"
- A vague direction: "what's missing?", "how can we improve things?"
- Empty (no arguments) — that's fine, you'll discover the topic in Step 1

## Step 1: Open the Conversation

Start with a warm, exploratory question. The goal is to understand the user's
mental model — what they care about, what's frustrating them, what opportunity
they see. Don't jump to solutions yet.

**If arguments were provided**, acknowledge the topic and ask a focusing question:

Use `AskUserQuestion`:
- Question: Based on the topic, ask something that narrows scope without closing
  off possibilities. Examples:
  - For "dashboard UX": "What's the most frustrating part of using the dashboard
    right now — is it finding information, understanding status, or something else?"
  - For "notifications": "When you think about notifications, are you imagining
    alerts for yourself (status changes, blockers) or for keeping a team in sync?"
  - For vague topics: "What prompted this? Was there a moment recently where you
    thought 'this should be easier' or 'I wish I could...'?"
- Header: "Direction"
- Options: 2-3 options that represent distinct directions, plus always allow
  free-form via "Other"

**If no arguments**, use `AskUserQuestion` to discover the topic:
- Question: "What area of the project would you like to brainstorm about?"
- Header: "Topic"
- Options: Generate 3-4 options based on a quick scan of the project (e.g., based
  on entity types, existing features, or gaps you notice). Always phrased as
  user-facing concerns, not technical internals.

## Step 2: Resolve Project and Load Context

While engaging in dialogue, gather the project context you'll need:

- Current directory: !`pwd`
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`

In parallel, load existing items to understand what's already tracked:
1. `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Active"`
2. `mcp__pinkrooster__get_feature_requests` with `projectId` and `stateFilter: "Inactive"`
3. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Active"`
4. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Inactive"`

Compile a mental deduplication list. You'll use this throughout to avoid suggesting
things that are already tracked.

## Step 3: Research the Problem Space

Based on the user's initial direction, do two things in parallel:

### 3a: Web Research

Use `WebSearch` to understand the broader problem space. The goal is to bring
external perspective — what do other products do? What are established patterns?
What do users of similar tools typically want?

Research queries should be targeted to the user's area of interest:
- If they're exploring "dashboard UX": search for best practices in project management
  dashboards, common dashboard widgets, information architecture patterns
- If they're exploring "notifications": search for notification system design patterns,
  alert fatigue research, notification preferences UX
- If they're exploring a specific integration: search for that integration's API,
  common use cases, existing implementations

Synthesize 3-5 key insights from the research. These aren't suggestions yet —
they're perspective that will inform the brainstorming conversation.

### 3b: Codebase Analysis

Use Glob, Grep, and Read to understand the current state of the area being discussed.
Focus on:

- What exists today in this area? (pages, components, API endpoints, entities)
- What data is available that could power new features?
- What patterns/infrastructure could be leveraged?
- What are the current limitations or gaps?

The codebase analysis grounds the brainstorming in reality — every idea should be
something the existing architecture can support or be reasonably extended to support.

## Step 4: The Brainstorming Loop (2-3 rounds)

This is the heart of the skill. Run 2-3 rounds of guided conversation, each building
on the previous one. The goal is progressive refinement: broad → focused → specific.

### Round 1: Expand Possibilities

Share what you've learned from research and code analysis, framed as provocations
rather than suggestions. Use `AskUserQuestion` to explore:

- Question: Present 3-4 directions informed by your research, each with a brief
  "why this matters" explanation drawn from external best practices or competitor
  analysis. Frame them as questions, not proposals:
  - "Other project management tools let users set up custom dashboard views —
    would something like that help here?"
  - "I noticed the activity log captures a lot of data but there's no way to
    visualize trends — is that something worth exploring?"
  - "Research shows that teams using status notifications reduce blocked-item
    resolution time by 40% — is notification fatigue a concern for your users?"
- Header: "Explore"
- multiSelect: true (the user can be interested in multiple directions)

### Round 2: Deepen and Shape

Based on what the user selected and said, go deeper on the chosen direction(s).
This is where you transition from "what area?" to "what specifically?". You should
do additional targeted research and code analysis based on the narrowed focus.

Use `AskUserQuestion` to refine:
- Question: Present 2-3 more specific variations within the chosen direction(s).
  Include concrete examples of what the feature would look like, grounded in
  the actual codebase:
  - "For dashboard customization, I see two approaches: (1) Configurable widgets
    where users pick what cards to show, or (2) Saved filter views per project.
    The existing TanStack Table infrastructure could support option 2 more easily.
    Which feels more valuable?"
- Header: "Shape"
- Options: Specific, concrete options with trade-off descriptions

### Round 3: Confirm and Detail (optional)

If the ideas are clear enough after Round 2, skip this. Otherwise, use one more
round to nail down specifics:

Use `AskUserQuestion`:
- Question: Present a concise summary of the emerging feature idea(s) and ask
  if anything is missing or should change
- Header: "Confirm"
- Options: `[{label: "Looks good", description: "Ready to create feature requests"}, {label: "Adjust", description: "I want to change something"}, {label: "Add more", description: "I have additional ideas to include"}]`

### Guidelines for the brainstorming conversation:

- **Listen more than you prescribe.** The user's instincts about their own product
  are more valuable than generic best practices. Use research to spark ideas, not
  to override the user's judgment.
- **Ground everything in the codebase.** Vague ideas like "better analytics" become
  concrete when you can say "the activity_logs table already captures request data —
  we could surface response time trends without any new data collection."
- **Name trade-offs honestly.** If an idea is exciting but would require significant
  work, say so. If a simpler version would capture 80% of the value, suggest it.
- **Watch for scope creep.** If the conversation is producing 8+ distinct ideas,
  gently suggest prioritizing: "These are all interesting — which 2-3 feel most
  impactful for your users right now?"
- **Cross-reference existing items.** If an idea overlaps with an existing FR or
  issue, mention it: "This is related to proj-1-fr-3 — should we expand that
  instead of creating something new?"

## Step 5: Synthesize Feature Requests

After the brainstorming rounds, synthesize the conversation into concrete FR
candidates. For each idea that emerged:

1. **Name**: Descriptive title following the pattern `{Feature}: {brief qualifier}`
2. **Description**: Comprehensive spec incorporating:
   - What the feature does (from the brainstorming conversation)
   - Why it matters (from user's input + research findings)
   - How it fits with existing functionality (from codebase analysis)
   - Key interactions or workflow steps
   - Explicit out-of-scope items (things discussed but deferred)
3. **Category**: `Feature`, `Enhancement`, or `Improvement`
4. **Priority**: Inferred from the conversation — what the user seemed most
   excited about gets higher priority
5. **Business Value**: Synthesized from the user's "why" + research backing
6. **User Stories**: 2-4 concrete stories derived from the conversation
7. **Acceptance Summary**: 4-6 testable criteria

Present the synthesized FRs in a table:

```
## Brainstorm Results — {N} Feature Request(s)

| # | Name | Category | Priority | Business Value |
|---|------|----------|----------|----------------|
| 1 | {name} | {category} | {priority} | {one-line value} |
| ... |

### Details

**1. {name}**
- Category: {category} | Priority: {priority}
- Description: {2-3 sentence summary}
- Business Value: {why it matters}
- User Stories:
  - As a {role}, I want {goal}, so that {benefit}
- Acceptance Criteria:
  - {criterion 1}
  - {criterion 2}
- Research backing: {key insight from web research that supports this}

**2. {name}**
...
```

Use `AskUserQuestion` to confirm:
- Question: "Here are the feature requests from our brainstorming. Which should I create?"
- Header: "Create FRs"
- multiSelect: true
- Options: One per FR (up to 4), plus "All" and "None" options

## Step 6: Create Feature Requests

For each selected FR, call `mcp__pinkrooster__create_or_update_feature_request` with:
- `projectId`
- `name`: from synthesis
- `description`: full description from synthesis
- `category`: mapped category
- `priority`: mapped priority
- `businessValue`: from synthesis
- `acceptanceSummary`: from synthesis
- `userStories`: array of `[{"role": "...", "goal": "...", "benefit": "..."}]`
- `requester`: "pm-brainstorm"

Before each creation, check the deduplication list. If overlap exists with an
existing FR, use `AskUserQuestion` to ask whether to update the existing item
or create a new one.

## Step 7: Report and Next Steps

```
## Created {count} Feature Request(s) from Brainstorm

| # | FR ID | Name | Priority |
|---|-------|------|----------|
| 1 | {frId} | {name} | {priority} |
| ... |

### Research Insights Applied
- {key finding 1 that influenced the FRs}
- {key finding 2}

### Next Steps
- Refine details: `/pm-refine-fr {frId}`
- Plan implementation: `/pm-scaffold {frId}`
- Continue brainstorming: `/pm-brainstorm {different-topic}`
- View project status: `/pm-status`
```

Use `AskUserQuestion` for follow-up:
- Question: "What would you like to do next?"
- Header: "Next step"
- Options: Based on what was created — Scaffold first FR, Refine an FR,
  Brainstorm another area, or Done

## Constraints

- The brainstorming conversation should feel natural, not like filling out a form.
  Use open-ended questions early, structured options later.
- Every feature idea must be grounded in codebase reality — don't suggest things
  the architecture can't support without noting the gap.
- Web research should inform the conversation, not dominate it. Share 1-2 key
  insights per round, not a research dump.
- Always cross-reference existing FRs/issues/WPs to avoid duplicates.
- Created FRs start as `Proposed` — no auto-state propagation.
- Keep the brainstorming loop to 2-3 rounds. If the user wants to keep going,
  that's fine, but don't force extra rounds when ideas are already clear.
- User stories must be concrete and tied to the conversation — not generic
  templates like "As a user, I want this feature, so that I can use it."
- Include web research sources in FR descriptions where relevant, so future
  implementers have context.
- If brainstorming reveals a bug or issue rather than a feature, note it and
  suggest: "This sounds more like an issue — want me to create it with
  `/pm-plan` instead?"
- Maximum 5 FRs per brainstorm session. If the conversation produces more,
  suggest prioritizing and deferring the rest to a follow-up session.
