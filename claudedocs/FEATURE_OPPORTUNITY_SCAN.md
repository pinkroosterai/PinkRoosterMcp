# Feature Opportunity Scan — PinkRooster

## Application Profile

**Domain:** AI-assisted software project management platform
**Primary users:** AI coding agents (via MCP tools), developers and project managers (via dashboard)
**Core capabilities:**
- Project registration and tracking (name, path, status)
- Issue tracking with full lifecycle (bug reports, defects, technical debt — 9 completion states)
- Work package planning with hierarchical breakdown (WP > Phases > Tasks)
- Dependency management between work packages and between tasks (with auto-block/unblock)
- State cascade automation (task completion propagates upward through phases to WPs)
- Full-field audit trail on all entity changes
- API request activity logging

**Integrations present:** None (standalone system)
**Unique design:** Dual-interface architecture — AI agents create and manage work via MCP tools; humans view and delete via a React dashboard. No overlap in write operations.

---

## Recommendations

### #1 — Link Issues to Work Packages from the Issue Side

**Category:** A — Workflow Completion & Lifecycle Gaps
**Benefits:** Project managers and agents can see which work package addresses a given issue, closing the traceability loop between "what's wrong" and "what's being done about it." Today the link is one-directional and buried in the WP detail.
**Value:** High

**Current state:**
Work packages have an optional `LinkedIssueId` field pointing to a single issue. However, issues have no back-reference — viewing an issue gives no indication whether any work package exists to address it. The dashboard's issue detail page shows reproduction data, resolution notes, and timestamps, but nothing about related work. An agent reviewing an issue has no tool to discover if a WP already covers it, leading to potential duplicate WPs.

**Proposed feature:**
Surface "linked work packages" on every issue — both in the MCP `get_issue_details` response and on the dashboard issue detail page. When viewing an issue, show all work packages that reference it (reverse lookup). Provide an MCP read tool or extend the existing `get_issue_details` tool to include this list. On the dashboard, add a "Related Work" card on the issue detail page with links to the associated WPs.

**User stories:**
- "As a project manager, I want to see which work packages are addressing an issue so I can check progress without searching through all WPs."
- "As an AI agent, I want to check if an issue already has an associated work package before creating a new one, so I avoid duplicate work."

**Depends on:** Existing `LinkedIssueId` FK on WorkPackage entity.

---

### #2 — Dashboard Search and Cross-Entity Filtering

**Category:** B — User Self-Service & Empowerment
**Benefits:** Users with many issues and work packages can find what they need without scrolling through tables or remembering IDs. Currently, the only filter is the three-way state category toggle.
**Value:** High

**Current state:**
The dashboard has no search functionality anywhere — no text search on issue names, no filtering by severity or priority, no search across work packages, no global search bar. The only filtering is state category buttons (Active/Inactive/Terminal) on the issues and work packages tabs. With projects that accumulate dozens of issues and WPs, finding a specific item requires visual scanning. Column sorting is not user-controllable (server-ordered only).

**Proposed feature:**
Add a search bar to the issues tab and work packages tab that filters by name/description. Add dropdown filters for severity, priority, type, and assignee (if added later). Enable column sorting on tables. Consider a global search that spans issues and WPs within the selected project, returning results grouped by entity type.

Scope: Text search + column sorting + dropdown filters on existing tables. Out of scope: full-text search infrastructure, cross-project search.

**User stories:**
- "As a project manager, I want to search issues by name so I can quickly find a specific bug report."
- "As a developer reviewing the dashboard, I want to sort work packages by priority so I can see what's most important."
- "As a project manager, I want to filter issues by severity so I can focus on critical problems first."

**Depends on:** None.

---

### #3 — Project-Level Progress Dashboard with Metrics

**Category:** E — Reporting, Analytics & Insights
**Benefits:** Gives project managers an at-a-glance understanding of project health — how much is done, what's blocked, how fast work is progressing. Today the dashboard home page shows only API request counts, not project progress.
**Value:** High

**Current state:**
The dashboard home page shows three stats: total API requests, server status (online/offline), and latest activity timestamp. These are infrastructure metrics, not project metrics. The project detail page shows count badges (active/inactive/terminal) for issues and WPs but no aggregated progress view. The data to compute meaningful metrics already exists: task completion states, timestamps (StartedAt, CompletedAt), phase/task counts, dependency chains. None of this is surfaced as analytics.

**Proposed feature:**
Replace or augment the dashboard home page with project-level progress metrics when a project is selected:
- **Completion progress**: X of Y tasks completed (percentage bar) across all active WPs
- **State distribution**: visual breakdown of issues and WPs by state (pie or bar chart)
- **Blocked items count**: how many WPs and tasks are currently blocked, with links to see which dependencies are holding them
- **Recently completed**: list of items that reached terminal state in the last 7 days
- **Velocity indicator**: tasks completed per week trend (using CompletedAt timestamps)

Scope: Read-only dashboard widgets using existing data. Out of scope: custom date ranges, exportable reports, velocity forecasting.

**User stories:**
- "As a project manager, I want to see overall project completion percentage so I can report progress to stakeholders."
- "As a team lead, I want to quickly see how many items are blocked so I can prioritize unblocking work."

**Depends on:** None — all data already exists in the API.

---

### #4 — Comments and Notes on Issues and Work Packages

**Category:** J — Natural Feature Extensions
**Benefits:** Agents and humans can leave contextual notes, decisions, or follow-up questions directly on entities. Today, the only way to record context is by updating description fields or implementation notes, which overwrites previous content.
**Value:** High

**Current state:**
Issues have a fixed set of text fields (description, steps to reproduce, root cause, resolution). Work packages have description and plan. Tasks have implementation notes. None of these support threaded or appended comments — updating any field replaces the previous value entirely (PATCH semantics with no field clearing). There is no way for an agent to say "I investigated this and found X" without overwriting the existing description. The audit log tracks field changes but isn't designed as a communication channel.

**Proposed feature:**
Add a comment/note system on Issues, Work Packages, and Tasks. Each comment records: author (caller identity), timestamp, and text content. Comments are append-only (create + read, no edit/delete). MCP tools get `add_comment` and the read tools include comments in their responses. The dashboard shows a chronological comment thread on detail pages.

Scope: Simple append-only comments on Issues, WPs, and Tasks. Out of scope: rich text, file attachments on comments, comment reactions, threaded replies.

**User stories:**
- "As an AI agent, I want to add investigation notes to an issue so that the next agent or developer can see what was already tried."
- "As a project manager, I want to read the comment history on a work package to understand the decisions that were made during implementation."

**Depends on:** None.

---

### #5 — Bulk State Transitions for Tasks

**Category:** F — Bulk Operations & Efficiency
**Benefits:** When an AI agent completes multiple tasks in a phase (common when finishing a batch of related work), it currently must call `create_or_update_task` once per task. A bulk update would reduce round-trips and make cascade notifications more coherent.
**Value:** High

**Current state:**
The phase batch operations support creating and upserting multiple tasks in one `create_or_update_phase` call. However, there is no equivalent for state-only transitions — marking 5 tasks as "Completed" requires 5 separate `create_or_update_task` calls. Each call triggers its own cascade check (upward propagation, auto-unblock), generating separate state change notifications. For an agent completing a phase's worth of work, this is chatty and produces fragmented cascade reporting.

**Proposed feature:**
Add a `batch_update_task_states` MCP tool that accepts a list of task IDs and a target state. Apply all transitions in a single operation, run cascades once at the end, and return a consolidated list of state changes. The API backing endpoint would accept a batch request.

Scope: Batch state transitions for tasks within a single work package. Out of scope: cross-WP batch operations, batch updates for non-state fields.

**User stories:**
- "As an AI agent, I want to mark all tasks in a phase as completed in one call so I can efficiently report progress after finishing a batch of work."
- "As an AI agent, I want to see a single consolidated cascade report when completing multiple tasks, rather than piecing together individual responses."

**Depends on:** None.

---

### #6 — Work Package and Issue Templates

**Category:** G — Personalization, Preferences & Customization
**Benefits:** When agents create similar work packages repeatedly (e.g., "bug fix" WPs always have the same phase structure), templates would eliminate repetitive setup and ensure consistency.
**Value:** Medium

**Current state:**
Every work package, phase, and task is created from scratch. An agent building a "feature implementation" work package must manually specify phases (e.g., Design, Implementation, Testing, Review) and their acceptance criteria each time. There is no way to save or reuse a common structure. Issue creation similarly requires providing all fields from scratch, even though bug reports in a project tend to follow a consistent pattern.

**Proposed feature:**
Introduce project-level templates for work packages and issues. A template defines a pre-filled structure (WP template: name pattern, phases with acceptance criteria, default tasks; Issue template: pre-filled type, severity, field hints). MCP tools get `create_template` and `list_templates`, plus existing create tools accept an optional `templateId` to pre-populate fields. Templates are project-scoped.

Scope: Templates for WPs (with phase/task structure) and issues (with field defaults). Out of scope: global templates, template versioning, template inheritance.

**User stories:**
- "As an AI agent, I want to create a work package from a 'Feature' template so that standard phases and acceptance criteria are pre-populated."
- "As a project manager, I want to define issue templates so that bug reports created by different agents follow the same structure."

**Depends on:** None.

---

### #7 — Verification Workflow for Acceptance Criteria

**Category:** A — Workflow Completion & Lifecycle Gaps
**Benefits:** Acceptance criteria exist on phases but have no way to be verified through MCP tools. The verification fields (VerificationResult, VerifiedAt) exist on the entity but are only settable through phase update's full-replacement semantics — there's no targeted verification action.
**Value:** Medium

**Current state:**
Phases have `AcceptanceCriteria` with fields for `VerificationMethod` (Manual, Automated, CodeReview), `VerificationResult`, and `VerifiedAt`. However, acceptance criteria are managed as a full-replacement list on phase update — there are no individual IDs exposed to MCP tools, and no dedicated "verify criterion" action. An agent wanting to mark a single criterion as verified must re-submit the entire list. The dashboard shows a checkmark based on `VerifiedAt` being set, but there's no way to trigger verification. The data model supports verification but the workflow doesn't.

**Proposed feature:**
Add a dedicated `verify_acceptance_criterion` MCP tool that accepts a phase ID, criterion name (or index), verification result text, and marks it as verified with a timestamp. The dashboard should show a verification status summary on the phase card (e.g., "3 of 5 criteria verified"). Consider auto-advancing phase state when all criteria are verified.

Scope: Individual criterion verification via MCP tool + dashboard status display. Out of scope: automated test integration, CI/CD-triggered verification.

**User stories:**
- "As an AI agent, I want to mark individual acceptance criteria as verified after running tests so the team can see which criteria are met."
- "As a project manager, I want to see how many acceptance criteria are verified for each phase so I can assess readiness for completion."

**Depends on:** None — entity fields already exist.

---

### #8 — Audit Trail Visibility for Work Packages and Tasks

**Category:** C — Visibility, History & Audit
**Benefits:** The system tracks full-field audit logs for WPs, phases, and tasks (WorkPackageAuditLog, PhaseAuditLog, TaskAuditLog tables) but never surfaces this data. Only issue audit logs have an API endpoint and dashboard display.
**Value:** Medium

**Current state:**
The Issue entity has a dedicated `/audit` API endpoint and the dashboard issue detail page shows an "Audit Log" card with timestamp, field, old value, new value, and changed-by. The same audit infrastructure exists for Work Packages, Phases, and Tasks (three separate audit log tables, populated on every field change). However, there are no API endpoints to retrieve WP/Phase/Task audit logs, no MCP tools to read them, and no dashboard display for them. The data is written but never read.

**Proposed feature:**
Expose audit log endpoints for work packages, phases, and tasks (matching the existing issue audit pattern). Add audit log display to the WP detail page and as expandable sections on phase/task detail views. Add MCP read tools or extend existing detail tools to optionally include audit history.

Scope: Read-only audit trail for WP, Phase, and Task entities. Out of scope: audit log search, audit log export, cross-entity audit timeline.

**User stories:**
- "As a project manager, I want to see who changed a work package's state and when so I can understand the history of decisions."
- "As an AI agent, I want to review a task's change history to understand what modifications were previously made before I update it."

**Depends on:** None — audit data is already being written.

---

### #9 — Project Archiving and Status Lifecycle

**Category:** A — Workflow Completion & Lifecycle Gaps
**Benefits:** Projects have a `Status` field with Active/Archived values, but there is no way to archive a project or filter by status. A completed project stays in the active list indefinitely.
**Value:** Medium

**Current state:**
The `ProjectStatus` enum defines `Active` and `Archived` values. The `Project` entity has a `Status` field. However, the `create_or_update_project` MCP tool doesn't accept a status parameter — projects are always created as Active. There is no API endpoint or MCP tool to archive a project. The dashboard projects list shows a status badge but doesn't filter by it. The project list API returns all projects regardless of status.

**Proposed feature:**
Add status management to projects: an `archive_project` MCP tool (or extend `create_or_update_project` with a status parameter), an API endpoint to update project status, and dashboard filtering to show/hide archived projects. Archiving should be reversible. Consider preventing creation of new issues or WPs on archived projects.

Scope: Archive/unarchive via MCP + API, dashboard filter, guard against writes on archived projects. Out of scope: auto-archive rules, project deletion workflow.

**User stories:**
- "As an AI agent, I want to archive a completed project so it doesn't clutter the active project list."
- "As a project manager, I want to filter the project list to show only active projects so I can focus on current work."

**Depends on:** None — enum and field already exist.

---

### #10 — Time Tracking on Tasks

**Category:** J — Natural Feature Extensions
**Benefits:** Tasks already track StartedAt and CompletedAt, giving wall-clock duration. But there's no way to record actual effort (time spent) vs. estimated effort, which is fundamental to planning accuracy.
**Value:** Medium

**Current state:**
Work packages have `EstimatedComplexity` (integer) and `EstimationRationale` (text) for upfront estimation. Tasks track `StartedAt` and `CompletedAt` timestamps for lifecycle timing. However, there is no concept of actual effort — no "hours spent" field, no time logging. The system can tell you *when* something started and finished but not *how much effort* it took. For AI agents doing implementation work, the elapsed time between StartedAt and CompletedAt includes idle time and is not a useful effort metric.

**Proposed feature:**
Add an `estimatedEffort` and `actualEffort` field to tasks (in hours or story points). MCP tools can set estimated effort on creation and log actual effort on completion or via update. The dashboard shows effort totals rolled up to phase and WP levels. Enable comparison between estimated and actual to inform future estimation.

Scope: Effort fields on tasks, rollup display on phases and WPs. Out of scope: time-tracking timer, hourly rates, cost calculations.

**User stories:**
- "As an AI agent, I want to record how long a task actually took so the project manager can compare estimates to actuals."
- "As a project manager, I want to see total estimated vs. actual effort per work package so I can improve future planning."

**Depends on:** None.

---

### #11 — Tag / Label System for Issues and Work Packages

**Category:** G — Personalization, Preferences & Customization
**Benefits:** Beyond the fixed type/severity/priority taxonomy, users and agents need flexible categorization — tagging by component, sprint, team, technology, or any custom grouping.
**Value:** Medium

**Current state:**
Issues have a fixed `AffectedComponent` text field and an `IssueType` enum. Work packages have a `WorkPackageType` enum. There is no freeform labeling system. If an agent wants to mark multiple issues as related to "authentication" or tag work packages for "sprint-3," there's no mechanism. The `AffectedComponent` field is a single text value on issues only — not a queryable, multi-value tag system.

**Proposed feature:**
Add a tag/label system where entities (Issues and Work Packages) can have multiple string tags. MCP tools support adding/removing tags on create and update. The dashboard shows tags as badges and supports filtering by tag. Tags are project-scoped and freeform (no predefined list required).

Scope: Freeform string tags on Issues and WPs, MCP CRUD, dashboard display and filter. Out of scope: tag management UI, tag color customization, tag hierarchies.

**User stories:**
- "As an AI agent, I want to tag related issues with a common label so they can be found and reviewed together."
- "As a project manager, I want to filter work packages by tag to see all items related to a specific sprint or component."

**Depends on:** None.

---

### #12 — Export Project Data for External Reporting

**Category:** H — Integrations & Interoperability
**Benefits:** Project managers who need to share progress with stakeholders outside the system currently have no export mechanism — they must manually transcribe data from the dashboard.
**Value:** Medium

**Current state:**
The system has no export functionality. The dashboard is read-only with no download buttons. The API returns JSON but there's no formatted export endpoint. Activity logs are paginated but not exportable. For anyone needing to create a status report, present to stakeholders, or archive project records, all data must be manually copied.

**Proposed feature:**
Add project data export in common formats. An MCP tool and API endpoint that generates a structured project report (JSON or Markdown) containing: project overview, all issues with current state, all work packages with phase/task breakdowns, dependency graph, and key metrics (completion %, blocked count). The dashboard gets an "Export" button on the project detail page.

Scope: Markdown and JSON export of project state. Out of scope: PDF generation, scheduled/automated exports, partial exports.

**User stories:**
- "As a project manager, I want to export a project summary as Markdown so I can paste it into a status report."
- "As an AI agent, I want to generate a structured project snapshot so I can analyze it outside the MCP tool context."

**Depends on:** None.

---

---

## Final Summary

### Top 3 Highest-Value Features

1. **Issue-to-Work-Package traceability** (#1) — Close the loop between "what's broken" and "what's being built to fix it" by showing linked work packages on every issue, both for agents and the dashboard.

2. **Project-level progress dashboard** (#3) — Transform the dashboard from an entity browser into a project health monitor with completion percentages, blocked-item counts, and velocity trends using data that already exists.

3. **Comments and notes on entities** (#4) — Give agents and humans a way to record investigation notes, decisions, and context without overwriting existing field values, turning entities into living documents.

### Theme Analysis

The gaps are concentrated in two areas:

**Visibility and insight** — The system is strong at recording data (audit logs, timestamps, state changes, dependencies) but weak at surfacing it. Audit trails for WPs/phases/tasks are written but never exposed. Progress metrics are computable but not computed. The dashboard is a faithful entity browser but not yet an analytical tool.

**Workflow completeness** — Several data model features are partially implemented. Acceptance criteria have verification fields but no verification workflow. Projects have a status enum but no lifecycle management. The issue-to-WP link is unidirectional. Tags are approximated by single text fields. These are "version 1.0" implementations that are structurally ready for the next step.

### Domain Comparison Note

Compared to project management tools in this space (Jira, Linear, Shortcut, Plane), PinkRooster is **notably thin on collaboration and reporting** but **uniquely strong on AI-agent integration and automated state cascades**. The dual-interface architecture (AI creates, humans view) is distinctive. The largest gaps relative to market expectations are: no search/filtering beyond state category, no comments/discussion, no analytics/dashboards, and no export capability. The dependency management and state cascade system is more sophisticated than many peer tools, which is a genuine differentiator.

### Recommended Sequencing

**Phase 1 — Foundational visibility** (builds on existing data, no schema changes):
1. #8 Audit trail for WPs/Tasks (data already exists, just needs endpoints + UI)
2. #1 Issue-to-WP back-reference (reverse lookup on existing FK)
3. #9 Project archiving (enum already exists, just needs tooling)

**Phase 2 — User productivity** (dashboard improvements):
4. #2 Search and filtering on dashboard
5. #3 Project progress dashboard with metrics

**Phase 3 — Agent workflow efficiency** (MCP tool enhancements):
6. #5 Bulk state transitions for tasks
7. #7 Acceptance criteria verification workflow

**Phase 4 — New capabilities** (schema additions):
8. #4 Comments and notes system
9. #11 Tags/labels
10. #10 Time tracking on tasks
11. #6 Work package templates
12. #12 Project data export
