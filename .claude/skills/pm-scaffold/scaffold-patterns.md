# Scaffold Patterns for PinkRoosterMcp

This file documents the standard phase structure for work packages in this project.
Skills reference this file for consistent scaffolding.

## Vertical Slice Pattern

This project follows a strict vertical slice architecture. Each feature spans
multiple layers, and work packages should be phased by layer with dependencies
flowing top-to-bottom.

### Phase 1: Shared + Data Layer

**Directory**: `src/PinkRooster.Shared/`, `src/PinkRooster.Data/`

**Typical tasks**:
- Add/modify entity class in `src/PinkRooster.Data/Entities/`
- Add EF Core configuration in entity or `AppDbContext`
- Create migration: `dotnet ef migrations add <Name> --project src/PinkRooster.Data --startup-project src/PinkRooster.Api`
- Add/modify DTOs in `src/PinkRooster.Shared/DTOs/Requests/` and `src/PinkRooster.Shared/DTOs/Responses/`
- Add enum values in `src/PinkRooster.Shared/Enums/`
- Add helper methods in `src/PinkRooster.Shared/Helpers/`
- Update `IHasStateTimestamps` / `IHasBlockedState` interfaces if needed

**Patterns to follow**:
- Entities implement marker interfaces (`IHasUpdatedAt`, `IHasStateTimestamps`)
- Per-entity sequential numbering via serializable transaction
- Snake_case DB column naming via fluent configuration
- Request DTOs: all-nullable for PATCH semantics (update), required for POST (create)

### Phase 2: API Layer

**Directory**: `src/PinkRooster.Api/`

**Typical tasks**:
- Create service interface in `src/PinkRooster.Api/Services/`
- Create service implementation
- Create controller in `src/PinkRooster.Api/Controllers/`
- Register service in DI (`Program.cs`)
- Add audit log entries using per-entity audit log tables

**Patterns to follow**:
- Service layer + Controllers (not CQRS)
- `StateTransitionHelper` for state-driven timestamps
- `StateCascadeService` for cross-entity cascades
- `ResponseMapper` for DTO mapping
- `HttpContextExtensions.GetCallerIdentity()` in all controllers
- Nested routes for child entities: `api/projects/{projectId}/...`

### Phase 3: MCP Layer

**Directory**: `src/PinkRooster.Mcp/`

**Typical tasks**:
- Create tool class in `src/PinkRooster.Mcp/Tools/`
- Create input types in `src/PinkRooster.Mcp/Inputs/`
- Create response types in `src/PinkRooster.Mcp/Responses/`
- Add API client methods in `src/PinkRooster.Mcp/Clients/PinkRoosterApiClient.cs`
- Use `McpInputParser` helpers for mapping

**Patterns to follow**:
- MCP tools NEVER throw exceptions — always return OperationResult
- MCP responses are separate from API DTOs (agent-optimized)
- `PinkRoosterApiClient` uses `EnsureSuccessAsync()` for all calls
- Tool annotations: `Title`, `OpenWorld = false`, read tools `ReadOnly = true`
- `McpInputParser` for input mapping, `McpInputParser.NullIfEmpty()` for optional fields

### Phase 4: Dashboard

**Directory**: `src/dashboard/src/`

**Typical tasks**:
- Add API client functions in `src/dashboard/src/api/`
- Create React hooks in `src/dashboard/src/hooks/`
- Create list page in `src/dashboard/src/pages/`
- Create detail page
- Create/edit page (for issues and FRs)
- Add route in `src/dashboard/src/App.tsx`
- Update navigation in `src/dashboard/src/components/layout/app-layout.tsx`

**Patterns to follow**:
- TanStack Query for data fetching, TanStack Table for tables
- Shadcn/ui components with dark-mode-first theming
- `stateColorClass()` from `src/dashboard/src/lib/state-colors.ts` for badges
- Path alias `@/` maps to `src/`
- Recharts for visualization components

### Phase 5: Integration Testing

**Directory**: `tests/PinkRooster.Api.Tests/`, `src/dashboard/src/test/`

**Typical tasks**:
- Create API integration tests using `IntegrationTest` base class
- Add MSW handlers in `src/dashboard/src/test/mocks/handlers.ts`
- Create dashboard component tests using `renderWithProviders()`

**Patterns to follow**:
- xUnit v3 with `TestContext.Current.CancellationToken`
- Testcontainers for real PostgreSQL
- Respawn for per-test database reset
- `ApiFactory` provides authenticated `HttpClient`
- MSW 2.x for API mocking in dashboard tests

## Cross-Phase Dependencies

When using `scaffold_work_package`, tasks can only depend on other tasks
within the same phase (via `dependsOnTaskIndices`). For cross-phase
dependencies, use WP-level or natural ordering — phases execute in order.

Common intra-phase dependencies:
- Entity → Migration → DTO (Phase 1)
- Service interface → Service impl → Controller (Phase 2)
- API client → Hooks → Pages (Phase 4)
- API tests → Dashboard tests (Phase 5, if applicable)
