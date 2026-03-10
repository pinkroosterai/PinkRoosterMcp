# Proposal: Feature Request & Implementation Tracking

## Problem Statement

AI agents using PinkRooster can track **problems** (Issues) and **execute planned work** (Work Packages), but there is no way to capture **feature requests, enhancements, or ideas** before they become committed work. The gap exists in the early lifecycle: from concept through evaluation to approval.

### Current Entity Roles

| Entity | Role | Lifecycle |
|--------|------|-----------|
| Issue | Reactive problem tracking | Found → Diagnosed → Resolved |
| WorkPackage | Proactive work execution | Planned → Decomposed → Implemented |
| **??? (gap)** | **Feature request / idea capture** | **Proposed → Evaluated → Approved → Scheduled** |

### What AI Agents Cannot Do Today

- Capture a feature idea without committing to a full Work Package
- Distinguish feature requests from bug reports in the Issue system
- Track the evaluation/approval lifecycle before implementation begins
- Link a single feature request to multiple implementation WPs
- Query "what features are proposed but not yet scheduled?"
- Record business justification, user stories, or requester context

---

## Current Entity & Enum Inventory

### IssueType (all reactive/problem-oriented)
`Bug`, `Defect`, `Regression`, `TechnicalDebt`, `PerformanceIssue`, `SecurityVulnerability`

### WorkPackageType (all execution-oriented)
`Feature`, `BugFix`, `Refactor`, `Spike`, `Chore`

### Issue Fields (diagnosis-oriented)
StepsToReproduce, ExpectedBehavior, ActualBehavior, AffectedComponent, StackTrace, RootCause, Resolution

### Issue ↔ WorkPackage Link
- Direction: WP → Issue (optional `LinkedIssueId` FK)
- Cardinality: One Issue ← Many WPs; One WP → At most one Issue
- No reverse FK on Issue; linked WPs discovered via query

---

## Solution Paths

### Path A: Extend Issue Entity with New Types

Expand the existing Issue entity to cover feature requests by adding new `IssueType` values and optional fields.

#### Changes Required

**Enum addition** (Shared):
```
IssueType += FeatureRequest, Enhancement
```

**New optional fields on Issue entity** (Data):
```csharp
public string? BusinessValue { get; set; }     // Why this matters
public string? UserStory { get; set; }          // "As a... I want... So that..."
public string? Requester { get; set; }          // Who requested it
public string? AcceptanceSummary { get; set; }  // High-level acceptance criteria
```

**Migration**: Add 4 nullable string columns to `issues` table.

**MCP tools**: No new tools needed. `create_or_update_issue` already handles all fields via PATCH semantics. Add the 4 new optional parameters.

**Dashboard**: Issue detail page conditionally shows diagnosis fields (Bug types) vs request fields (FeatureRequest/Enhancement types).

#### Vertical Slice Scope
| Layer | Changes |
|-------|---------|
| Shared | Add 2 enum values, extend DTOs with 4 optional fields |
| Data | Add 4 columns to Issue entity, migration |
| API | Update IssueService to map new fields, update audit logging |
| MCP | Add 4 optional params to `create_or_update_issue` |
| Dashboard | Conditional field rendering on issue detail |
| Tests | Extend existing issue tests with new type/field coverage |

#### Pros
- Minimal code change (~1-2 days of work)
- All 16 existing MCP tools work unchanged
- Reuses Issue infrastructure: CompletionState, Priority, audit log, attachments, LinkedWorkPackages
- AI agents already know how to use `create_or_update_issue`
- `get_next_actions` automatically includes feature requests (they're Issues)
- `get_issue_overview` with state filters works for feature requests too

#### Cons
- Semantic stretch: Issue entity was designed for problems (field names like StepsToReproduce, ActualBehavior feel wrong alongside FeatureRequest)
- No separate lifecycle: feature requests share CompletionState with bugs (Designing/Implementing/Testing stages apply differently)
- Query pollution: `get_issue_overview` mixes bugs and feature requests unless filtered by type
- Single LinkedIssueId on WP means a WP can only link to one request (adequate for most cases but limiting for compound features)

---

### Path B: New FeatureRequest Entity (Recommended)

Create a purpose-built entity for feature requests with its own lifecycle, fields, and MCP tools.

#### New Entity: FeatureRequest

```
ID format: proj-{ProjectId}-fr-{FeatureRequestNumber}
Per-project sequential numbering (same pattern as Issue/WP)
```

**Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Name | string | Yes | Short title |
| Description | string | Yes | Detailed description |
| Category | enum | Yes | Feature, Enhancement, Improvement |
| Priority | Priority enum | No (default: Medium) | Reuse existing Priority enum |
| Status | FeatureStatus enum | No (default: Proposed) | Lifecycle state (see below) |
| BusinessValue | string | No | Why this matters, business justification |
| UserStory | string | No | "As a... I want... So that..." |
| Requester | string | No | Who/what requested this |
| AcceptanceSummary | string | No | High-level definition of done |
| Attachments | FileReference[] | No | Reuse existing owned type |
| StartedAt | DateTimeOffset? | Auto | Set when leaving Proposed |
| CompletedAt | DateTimeOffset? | Auto | Set when reaching terminal |
| ResolvedAt | DateTimeOffset? | Auto | Set when reaching terminal |

**FeatureStatus enum** (purpose-built lifecycle):
```
Proposed       — Initial capture, not yet evaluated
UnderReview    — Being evaluated for feasibility/priority
Approved       — Accepted, awaiting implementation scheduling
Scheduled      — Work Package(s) created, implementation planned
InProgress     — At least one linked WP is active
Completed      — All linked WPs completed, feature delivered
Rejected       — Evaluated and declined (with reason)
Deferred       — Accepted but postponed indefinitely
```

**Categories**: Active (UnderReview, Approved, Scheduled, InProgress), Inactive (Proposed, Deferred), Terminal (Completed, Rejected)

#### Relationships

**FeatureRequest → WorkPackage** (one-to-many):
- Replace `WorkPackage.LinkedIssueId` pattern with `WorkPackage.LinkedFeatureRequestId` (nullable FK)
- OR: Add `LinkedFeatureRequestId` alongside existing `LinkedIssueId` (WP can link to one Issue AND one FeatureRequest)
- FeatureRequest response includes `LinkedWorkPackages[]` array (same pattern as Issue)

**FeatureRequest → Issue** (optional, zero-to-many):
- Feature requests can reference Issues that motivated them (e.g., "users reported bug X, which revealed we need feature Y")
- Lightweight: just an optional `LinkedIssueId` on FeatureRequest, or skip for V1

#### New MCP Tools (4-5)

| Tool | Type | Description |
|------|------|-------------|
| `create_or_update_feature_request` | Write | Create or update feature request (same pattern as issue tool) |
| `get_feature_request_details` | Read | Full feature request data + linked WPs |
| `get_feature_requests` | Read | Compact list with status filter |

**Existing tools enhanced:**
- `create_or_update_work_package`: Accept optional `linkedFeatureRequestId` parameter
- `get_work_package_details`: Return `linkedFeatureRequestId` in response
- `get_project_status`: Add feature request counts to status summary
- `get_next_actions`: Include actionable feature requests (UnderReview, Approved)

#### Automatic Status Transitions

When WP state changes cascade back to FeatureRequest status:
- WP linked to FR transitions to active → FR auto-transitions to `InProgress` (if currently Approved/Scheduled)
- All linked WPs reach terminal → FR auto-transitions to `Completed`
- This reuses the existing cascade pattern from `StateCascadeService`

#### Vertical Slice Scope

| Layer | Changes |
|-------|---------|
| Shared | New FeatureStatus enum, FeatureCategory enum, DTOs (Create/Update requests, response), extend ProjectStatusResponse, extend NextActionItem |
| Data | New FeatureRequest entity + FeatureRequestAuditLog, new FK on WorkPackage, migration, DbContext config |
| API | New FeatureRequestService + IFeatureRequestService, FeatureRequestController, extend WorkPackageService for new FK |
| MCP | New FeatureRequestTools (3 tools), extend WorkPackageTools + ProjectTools |
| Dashboard | New feature request list/detail pages, extend project detail with FR tab |
| Tests | New integration test class (~15-20 tests) |

#### Pros
- Clean domain model: purpose-built fields and lifecycle for feature requests
- Separate lifecycle: FeatureStatus captures the evaluation → approval → implementation flow that CompletionState doesn't model well
- No semantic pollution: Issues stay for problems, FeatureRequests for ideas
- Better querying: `get_feature_requests` with status filter gives AI agents a clean view
- Extensible: can add voting, comments, or priority scoring later
- Follows established patterns: same vertical slice as Issue and WorkPackage entities

#### Cons
- Larger implementation effort (~3-5 days)
- 3 new MCP tools to learn (though they follow exact same patterns)
- New entity means new audit log table, new dashboard pages
- WorkPackage now has two optional FKs (LinkedIssueId + LinkedFeatureRequestId)

---

### Path C: Unified Ticket Entity (Replace Issues)

Merge Issues and FeatureRequests into a single "Ticket" entity with a category discriminator.

#### Concept

```
Ticket
├── Category: Bug | Defect | Regression | TechnicalDebt | PerformanceIssue | SecurityVulnerability
│              | FeatureRequest | Enhancement | Improvement
├── [Bug fields]: StepsToReproduce, ActualBehavior, StackTrace, RootCause...
├── [Feature fields]: BusinessValue, UserStory, Requester, AcceptanceSummary...
├── [Shared fields]: Name, Description, Priority, State, Attachments...
```

All fields optional; which fields are relevant depends on category.

#### Changes Required
- Rename Issue → Ticket across entire codebase
- Add new category values and feature-related fields
- Migration to rename table + add columns
- Update all MCP tools, DTOs, API routes, dashboard
- Backward-incompatible: existing `proj-1-issue-3` IDs change to `proj-1-ticket-3`

#### Pros
- Single entity for all "incoming items" (bugs, features, enhancements)
- Unified search and filtering
- Simpler mental model: one inbox, one tool set

#### Cons
- **Breaking change**: renames Issue entity, IDs, API routes, all MCP tool names
- Large refactor across all layers (~5-7 days)
- God-entity anti-pattern: one entity with many conditional fields
- Loses semantic clarity between "problem found" and "feature requested"
- Existing test suite needs significant rewrite
- AI agents need to relearn renamed tools

---

## Recommendation: Path B (New FeatureRequest Entity)

### Why Path B Over Path A

Path A is tempting for its simplicity, but it compromises the domain model:

1. **Lifecycle mismatch**: Feature requests go through Proposed → UnderReview → Approved → Scheduled, which maps poorly to CompletionState's Designing → Implementing → Testing flow. An agent would need to use "Designing" to mean "UnderReview" — confusing semantics.

2. **Field pollution**: The Issue entity would carry both `StepsToReproduce` (for bugs) and `BusinessValue` (for features) on every instance. Agents need to know which fields apply when.

3. **Query clarity**: `get_issue_overview` returning a mix of bugs and features makes `get_next_actions` harder to reason about. Agents benefit from clear entity boundaries.

4. **Established pattern**: PinkRooster already separated Issues (problems) from Work Packages (execution). Adding FeatureRequests (ideas) as a third entity follows the same philosophy — each entity has a clear purpose and lifecycle.

### Why Path B Over Path C

Path C is architecturally clean in theory but devastating in practice:

1. **Breaking change**: Every consumer (MCP tools, dashboard, tests) needs rewriting
2. **Existing data**: ID format changes break stored references
3. **Diminishing returns**: The unified model doesn't add capability over Path B, just changes the organization

### Path B Implementation Priority

The feature can be built incrementally following the established vertical slice pattern:

```
Phase 1: Shared + Data (entity, enums, DTOs, migration)
Phase 2: API (service, controller, routes)
Phase 3: MCP (3 new tools + extend existing tools)
Phase 4: Dashboard (list + detail pages, project detail FR tab)
Phase 5: Tests (integration tests)
Phase 6: Cascade integration (WP completion → FR auto-complete)
```

Each phase is independently deployable and testable — the same 6-phase workflow used for Issues and Work Packages.

### Expected Final Tool Count

Current: 16 MCP tools → After: 19 MCP tools (+3 for FeatureRequest CRUD)

| New Tool | Type | Description |
|----------|------|-------------|
| `create_or_update_feature_request` | Write | Create/update feature request |
| `get_feature_request_details` | Read | Full FR data + linked WPs |
| `get_feature_requests` | Read | Compact list with status filter |

### AI Agent Workflow After Implementation

```
1. Agent captures idea:
   create_or_update_feature_request(name, description, category, businessValue, userStory)
   → proj-1-fr-1 (Proposed)

2. Agent evaluates feasibility:
   create_or_update_feature_request(featureRequestId, status: UnderReview)

3. Agent approves and plans:
   create_or_update_feature_request(featureRequestId, status: Approved)
   scaffold_work_package(linkedFeatureRequestId: proj-1-fr-1, phases: [...])
   → FR auto-transitions to Scheduled

4. Agent tracks progress:
   get_feature_request_details(proj-1-fr-1) → shows linked WPs and their states
   get_next_actions() → includes FR items needing review

5. Implementation completes:
   All linked WPs reach terminal → FR auto-transitions to Completed
```
