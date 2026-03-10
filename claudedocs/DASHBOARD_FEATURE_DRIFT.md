# Dashboard Feature Drift Analysis

> Analyzed: 2026-03-10 | API: 40 endpoints | Dashboard coverage: ~45%

## Summary

The dashboard is a **read-only viewer with delete support**. It covers Projects, Issues, and Work Packages (list + detail + delete), but has significant gaps: the entire **FeatureRequest entity has zero dashboard presence**, several API response fields are never displayed, and two compact API endpoints (`get_project_status`, `get_next_actions`) go unused.

---

## 1. Entity Coverage

| Entity | List | Detail | Delete | Notes |
|--------|------|--------|--------|-------|
| Project | OK | OK | OK | No create/edit UI (by design) |
| Issue | OK | OK | OK | Full detail with audit trail, linked WPs, attachments |
| Feature Request | **MISSING** | **MISSING** | **MISSING** | Zero dashboard presence despite full API |
| Work Package | OK | OK | OK | Full detail with phases, tasks, dependencies |
| Phase | Embedded in WP | Embedded in WP | **MISSING** | No standalone page, no delete |
| Task | Embedded in Phase | Embedded in Phase | **MISSING** | No standalone page, no delete |

---

## 2. Feature Request Entity (Critical Gap)

The FeatureRequest entity has a complete API (5 endpoints), 3 MCP tools, 22 integration tests, and full lifecycle support — but nothing in the dashboard.

### What needs to be implemented

**Files to create:**

| File | Purpose |
|------|---------|
| `src/dashboard/src/api/feature-requests.ts` | API client: `getFeatureRequests()`, `getFeatureRequest()`, `deleteFeatureRequest()` |
| `src/dashboard/src/hooks/use-feature-requests.ts` | Hooks: `useFeatureRequests()`, `useFeatureRequest()`, `useDeleteFeatureRequest()` |
| `src/dashboard/src/pages/feature-request-detail-page.tsx` | Detail page showing all FR fields, status, timestamps, linked WPs |

**Files to modify:**

| File | Change |
|------|--------|
| `src/dashboard/src/types/index.ts` | Add `FeatureRequest`, `FeatureRequestSummary`, `FeatureStatus`, `FeatureCategory` types |
| `src/dashboard/src/App.tsx` | Add route: `/projects/:id/feature-requests/:frNumber` |
| `src/dashboard/src/pages/project-detail-page.tsx` | Add "Feature Requests" tab alongside Issues and Work Packages |
| `src/dashboard/src/components/layout/app-sidebar.tsx` | Add Feature Requests nav item |

**API endpoints to consume:**

| Endpoint | Dashboard Usage |
|----------|----------------|
| `GET /api/projects/{id}/feature-requests?state=` | List in project detail tab |
| `GET /api/projects/{id}/feature-requests/{frNumber}` | Detail page |
| `DELETE /api/projects/{id}/feature-requests/{frNumber}` | Delete with confirmation dialog |

**Fields to display on detail page:**
- `featureRequestId`, `name`, `description`, `category`, `priority`, `status`
- `businessValue`, `userStory`, `requester`, `acceptanceSummary`
- `startedAt`, `completedAt`, `resolvedAt`, `createdAt`, `updatedAt`
- `attachments` (file reference list)
- `linkedWorkPackages` (clickable links to WP detail pages)

**Fields to display on list view:**
- `featureRequestId`, `name`, `status` (badge), `priority` (badge), `category`, `requester`, `createdAt`

---

## 3. Unused API Response Fields

### WorkPackage Detail Page — `linkedFeatureRequestId`

The `WorkPackageResponse.LinkedFeatureRequestId` field (e.g., `proj-1-fr-3`) is returned by the API but never rendered on the work package detail page.

**What needs to be implemented:**
- In `work-package-detail-page.tsx`: display `linkedFeatureRequestId` alongside `linkedIssueId` in the metadata section
- Render as a clickable link to `/projects/:id/feature-requests/:frNumber` (once FR detail page exists)

### WorkPackage/Task Detail — `previousActiveState`

When a WP or Task is in `Blocked` state, `previousActiveState` indicates what state it will return to when unblocked. This is never shown.

**What needs to be implemented:**
- In `work-package-detail-page.tsx`: when `state === "Blocked"` and `previousActiveState` is set, show a subtitle like "Was: Implementing" next to the Blocked badge
- Same for tasks in the task list within phase sections

### State Change Cascades — `stateChanges`

When state transitions cause cascades (auto-block, auto-unblock, upward propagation), the API returns a `stateChanges` array. The dashboard never visualizes these.

**What needs to be implemented:**
- Add `StateChangeDto` type to `types/index.ts`: `{ entityType, entityId, oldState, newState, reason }`
- This is low priority since the dashboard is read-only; cascades are only triggered by MCP write operations

---

## 4. Unused API Endpoints

### `GET /api/projects/{projectId}/status`

Returns a compact project status with issue/FR/WP counts by state category, active/inactive item lists, and percent complete. The dashboard instead makes separate calls to `/issues/summary` and `/work-packages/summary`.

**What needs to be implemented:**
- Create `getProjectStatus()` in `api/projects.ts`
- Create `useProjectStatus()` hook
- Add `ProjectStatusResponse` type (with `EntityStatusSummary`, `WorkPackageStatusSummary`, `StatusItem`)
- Replace separate summary calls in `project-detail-page.tsx` with single status call
- Display FR counts in the project detail header (once FR tab exists)

### `GET /api/projects/{projectId}/next-actions`

Returns priority-ordered actionable items (tasks, WPs, issues, FRs) that need attention. Never called.

**What needs to be implemented:**
- Create `getNextActions()` in `api/projects.ts`
- Create `useNextActions()` hook
- Add `NextActionItem` type: `{ type, id, name, priority, state, parentId }`
- Display as a priority-ordered list on `dashboard-page.tsx` (the home page) — this is the most natural place since it currently shows generic stats
- Each item clickable, navigating to the appropriate detail page

---

## 5. Phase & Task Delete Support

Phases and tasks are rendered embedded in the work package detail page but have no delete functionality. Per the design decision ("Deletion: Dashboard only"), these should be deletable.

**What needs to be implemented:**

| File | Change |
|------|--------|
| `src/dashboard/src/api/work-packages.ts` | Add `deletePhase(projectId, wpNumber, phaseNumber)` and `deleteTask(projectId, wpNumber, taskNumber)` |
| `src/dashboard/src/hooks/use-work-packages.ts` | Add `useDeletePhase()` and `useDeleteTask()` mutation hooks |
| `src/dashboard/src/pages/work-package-detail-page.tsx` | Add delete buttons with confirmation dialogs on phase and task rows |

**API endpoints:**
- `DELETE /api/projects/{projectId}/work-packages/{wpNumber}/phases/{phaseNumber}`
- `DELETE /api/projects/{projectId}/work-packages/{wpNumber}/tasks/{taskNumber}`

---

## 6. Dashboard Home Page (`/`)

The dashboard home page (`dashboard-page.tsx`) shows a health status card and basic stats. It could be significantly improved.

**What needs to be implemented:**
- Replace hardcoded "Online" with actual health check result (hook `useHealth()` exists but only used in sidebar)
- Add a "Next Actions" section using `GET /next-actions` endpoint — shows what to work on next
- Add project status summary cards showing issue/FR/WP counts across all projects
- Link to specific entities from the next actions list

---

## 7. Type Definition Gaps

Fields/types missing from `src/dashboard/src/types/index.ts`:

| Type | Fields | Source DTO |
|------|--------|------------|
| `FeatureRequest` | featureRequestId, name, description, category, priority, status, businessValue, userStory, requester, acceptanceSummary, startedAt, completedAt, resolvedAt, attachments, linkedWorkPackages, createdAt, updatedAt | `FeatureRequestResponse` |
| `FeatureStatus` | Proposed, UnderReview, Approved, Scheduled, InProgress, Completed, Rejected, Deferred | `FeatureStatus` enum |
| `FeatureCategory` | Feature, Enhancement, Improvement | `FeatureCategory` enum |
| `ProjectStatus` | projectId, name, status, issues, featureRequests, workPackages | `ProjectStatusResponse` |
| `NextActionItem` | type, id, name, priority, state, parentId | `NextActionItem` |
| `StateChangeDto` | entityType, entityId, oldState, newState, reason | `StateChangeDto` |

---

## 8. Navigation Gaps

### Sidebar (`app-sidebar.tsx`)

Currently has: Dashboard, Projects, Issues, Work Packages, Activity Log.

**What needs to be implemented:**
- Add "Feature Requests" item below Issues (links to `/projects/${selectedProject.id}` like Issues/WPs, navigates to FR tab)

### Project Detail Page Tabs

Currently has: Issues, Work Packages.

**What needs to be implemented:**
- Add "Feature Requests" tab showing the FR list for the project, with same UX as Issues tab (table, state filter badges, click-through to detail)

### Cross-Entity Navigation

Missing navigation paths:
- Issue detail → linked Feature Request (if FR is the source of the issue's WP)
- WP detail → linked Feature Request (field exists but never rendered)
- FR detail → linked Work Packages (clickable list)

---

## 9. Priority-Ordered Implementation Plan

### P0 — Feature Request Visibility (Critical)
1. Add FeatureRequest types to `types/index.ts`
2. Create `api/feature-requests.ts` (get list, get detail, delete)
3. Create `hooks/use-feature-requests.ts` (useFeatureRequests, useFeatureRequest, useDeleteFeatureRequest)
4. Create `pages/feature-request-detail-page.tsx`
5. Add FR tab to `project-detail-page.tsx`
6. Add route in `App.tsx`
7. Add sidebar nav item

### P1 — Display Missing Fields
8. Show `linkedFeatureRequestId` on WP detail page (clickable link)
9. Show `previousActiveState` on blocked WPs and tasks

### P2 — Phase/Task Delete
10. Add delete buttons for phases and tasks on WP detail page
11. Add API client methods and mutation hooks

### P3 — Dashboard Home Improvements
12. Integrate `get_next_actions` on dashboard home page
13. Replace hardcoded health status with live check
14. Add project status summary cards

### P4 — Replace Separate Summary Calls
15. Switch `project-detail-page.tsx` to use `GET /status` instead of separate issue/WP summary endpoints
16. Add `ProjectStatus` and `NextActionItem` types

### P5 — State Change Visualization (Low Priority)
17. Add `StateChangeDto` type
18. Display cascade notifications (toast or inline)
