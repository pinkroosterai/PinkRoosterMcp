# Entities & Human-Readable IDs

## ID Formats (derived at read-time, never stored)
- Projects: `proj-{Id}`
- Issues: `proj-{ProjectId}-issue-{IssueNumber}` (IssueNumber is per-project sequential)
- Work Packages: `proj-{ProjectId}-wp-{WpNumber}`
- Phases: `proj-{ProjectId}-wp-{WpNumber}-phase-{PhaseNumber}`
- Tasks: `proj-{ProjectId}-wp-{WpNumber}-task-{TaskNumber}`
- IdParser utility in Shared/Helpers for all formats

## Entities
- **Project** — Name, Description, ProjectPath (unique), Status
- **Issue** — per-project IssueNumber, CompletionState, IssueType/Severity/Priority, FileReference (jsonb)
- **WorkPackage** — per-project WpNumber, CompletionState, WorkPackageType/Priority, Plan (markdown), LinkedIssueId
- **WorkPackagePhase** — per-WP PhaseNumber, SortOrder, AcceptanceCriteria children
- **WorkPackageTask** — per-WP TaskNumber (across phases), SortOrder, TargetFiles + Attachments (jsonb)
- **AcceptanceCriterion** — belongs to Phase, VerificationMethod enum
- **WorkPackageDependency / WorkPackageTaskDependency** — self-referencing dependency tables
- **IssueAuditLog, WorkPackageAuditLog, PhaseAuditLog, TaskAuditLog** — full-field audit per entity
- **ActivityLog** — HTTP request logging (auto-populated by middleware)

## Enums
- CompletionState (9 values, 3 categories: Active/Inactive/Terminal), IssueType (6), IssueSeverity (4), Priority (4), WorkPackageType (5), VerificationMethod (3)

## State Management
- State-driven timestamps via `StateTransitionHelper.ApplyStateTimestamps(IHasStateTimestamps)`
- Blocked state logic via `StateTransitionHelper.ApplyBlockedStateLogic(IHasBlockedState)` — captures/restores PreviousActiveState
- Cascades via `StateCascadeService`: upward propagation, auto-unblock, circular dependency detection (BFS)
- All transitions allowed (no validation). PATCH semantics: null = don't change
