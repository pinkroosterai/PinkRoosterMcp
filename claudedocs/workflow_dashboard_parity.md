# Dashboard Parity Implementation Workflow

> Source: `claudedocs/DASHBOARD_FEATURE_DRIFT.md` | Strategy: systematic | 6 phases

## Scope

Close all dashboard feature gaps identified in the drift analysis. Ordered by priority: P0 (Feature Request entity), P1 (missing fields), P2 (phase/task delete), P3 (dashboard home), P4 (project status consolidation).

P5 (state change visualization) is deferred — cascades only occur via MCP writes and the dashboard is read-only.

---

## Phase 1: Types & API Layer

**Goal**: Foundation — TypeScript types and API client functions for all new data.

### Task 1.1 — Add Feature Request types to `types/index.ts`

Add after the existing `WorkPackage` interface:

```typescript
export interface FeatureRequest {
  featureRequestId: string;
  id: number;
  featureRequestNumber: number;
  projectId: string;
  name: string;
  description: string;
  category: string;
  priority: string;
  status: string;
  businessValue: string | null;
  userStory: string | null;
  requester: string | null;
  acceptanceSummary: string | null;
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  attachments: FileReference[];
  linkedWorkPackages: LinkedWorkPackageItem[];
  createdAt: string;
  updatedAt: string;
}
```

Add `ProjectStatus` and `NextActionItem` types:

```typescript
export interface StatusItem {
  id: string;
  name: string;
}

export interface EntityStatusSummary {
  total: number;
  active: number;
  inactive: number;
  terminal: number;
  percentComplete: number;
  activeItems: StatusItem[];
  inactiveItems: StatusItem[];
}

export interface WorkPackageStatusSummary extends EntityStatusSummary {
  terminalCount: number;
  blocked: StatusItem[];
}

export interface ProjectStatus {
  projectId: string;
  name: string;
  status: string;
  issues: EntityStatusSummary;
  featureRequests: EntityStatusSummary;
  workPackages: WorkPackageStatusSummary;
}

export interface NextActionItem {
  type: string;
  id: string;
  name: string;
  priority: string;
  state: string;
  parentId: string;
}
```

**Checkpoint**: `npm run build` passes with no type errors.

### Task 1.2 — Create `api/feature-requests.ts`

Follow the pattern from `api/issues.ts`:

```typescript
import { apiFetch } from "./client";
import type { FeatureRequest } from "@/types";

export function getFeatureRequests(projectId: number, stateFilter?: string): Promise<FeatureRequest[]> {
  const params = stateFilter ? `?state=${encodeURIComponent(stateFilter)}` : "";
  return apiFetch<FeatureRequest[]>(`/projects/${projectId}/feature-requests${params}`);
}

export function getFeatureRequest(projectId: number, frNumber: number): Promise<FeatureRequest> {
  return apiFetch<FeatureRequest>(`/projects/${projectId}/feature-requests/${frNumber}`);
}

export function deleteFeatureRequest(projectId: number, frNumber: number): Promise<void> {
  return apiFetch(`/projects/${projectId}/feature-requests/${frNumber}`, { method: "DELETE" });
}
```

### Task 1.3 — Add project status & next-actions to `api/projects.ts`

Append to existing file:

```typescript
export function getProjectStatus(projectId: number): Promise<ProjectStatus> {
  return apiFetch<ProjectStatus>(`/projects/${projectId}/status`);
}

export function getNextActions(projectId: number, limit?: number, entityType?: string): Promise<NextActionItem[]> {
  const params = new URLSearchParams();
  if (limit) params.set("limit", String(limit));
  if (entityType) params.set("entityType", entityType);
  const qs = params.toString();
  return apiFetch<NextActionItem[]>(`/projects/${projectId}/next-actions${qs ? `?${qs}` : ""}`);
}
```

### Task 1.4 — Add phase/task delete to `api/work-packages.ts`

Append to existing file:

```typescript
export function deletePhase(projectId: number, wpNumber: number, phaseNumber: number): Promise<void> {
  return apiFetch(`/projects/${projectId}/work-packages/${wpNumber}/phases/${phaseNumber}`, { method: "DELETE" });
}

export function deleteTask(projectId: number, wpNumber: number, taskNumber: number): Promise<void> {
  return apiFetch(`/projects/${projectId}/work-packages/${wpNumber}/tasks/${taskNumber}`, { method: "DELETE" });
}
```

**Checkpoint**: `npm run build` passes. All API functions compile with correct types.

---

## Phase 2: Hooks

**Goal**: TanStack Query hooks for all new data sources.

### Task 2.1 — Create `hooks/use-feature-requests.ts`

Follow the pattern from `hooks/use-issues.ts`:

- `useFeatureRequests(projectId, stateFilter?)` — queryKey: `["feature-requests", projectId, stateFilter]`
- `useFeatureRequest(projectId, frNumber)` — queryKey: `["feature-request", projectId, frNumber]`
- `useDeleteFeatureRequest()` — invalidates `["feature-requests"]` on success

### Task 2.2 — Add hooks to `hooks/use-projects.ts`

- `useProjectStatus(projectId)` — queryKey: `["project-status", projectId]`, enabled: `projectId !== undefined`
- `useNextActions(projectId, limit?, entityType?)` — queryKey: `["next-actions", projectId, limit, entityType]`

### Task 2.3 — Add hooks to `hooks/use-work-packages.ts`

- `useDeletePhase()` — mutationFn calls `deletePhase()`, invalidates `["work-package"]` on success
- `useDeleteTask()` — mutationFn calls `deleteTask()`, invalidates `["work-package"]` on success

**Checkpoint**: `npm run build` passes. All hooks compile.

---

## Phase 3: Feature Request Detail Page

**Goal**: Full detail page for a single feature request, following `issue-detail-page.tsx` patterns.

### Task 3.1 — Create `pages/feature-request-detail-page.tsx`

**Structure** (mirrors issue detail):
1. Header: back button, FR name, `featureRequestId` badge, status badge, delete button
2. Definition card: name, description, category badge, priority badge, requester
3. User Story & Business Value card (conditional — only if either field set)
4. Acceptance Summary card (conditional)
5. Related Work Packages section (table: WP ID, name, state, type, priority — each row clickable → WP detail)
6. Attachments section (conditional)
7. Timeline card: createdAt, updatedAt, startedAt, completedAt, resolvedAt
8. Delete confirmation dialog (AlertDialog)

**Status colors** — define `featureStatusColors` map:
```typescript
const featureStatusColors: Record<string, string> = {
  Proposed: "bg-gray-100 text-gray-700",
  UnderReview: "bg-blue-100 text-blue-700",
  Approved: "bg-indigo-100 text-indigo-700",
  Scheduled: "bg-purple-100 text-purple-700",
  InProgress: "bg-yellow-100 text-yellow-700",
  Completed: "bg-green-100 text-green-700",
  Rejected: "bg-red-100 text-red-700",
  Deferred: "bg-orange-100 text-orange-700",
};
```

**Category badge variants**: Feature → default, Enhancement → secondary, Improvement → outline.

**Navigation on delete**: `navigate(`/projects/${id}`)`.

**Params**: `useParams<{ id: string; featureNumber: string }>()`, convert to `Number()`.

### Task 3.2 — Add route to `App.tsx`

Add inside the `<Routes>` block:
```tsx
<Route path="/projects/:id/feature-requests/:featureNumber" element={<FeatureRequestDetailPage />} />
```

Import the new page component.

**Checkpoint**: Navigate to `/projects/1/feature-requests/1` and verify the page renders with real data from the API (requires running containers).

---

## Phase 4: Project Detail — Feature Requests Tab

**Goal**: Add a third tab to the project detail page, with the same UX as the Issues and Work Packages tabs.

### Task 4.1 — Extend `project-detail-page.tsx` tab state

Change tab state type from `"issues" | "work-packages"` to `"issues" | "feature-requests" | "work-packages"`.

### Task 4.2 — Add FR data hooks

Add to the page component:
```typescript
const [frFilter, setFrFilter] = useState<string | undefined>();
const { data: featureRequests, isLoading: frLoading } = useFeatureRequests(project?.id, frFilter);
```

### Task 4.3 — Add FR tab button

Add a third tab button between Issues and Work Packages (or after WPs):
```tsx
<button onClick={() => setActiveTab("feature-requests")} className={...}>
  Feature Requests {featureRequests && `(${featureRequests.length})`}
</button>
```

### Task 4.4 — Add FR tab content

Render when `activeTab === "feature-requests"`:

1. **Filter buttons**: All, Active (UnderReview/Approved/Scheduled/InProgress), Inactive (Proposed/Deferred), Terminal (Completed/Rejected)
2. **Table columns**: ID, Name, Category, Priority, Status, Requester, Created
3. **Row click** → navigate to `/projects/${id}/feature-requests/${fr.featureRequestNumber}`
4. **Delete button** per row with AlertDialog confirmation
5. **Empty state**: "No feature requests found" with Lightbulb icon

### Task 4.5 — Add sidebar nav item

In `app-sidebar.tsx`, add to the project-context nav items:
```typescript
{ title: "Feature Requests", href: `/projects/${selectedProject.id}`, icon: Lightbulb }
```

Import `Lightbulb` from `lucide-react`. The click navigates to project detail; the tab can be auto-selected via URL search param or default behavior.

**Checkpoint**: Project detail page shows 3 tabs. FR tab lists feature requests with filtering, click-through to detail, and delete with confirmation.

---

## Phase 5: Display Missing Fields & Phase/Task Delete

**Goal**: Close field display gaps and add phase/task delete buttons.

### Task 5.1 — Show `linkedFeatureRequestId` on WP detail page

In `work-package-detail-page.tsx`, in the metadata section near `linkedIssueId`:

```tsx
{wp.linkedFeatureRequestId && (
  <div>
    <span className="text-sm text-muted-foreground">Linked Feature Request</span>
    <Link to={`/projects/${id}/feature-requests/${wp.linkedFeatureRequestId.split("-fr-")[1]}`}>
      <Badge variant="outline">{wp.linkedFeatureRequestId}</Badge>
    </Link>
  </div>
)}
```

### Task 5.2 — Show `previousActiveState` on blocked entities

In `work-package-detail-page.tsx`:
- When WP state is "Blocked" and `previousActiveState` is set, render after the state badge: `(was: {previousActiveState})`
- Same pattern for tasks in the task list section

### Task 5.3 — Add phase delete buttons

In `work-package-detail-page.tsx`, on each phase header row:
- Add a trash icon button (same pattern as WP/Issue delete)
- Wire to `useDeletePhase()` mutation
- AlertDialog confirmation: "Delete phase '{name}'? All tasks in this phase will also be deleted."
- On success: invalidate `["work-package", projectId, wpNumber]`

### Task 5.4 — Add task delete buttons

On each task row within a phase:
- Add a trash icon button
- Wire to `useDeleteTask()` mutation
- AlertDialog confirmation: "Delete task '{name}'?"
- On success: invalidate `["work-package", projectId, wpNumber]`

**Checkpoint**: WP detail shows linked FR as clickable link. Blocked entities show previous state. Phase and task rows have working delete buttons.

---

## Phase 6: Dashboard Home Improvements

**Goal**: Make the dashboard home page (`/`) useful with real project data.

### Task 6.1 — Live health status

Replace the hardcoded "Online" text in `dashboard-page.tsx`:
- Use the existing `useHealth()` hook (already defined in `hooks/use-health.ts`)
- Show green "Online" / red "Offline" / yellow "Checking..." based on hook state

### Task 6.2 — Next Actions section

Add a "Next Actions" card below the health status:
- Use `useNextActions(selectedProject?.id, 10)` to fetch top 10 actionable items
- Render as a list: priority badge, type badge (Task/WP/Issue/FR), name, state
- Each item clickable → navigates to the appropriate detail page based on `type` and `id`
- Show "Select a project to see next actions" when no project selected
- Show "No actionable items" when list is empty

### Task 6.3 — Project status summary cards

Add summary cards showing the selected project's status:
- Use `useProjectStatus(selectedProject?.id)` hook
- Render 3 summary cards: Issues (active/terminal/%), Feature Requests (active/terminal/%), Work Packages (active/blocked/%)
- Each card clickable → navigates to project detail with appropriate tab

**Checkpoint**: Dashboard home shows live health, next actions list, and project status summary. All items are clickable and navigate correctly.

---

## Validation Checklist

After all phases, verify:

- [ ] `npm run build` — zero errors
- [ ] `npm run lint` — no new warnings
- [ ] Feature request list renders in project detail (3rd tab)
- [ ] Feature request detail page shows all fields
- [ ] FR detail → linked WP clickable → WP detail
- [ ] WP detail → linked FR clickable → FR detail
- [ ] FR delete with confirmation works, list refreshes
- [ ] Phase delete with confirmation works, WP detail refreshes
- [ ] Task delete with confirmation works, WP detail refreshes
- [ ] Blocked WPs/tasks show `previousActiveState`
- [ ] Dashboard home shows live health status
- [ ] Dashboard home shows next actions (clickable)
- [ ] Dashboard home shows project status summary
- [ ] Sidebar shows Feature Requests nav item when project selected
- [ ] All new routes work with direct URL navigation (refresh)
- [ ] Responsive layout on mobile/tablet breakpoints

---

## File Change Summary

### New Files (3)
| File | Phase |
|------|-------|
| `src/dashboard/src/api/feature-requests.ts` | 1 |
| `src/dashboard/src/hooks/use-feature-requests.ts` | 2 |
| `src/dashboard/src/pages/feature-request-detail-page.tsx` | 3 |

### Modified Files (9)
| File | Phase | Changes |
|------|-------|---------|
| `src/dashboard/src/types/index.ts` | 1 | Add FeatureRequest, ProjectStatus, NextActionItem, StatusItem types |
| `src/dashboard/src/api/projects.ts` | 1 | Add getProjectStatus(), getNextActions() |
| `src/dashboard/src/api/work-packages.ts` | 1 | Add deletePhase(), deleteTask() |
| `src/dashboard/src/hooks/use-projects.ts` | 2 | Add useProjectStatus(), useNextActions() |
| `src/dashboard/src/hooks/use-work-packages.ts` | 2 | Add useDeletePhase(), useDeleteTask() |
| `src/dashboard/src/App.tsx` | 3 | Add FR detail route |
| `src/dashboard/src/pages/project-detail-page.tsx` | 4 | Add Feature Requests tab (state, hooks, filter, table, delete) |
| `src/dashboard/src/components/layout/app-sidebar.tsx` | 4 | Add Feature Requests nav item |
| `src/dashboard/src/pages/work-package-detail-page.tsx` | 5 | Show linkedFeatureRequestId, previousActiveState, phase/task delete |
| `src/dashboard/src/pages/dashboard-page.tsx` | 6 | Live health, next actions, project status summary |

### Unchanged (by design)
- No API or backend changes required — all endpoints already exist
- No new Shadcn/ui components needed — all required components already installed
- No package.json changes — all dependencies already present
