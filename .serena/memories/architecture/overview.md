# Architecture Overview

## Stack
- .NET 9 (SDK 10 installed, targeting net9.0), PostgreSQL 17, React 19 + Vite + TypeScript
- Solution: `PinkRooster.slnx` (new .NET slnx format)

## Project Dependency Graph
```
PinkRooster.Shared  ← DTOs, constants, enums (no deps)
       ↑
PinkRooster.Data    ← EF Core + Npgsql (entities, migrations, DbContext)
       ↑
PinkRooster.Api     ← REST API (controllers, services, middleware)

PinkRooster.Mcp     ← refs Shared ONLY, calls API via HTTP
dashboard           ← standalone Vite/React app, proxies to API
PinkRooster.Api.Tests ← xUnit v3 + Testcontainers + Respawn
```

## Key Design Decisions
- Service layer + Controllers (not Clean Architecture/CQRS)
- MCP server communicates with API via HTTP only (no ref to Api/Data)
- Human-readable IDs derived at read-time, never stored. No GUIDs — all PKs are long auto-increment
- Entity creation: MCP tools only. Deletion: dashboard only
- API key auth via X-Api-Key header middleware
- MCP tools must NEVER throw exceptions — always return OperationResult

## Services (Docker)
- API: localhost:5100, MCP: localhost:5200, Dashboard: localhost:5173 (dev) / 3000 (docker), PostgreSQL: 5432
- Health chain: postgres → api → mcp
- Redeploy: `docker compose up -d --build api mcp`, wait ~15s for health checks

## EF Core
- Pinned: EF Core 9.0.13, Npgsql 9.0.4
- Snake_case via fluent config, enums as string via HasConversion<string>()
- Auto-timestamps: UpdatedAt via IHasUpdatedAt + SaveChangesAsync override, CreatedAt via DB default now()
