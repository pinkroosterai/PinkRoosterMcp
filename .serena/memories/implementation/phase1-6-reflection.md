# Phase 1-6 Implementation Reflection

## Completed: Full vertical slice scaffold
- ActivityLog entity → EF migration → API endpoint → MCP tool → Dashboard page
- Docker Compose orchestration with 4 services (all healthy)
- Auth middleware, request logging, Swagger

## Known Issues to Fix
1. **Hardcoded API key** in `src/dashboard/src/api/client.ts:L4` — should use env var or nginx-injected header
2. **Descending index** in migration generates `new bool[0]` instead of `new[] { true }` — verify with EF
3. **RequestLoggingMiddleware** logs its own `/api/activity-logs` requests (recursive noise) — add path exclusion
4. **Dashboard title** generic "dashboard" in `index.html` — change to "PinkRooster"
5. **Missing favicon** — `/vite.svg` referenced but deleted
6. **ApiRoutes.cs** constants defined but not consumed by controllers
7. **Status card hardcoded** "Online" — should call /health endpoint

## Blocked on Entity Model
- CRUD controllers, MCP tools, dashboard CRUD pages all await domain entity definition
- react-hook-form + zod installed but unused (needed for CRUD forms)

## Not Yet Implemented (deferred, not forgotten)
- docker-compose.override.yml for dev hot reload
- Activity log filtering (date range, method, path)
- Test projects (empty shells)
