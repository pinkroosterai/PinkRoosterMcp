# Workflow: Dashboard CRUD for Issues & Feature Requests

## Overview

Add full create and inline-edit capabilities for Issues and Feature Requests in the dashboard.
State transitions are handled as separate quick-actions with confirmation dialogs.

**Prerequisite**: API already supports full CRUD (POST/PATCH/DELETE/GET) for both entities.

---

## Phase 1: Shared Infrastructure

> Goal: Build the reusable foundation that all CRUD forms depend on.

### Task 1.1: Add Shadcn Form + Textarea + Sonner components

- Generate `src/dashboard/src/components/ui/form.tsx` (Shadcn Form using react-hook-form context)
- Generate `src/dashboard/src/components/ui/textarea.tsx`
- Install `sonner` package, create `src/dashboard/src/components/ui/sonner.tsx`
- Add `<Toaster />` to app layout

**Checkpoint**: Components render without errors, toast fires on manual test.

### Task 1.2: API client functions for create/update

- Add to `src/dashboard/src/api/issues.ts`:
  - `createIssue(projectId: number, data: CreateIssuePayload): Promise<Issue>`
  - `updateIssue(projectId: number, issueNumber: number, data: UpdateIssuePayload): Promise<Issue>`
- Add to `src/dashboard/src/api/feature-requests.ts`:
  - `createFeatureRequest(projectId: number, data: CreateFeatureRequestPayload): Promise<FeatureRequest>`
  - `updateFeatureRequest(projectId: number, frNumber: number, data: UpdateFeatureRequestPayload): Promise<FeatureRequest>`

**Checkpoint**: TypeScript compiles, payload types match API DTOs.

### Task 1.3: React Query mutation hooks

- Add to `src/dashboard/src/hooks/use-issues.ts`:
  - `useCreateIssue()` — POST, invalidates `["issues"]` + `["issue-summary"]`, returns created issue
  - `useUpdateIssue()` — PATCH, invalidates `["issue", projectId, issueNumber]` + `["issues"]`
- Add to `src/dashboard/src/hooks/use-feature-requests.ts`:
  - `useCreateFeatureRequest()` — POST, invalidates `["feature-requests"]`
  - `useUpdateFeatureRequest()` — PATCH, invalidates `["feature-request", projectId, frNumber]` + `["feature-requests"]`

**Checkpoint**: Hooks typecheck, mutation functions callable.

### Task 1.4: Zod validation schemas

- Create `src/dashboard/src/lib/schemas.ts`:
  - `createIssueSchema` — name (required), description (required), issueType (enum), severity (enum), priority (optional enum), stepsToReproduce (optional), expectedBehavior (optional), actualBehavior (optional), affectedComponent (optional), stackTrace (optional)
  - `updateIssueSchema` — all fields optional (PATCH semantics)
  - `createFeatureRequestSchema` — name (required), description (required), category (enum), priority (optional enum), businessValue (optional), userStory (optional), requester (optional), acceptanceSummary (optional)
  - `updateFeatureRequestSchema` — all fields optional
- Enum values must match API exactly: IssueType (6), IssueSeverity (4), Priority (4), CompletionState (9), FeatureStatus (8), FeatureCategory (3)

**Checkpoint**: Schemas validate correct data, reject invalid data.

---

## Phase 2: Create Pages

> Goal: Dedicated create pages with forms for issues and feature requests.

### Task 2.1: Create Issue page

- Create `src/dashboard/src/pages/issue-create-page.tsx`
- Route: `/projects/:id/issues/new` (add to App.tsx, BEFORE the `:issueNumber` route)
- Form sections in cards (matching glassmorphism style):
  - **Required**: Name (input), Description (textarea), IssueType (select: Bug, Defect, Regression, TechnicalDebt, PerformanceIssue, SecurityVulnerability), Severity (select: Critical, Major, Minor, Trivial)
  - **Optional**: Priority (select, default Medium), StepsToReproduce (textarea), ExpectedBehavior (textarea), ActualBehavior (textarea), AffectedComponent (input), StackTrace (textarea)
- Footer: "Create Issue" submit button + "Cancel" link back to list
- On success: toast + navigate to `/projects/:id/issues/:newIssueNumber`
- On error: toast with error message

**Checkpoint**: Can create an issue end-to-end, lands on detail page.

### Task 2.2: Create Feature Request page

- Create `src/dashboard/src/pages/feature-request-create-page.tsx`
- Route: `/projects/:id/feature-requests/new` (add to App.tsx, BEFORE `:featureNumber` route)
- Form sections:
  - **Required**: Name (input), Description (textarea), Category (select: Feature, Enhancement, Improvement)
  - **Optional**: Priority (select, default Medium), BusinessValue (textarea), UserStory (textarea), Requester (input), AcceptanceSummary (textarea)
- Same submit/cancel/toast pattern as issues

**Checkpoint**: Can create a feature request end-to-end.

### Task 2.3: List page integration

- `issues-list-page.tsx`: Add "Create Issue" button (with Plus icon) in the page header, next to the title. Add "Create Issue" button in the empty state card.
- `feature-requests-list-page.tsx`: Same pattern — "Create Feature Request" button in header + empty state.
- Both buttons navigate to the respective `/new` route.

**Checkpoint**: Buttons visible, navigation works, empty state has CTA.

---

## Phase 3: Inline Edit on Detail Pages

> Goal: Toggle edit mode on detail pages to modify fields in-place.

### Task 3.1: Issue detail inline edit

- Modify `src/dashboard/src/pages/issue-detail-page.tsx`:
  - Add `isEditing` state + "Edit" / "Save" / "Cancel" buttons in header
  - When `isEditing = true`:
    - Name → text input
    - Description → textarea
    - IssueType → select dropdown
    - Severity → select dropdown
    - Priority → select dropdown
    - StepsToReproduce, ExpectedBehavior, ActualBehavior → textareas
    - AffectedComponent → text input
    - StackTrace, RootCause, Resolution → textareas
  - When `isEditing = false`: current read-only display (unchanged)
  - Initialize react-hook-form with current issue values via `reset()` on entering edit mode
  - On save: compute diff (only send changed fields), PATCH via `useUpdateIssue`, toast, exit edit mode
  - On cancel: `reset()` form, exit edit mode
  - Non-editable always: IssueId badge, state badge, timestamps, audit log, linked WPs, attachments

**Checkpoint**: Toggle works, save sends minimal PATCH, cancel reverts cleanly.

### Task 3.2: Feature request detail inline edit

- Modify `src/dashboard/src/pages/feature-request-detail-page.tsx`:
  - Same toggle pattern as issues
  - Editable: Name, Description, Category, Priority, BusinessValue, UserStory, Requester, AcceptanceSummary
  - Non-editable: FeatureRequestId, status badge, timestamps, linked WPs, attachments
  - Same diff-based PATCH + toast pattern

**Checkpoint**: Toggle works, minimal PATCH, clean cancel.

---

## Phase 4: State/Status Quick-Actions

> Goal: Dedicated state transition UI with confirmation, separate from edit mode.

### Task 4.1: Issue state quick-action

- Add to issue detail page (visible in both view and edit modes):
  - A "Change State" dropdown/button next to the state badge in the header
  - Shows all 9 CompletionState options (NotStarted, Designing, Implementing, Testing, InReview, Completed, Cancelled, Blocked, Replaced)
  - Current state is indicated / disabled
  - On select: opens confirmation AlertDialog showing "Change state from {old} to {new}?"
  - On confirm: PATCH with `{ state: newState }` via `useUpdateIssue`, toast success
  - On cancel: close dialog, no change

**Checkpoint**: State changes with confirmation, badge updates.

### Task 4.2: Feature request status quick-action

- Same pattern for feature request detail page:
  - "Change Status" dropdown next to status badge
  - All 8 FeatureStatus options (Proposed, UnderReview, Approved, Scheduled, InProgress, Completed, Rejected, Deferred)
  - Confirmation dialog + PATCH `{ status: newStatus }`

**Checkpoint**: Status changes with confirmation, badge updates.

---

## Phase 5: Polish & Validation

> Goal: Ensure everything works together, update documentation.

### Task 5.1: TypeScript + build verification

- Run `npx tsc --noEmit` from dashboard directory
- Run `npm run build` to verify production build
- Fix any type errors

### Task 5.2: Update CLAUDE.md

- Update "Entity Creation & Deletion Ownership" section:
  - Creation: MCP tools AND dashboard (for Issues and Feature Requests)
  - Work Packages remain MCP-only
- Update "Dashboard Routing" section with new routes

### Task 5.3: Run tests

- Run existing test suite, fix any broken tests
- Verify new pages don't break routing tests

**Checkpoint**: Clean build, tests pass, docs updated.

---

## Dependency Graph

```
Phase 1 (Infrastructure)
  ├─ 1.1 Shadcn components
  ├─ 1.2 API client functions
  ├─ 1.3 Mutation hooks (depends on 1.2)
  └─ 1.4 Zod schemas

Phase 2 (Create Pages) — depends on all of Phase 1
  ├─ 2.1 Issue create page
  ├─ 2.2 FR create page
  └─ 2.3 List page buttons (depends on 2.1, 2.2)

Phase 3 (Inline Edit) — depends on Phase 1
  ├─ 3.1 Issue detail edit
  └─ 3.2 FR detail edit

Phase 4 (State Quick-Actions) — depends on Phase 1 (hooks only)
  ├─ 4.1 Issue state action
  └─ 4.2 FR status action

Phase 5 (Polish) — depends on all phases
  ├─ 5.1 Build verification
  ├─ 5.2 CLAUDE.md update
  └─ 5.3 Test run
```

Phases 2, 3, and 4 can be worked in parallel once Phase 1 is complete.

---

## Files Created / Modified

### New Files (7)
| File | Phase |
|------|-------|
| `src/dashboard/src/components/ui/form.tsx` | 1.1 |
| `src/dashboard/src/components/ui/textarea.tsx` | 1.1 |
| `src/dashboard/src/components/ui/sonner.tsx` | 1.1 |
| `src/dashboard/src/lib/schemas.ts` | 1.4 |
| `src/dashboard/src/pages/issue-create-page.tsx` | 2.1 |
| `src/dashboard/src/pages/feature-request-create-page.tsx` | 2.2 |
| `claudedocs/workflow_dashboard_crud.md` | this file |

### Modified Files (9)
| File | Phase | Changes |
|------|-------|---------|
| `package.json` | 1.1 | Add `sonner` dependency |
| `src/dashboard/src/api/issues.ts` | 1.2 | Add `createIssue`, `updateIssue` |
| `src/dashboard/src/api/feature-requests.ts` | 1.2 | Add `createFeatureRequest`, `updateFeatureRequest` |
| `src/dashboard/src/hooks/use-issues.ts` | 1.3 | Add `useCreateIssue`, `useUpdateIssue` |
| `src/dashboard/src/hooks/use-feature-requests.ts` | 1.3 | Add `useCreateFeatureRequest`, `useUpdateFeatureRequest` |
| `src/dashboard/src/App.tsx` | 2.1/2.2 | Add create page routes |
| `src/dashboard/src/pages/issues-list-page.tsx` | 2.3 | Add create button |
| `src/dashboard/src/pages/feature-requests-list-page.tsx` | 2.3 | Add create button |
| `src/dashboard/src/pages/issue-detail-page.tsx` | 3.1 + 4.1 | Inline edit + state quick-action |
| `src/dashboard/src/pages/feature-request-detail-page.tsx` | 3.2 + 4.2 | Inline edit + status quick-action |
| `src/dashboard/src/components/layout/app-layout.tsx` | 1.1 | Add `<Toaster />` |
| `CLAUDE.md` | 5.2 | Update ownership rules + routes |

---

## Execution Estimate

- **Phase 1**: 4 tasks, all parallelizable except 1.3 (depends on 1.2)
- **Phase 2**: 3 tasks, 2.3 depends on 2.1 + 2.2
- **Phase 3**: 2 tasks, fully parallel
- **Phase 4**: 2 tasks, fully parallel
- **Phase 5**: 3 sequential tasks

**Next step**: `/sc:implement` to execute phase by phase.
