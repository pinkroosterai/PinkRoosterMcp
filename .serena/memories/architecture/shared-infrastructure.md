# Shared Infrastructure (API)

## Static Helpers
- **StateTransitionHelper**: `ApplyStateTimestamps()`, `ApplyBlockedStateLogic()`, `MapFileReferences()` — operates on marker interfaces
- **ResponseMapper**: `MapTask()`, `MapPhase()`, `MapFileReferences()`, `MapAcceptanceCriterion()`, dependency mappers — shared by WP/Task/Phase services
- **HttpContextExtensions**: `GetCallerIdentity()` — used by all 4 controllers

## DI-Registered Services
- **StateCascadeService**: `PropagateStateUpwardAsync()`, `AutoUnblockDependentWpsAsync/TasksAsync()`, `HasCircularWp/TaskDependencyAsync()` (generic BFS)

## Marker Interfaces (Data/Entities)
- `IHasUpdatedAt`, `IHasStateTimestamps`, `IHasBlockedState`

## Gotchas
- Audit log FK on create: use navigation property (`Entity = entity`), NOT `EntityId = entity.Id`
- Batch task numbering: pre-fetch starting numbers before loop (avoid duplicate key with unsaved entities)
- Service `stateChanges` optional param + `CancellationToken ct`: controllers must use `ct:` named arg to skip stateChanges
- WP dependency: Cascade on both FKs for WorkPackageDependency; Restrict on Task→WP FK (avoid multi-path cascade)
