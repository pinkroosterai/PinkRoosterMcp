# PinkRoosterMcp — Implementation Workflow

## Overview

Phased implementation plan for the PinkRoosterMcp monorepo: API Server, MCP Server, React Dashboard, and Docker infrastructure. Each phase has a checkpoint to validate before proceeding.

---

## Phase 1: Scaffold & Infrastructure

> **Goal**: Empty but buildable solution with Docker Compose running PostgreSQL.

### 1.1 Solution & project scaffolding
- [ ] Create `PinkRooster.sln`
- [ ] Create `src/PinkRooster.Shared/PinkRooster.Shared.csproj` (net9.0 classlib)
- [ ] Create `src/PinkRooster.Data/PinkRooster.Data.csproj` (net9.0 classlib, refs Shared)
- [ ] Create `src/PinkRooster.Api/PinkRooster.Api.csproj` (net9.0 web, refs Data + Shared)
- [ ] Create `src/PinkRooster.Mcp/PinkRooster.Mcp.csproj` (net9.0 web, refs Shared)
- [ ] Add `Directory.Build.props` with shared settings (TargetFramework, Nullable, ImplicitUsings)
- [ ] Add `.gitignore` (dotnet + node + IDE)
- [ ] Add `.editorconfig`

### 1.2 Docker Compose (dev stack)
- [ ] Create `docker-compose.yml` with PostgreSQL service
- [ ] Create `.env.example` with placeholder secrets
- [ ] Create `docker/api.Dockerfile` (multi-stage build)
- [ ] Create `docker/mcp.Dockerfile` (multi-stage build)
- [ ] Create `docker/dashboard.Dockerfile` (Vite build + nginx)

### 1.3 React dashboard scaffolding
- [ ] Initialize Vite + React + TypeScript in `src/dashboard/`
- [ ] Install and configure Shadcn/ui (tailwind, components.json)
- [ ] Install core dependencies: React Router, TanStack Query, TanStack Table, React Hook Form, Zod

**Checkpoint 1**: `dotnet build` succeeds for all C# projects. `npm run dev` starts the dashboard. `docker compose up postgres` starts the database.

---

## Phase 2: Shared Contracts & Data Layer

> **Goal**: Database schema exists, EF migrations run, shared DTOs defined.

### 2.1 Shared DTOs
- [ ] Define request/response DTOs in `PinkRooster.Shared/DTOs/` (placeholder entity — can use a generic `Item` or `Task` shape)
- [ ] Define `ApiRoutes.cs` constants
- [ ] Define `PaginatedResponse<T>` wrapper

### 2.2 Data entities & DbContext
- [ ] Create `AppDbContext` in `PinkRooster.Data`
- [ ] Create initial entity (e.g., `ActivityLog`) with `IEntityTypeConfiguration<T>`
- [ ] Register Npgsql provider
- [ ] Create initial EF migration

### 2.3 Database seeding
- [ ] Add a `DbInitializer` to apply migrations on startup (for development)

**Checkpoint 2**: `dotnet ef database update` creates tables in PostgreSQL. Shared project compiles with DTOs.

---

## Phase 3: API Server Core

> **Goal**: Running API server with auth, request logging, and at least one CRUD endpoint.

### 3.1 Program.cs & DI setup
- [ ] Configure services: DbContext, service layer registrations
- [ ] Configure Swagger/OpenAPI for development
- [ ] Configure CORS (allow dashboard origin)

### 3.2 Authentication middleware
- [ ] Implement `ApiKeyAuthMiddleware` — validates `X-Api-Key` header
- [ ] Configure API key(s) from `appsettings.json` / environment variables

### 3.3 Request logging middleware
- [ ] Implement `RequestLoggingMiddleware` — logs method, path, status, duration, caller identity to `ActivityLog` table
- [ ] Register in pipeline after auth

### 3.4 First CRUD controller + service
- [ ] Create a service interface + implementation for one entity (placeholder until entity model is finalized)
- [ ] Create corresponding controller with full CRUD (GET list, GET by id, POST, PUT, DELETE)
- [ ] Wire up entity ↔ DTO mapping

### 3.5 Activity Log endpoint
- [ ] Create `ActivityLogController` (read-only: GET with pagination + filtering)

**Checkpoint 3**: API starts, Swagger UI loads, CRUD operations work via curl/Swagger, requests appear in activity log.

---

## Phase 4: MCP Server

> **Goal**: MCP server running with HTTP/SSE, tools callable by AI agents.

### 4.1 Program.cs & MCP configuration
- [ ] Configure MCP server with official SDK (`AddMcpServer`, `WithHttpTransport`)
- [ ] Configure stateful sessions (multi-tenancy)
- [ ] Register typed `HttpClient` for API server communication

### 4.2 API client
- [ ] Implement `PinkRoosterApiClient` — typed HttpClient wrapping all API endpoints
- [ ] Include API key forwarding in headers

### 4.3 MCP Tools
- [ ] Create tool classes with `[McpServerToolType]` and `[McpServerTool]` attributes
- [ ] Map each API CRUD operation to an MCP tool
- [ ] Add workflow orchestration tools (state transitions)

**Checkpoint 4**: MCP server starts on port 5200. Tools appear in `tools/list`. Tool invocations reach the API server and return results.

---

## Phase 5: Dashboard — Layout & Core Pages

> **Goal**: Functional dashboard with navigation, data tables, and CRUD forms.

### 5.1 App shell & routing
- [ ] Create `AppLayout` with Shadcn `SidebarProvider` + `Sidebar`
- [ ] Configure React Router with routes: `/`, `/tasks`, `/projects`, `/activity`
- [ ] Set up TanStack Query provider

### 5.2 API client layer
- [ ] Create base fetch wrapper (`src/api/client.ts`) with API key header
- [ ] Create typed API functions for each resource
- [ ] Create TanStack Query hooks per resource

### 5.3 Dashboard page
- [ ] Overview page with summary stats (counts)

### 5.4 Entity CRUD pages
- [ ] DataTable component (Shadcn + TanStack Table) with pagination
- [ ] Create/Edit form dialogs (React Hook Form + Zod validation)
- [ ] Wire up mutations (create, update, delete) with TanStack Query

### 5.5 Activity Log page
- [ ] Read-only DataTable showing agent activity
- [ ] Filtering by date range, method, path

**Checkpoint 5**: Dashboard loads, sidebar navigation works, CRUD operations function end-to-end through the UI.

---

## Phase 6: Docker Integration & Polish

> **Goal**: Full stack runs via `docker compose up`.

### 6.1 Dockerfiles
- [ ] Finalize `api.Dockerfile` — multi-stage build, health check
- [ ] Finalize `mcp.Dockerfile` — multi-stage build, health check
- [ ] Finalize `dashboard.Dockerfile` — Vite build, nginx serve, API proxy config

### 6.2 Docker Compose orchestration
- [ ] Add all services to `docker-compose.yml`
- [ ] Configure inter-service networking (MCP → API via `http://api:8080`)
- [ ] Add health checks and `depends_on` conditions
- [ ] Environment variable configuration via `.env`

### 6.3 Development experience
- [ ] Add `docker-compose.override.yml` for dev (volume mounts, hot reload)
- [ ] Document local dev setup in repo

**Checkpoint 6**: `docker compose up` starts all services. Dashboard at `localhost:3000`, API at `localhost:5100`, MCP at `localhost:5200`. Full end-to-end flow works.

---

## Dependency Map

```
Phase 1 (Scaffold)
    │
    ▼
Phase 2 (Shared + Data) ──────────────────┐
    │                                      │
    ▼                                      ▼
Phase 3 (API Server) ──────► Phase 4 (MCP Server)
    │                              │
    ▼                              │
Phase 5 (Dashboard) ◄─────────────┘
    │
    ▼
Phase 6 (Docker & Polish)
```

- Phases 4 and 5 can run **in parallel** after Phase 3 (API must exist for both to consume).
- Phase 6 can begin partially during Phase 4/5 (Dockerfiles can be drafted early).

---

## Execution Notes

- **Entity model** is intentionally deferred. Phases 2-3 use placeholder entities. When the entity model is defined, add entities to Data, DTOs to Shared, controllers to API, tools to MCP, and pages to Dashboard.
- Each phase should end with a working, testable increment.
- Tests can be added alongside each phase but are not blocking for the workflow.
