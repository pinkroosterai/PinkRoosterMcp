<div align="center">

# PinkRooster

**AI-native project management for coding agents**

Track issues, plan features, scaffold work packages, and manage your entire development lifecycle — all driven by your AI coding assistant through the [Model Context Protocol](https://modelcontextprotocol.io/).

[Getting Started](#getting-started) · [How It Works](#how-it-works) · [MCP Tools](#mcp-tools) · [Dashboard](#dashboard) · [PM Skills](#pm-workflow-skills)

</div>

---

## What is PinkRooster?

PinkRooster is a project management system purpose-built for AI-assisted development workflows. Instead of switching between your IDE and a project tracker, your AI agent manages everything — creating issues, breaking down features into work packages, tracking progress, and marking tasks complete — all while it writes your code.

It exposes an [MCP server](https://modelcontextprotocol.io/) with 18 tools that any MCP-compatible client (Claude Code, Cursor, Windsurf, etc.) can use to read and write project data. A React dashboard gives you full visibility into what your agent has been doing.

### Why not Jira / Linear / GitHub Issues?

| | Traditional PM tools | PinkRooster |
|---|---|---|
| **Created for** | Humans typing in web UIs | AI agents calling structured tools |
| **Work breakdown** | Manual — you write tickets | Automatic — agent scaffolds phases, tasks, and dependencies from a description |
| **State management** | Manual drag-and-drop | Automatic cascading — completing a task can auto-complete its phase, work package, and linked issue |
| **Context switching** | Tab to browser, find ticket, update | Zero — the agent updates state inline while coding |
| **Audit trail** | Varies | Full-field audit log on every entity, plus HTTP request logging |

---

## Dashboard

A real-time React dashboard lets you see everything your AI agent is tracking — at a glance or in full detail.

### Project Overview

The dashboard home shows active counts, completion percentages, and priority next actions across all entity types.

<div align="center">
<img src="docs/screenshots/dashboard-overview.png" alt="Dashboard overview with project status cards showing issues, feature requests, and work packages with completion donut charts" width="900" />
</div>

### Issue Tracking

Filter and sort issues by severity, priority, type, and state. Summary cards show active/inactive/terminal breakdowns with mini donut charts.

<div align="center">
<img src="docs/screenshots/issues-list.png" alt="Issues list page with state filter tabs, search filters, and sortable data table" width="900" />
</div>

### Rich Detail Pages

Every entity has a structured detail page with inline editing, state management, related entities, timeline, and a collapsible audit log.

<div align="center">
<img src="docs/screenshots/issue-detail.png" alt="Issue detail page showing definition, reproduction steps, resolution, related work packages, and timeline" width="900" />
</div>

### Work Packages

Work packages are the execution unit — each one contains phases and tasks with dependency tracking and automatic state propagation.

<div align="center">
<img src="docs/screenshots/work-package-detail.png" alt="Work package detail page with definition, estimation, linked issue, timeline, and phases with tasks" width="900" />
</div>

### Feature Requests

Track ideas from proposal through completion with an 8-state lifecycle. Link feature requests to work packages to connect "what" to "how."

<div align="center">
<img src="docs/screenshots/feature-requests-list.png" alt="Feature requests list with category and priority filters" width="900" />
</div>

### Activity Log

Every API request is logged with method, path, status, duration, and caller identity — giving you full observability into agent behavior.

<div align="center">
<img src="docs/screenshots/activity-log.png" alt="Activity log showing timestamped API requests with method badges, humanized paths, and response times" width="900" />
</div>

---

## How It Works

```
┌─────────────────┐     MCP (Streamable HTTP)     ┌─────────────────┐
│  Claude Code /  │ ◄──────────────────────────► │   MCP Server    │
│  Cursor / etc.  │                                │   :5200         │
└─────────────────┘                                └────────┬────────┘
                                                            │ HTTP
┌─────────────────┐          HTTP / REST           ┌────────▼────────┐
│   Dashboard     │ ◄──────────────────────────► │   REST API      │
│   :5173         │                                │   :5100         │
└─────────────────┘                                └────────┬────────┘
                                                            │ EF Core
                                                   ┌────────▼────────┐
                                                   │   PostgreSQL    │
                                                   │   :5432         │
                                                   └─────────────────┘
```

**Key design decisions:**
- The MCP server calls the API over HTTP — no shared database access, clean separation
- Human-readable IDs everywhere (`proj-1-issue-3`, `proj-1-wp-2-task-5`) — easy for both agents and humans
- State cascades automatically — completing all tasks in a phase completes the phase, which can complete the work package, which can complete linked issues and feature requests
- Full audit trail — every field change is recorded with old/new values

---

## Entities

| Entity | Purpose | States |
|--------|---------|--------|
| **Project** | Top-level container, identified by filesystem path | Active |
| **Issue** | Bugs, defects, regressions, tech debt, performance, security | NotStarted → Designing → Implementing → Testing → InReview → Completed |
| **Feature Request** | Ideas and enhancements with business context | Proposed → UnderReview → Approved → Scheduled → InProgress → Completed |
| **Work Package** | Execution plan with phases, tasks, and dependencies | Same as Issue + automatic upward propagation |
| **Phase** | Grouping of related tasks within a work package | Auto-completes when all tasks reach terminal state |
| **Task** | Atomic unit of work with target files | Same states as Issue, supports dependency blocking |

---

## MCP Tools

PinkRooster exposes 18 MCP tools organized by entity:

| Tool | Description |
|------|-------------|
| `get_project_status` | Compact status dashboard — counts, active/blocked items |
| `get_next_actions` | Priority-ordered actionable items across all entity types |
| `create_or_update_issue` | Create or update issues with full field support |
| `get_issue_details` / `get_issue_overview` | Read issue data |
| `create_or_update_feature_request` | Create or update feature requests |
| `get_feature_request_details` / `get_feature_requests` | Read feature request data |
| `create_or_update_work_package` | Create or update work packages |
| `get_work_package_details` / `get_work_packages` | Read work package trees |
| `scaffold_work_package` | One-call creation of WP + phases + tasks + dependencies |
| `create_or_update_phase` / `create_or_update_task` | Manage phases and tasks |
| `batch_update_task_states` | Update multiple task states in one call |
| `manage_work_package_dependency` / `manage_task_dependency` | Add/remove dependencies with auto-block |

All write operations return structured `OperationResult` JSON with state change cascades, so the agent always knows what happened downstream.

---

## PM Workflow Skills

Seven Claude Code slash commands provide high-level project management workflows on top of the MCP tools:

| Command | What it does |
|---------|-------------|
| `/pm-status` | Show project dashboard with counts, blocked items, and next actions |
| `/pm-next` | Pick the highest-priority item and start implementing |
| `/pm-done <id>` | Mark entity completed, report all cascades |
| `/pm-implement <id>` | Full implementation loop — read context, write code, run tests, update state |
| `/pm-scaffold <desc>` | Scaffold a work package from a description or linked issue/FR |
| `/pm-plan <desc>` | Create an issue or FR from natural language, optionally scaffold |
| `/pm-triage` | Analyze and prioritize open items |

Skills automatically propagate state to related entities. Starting a task activates its work package and linked issue/FR. Completing all tasks cascades completion upward through phases, work packages, and linked entities.

<div align="center">
<img src="docs/screenshots/skills-help.png" alt="PM Workflow Skills help page showing all seven slash commands with usage syntax and auto-state propagation rules" width="900" />
</div>

---

## Getting Started

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (for local development)
- [Node.js 20+](https://nodejs.org/) (for dashboard development)

### Quick Start (Docker)

```bash
# Clone and configure
git clone https://github.com/your-org/PinkRoosterMcp.git
cd PinkRoosterMcp
make setup        # Copy .env from template, install dashboard deps

# Start everything
make up           # PostgreSQL + API + MCP server + Dashboard

# Verify
docker compose ps # All services should show "healthy"
```

Services will be available at:
| Service | URL |
|---------|-----|
| Dashboard | http://localhost:3000 |
| REST API | http://localhost:5100 |
| MCP Server | http://localhost:5200 |
| Swagger | http://localhost:5100/swagger/index.html |

### Connect Your AI Agent

Add the MCP server to your agent's configuration. For Claude Code, it's already configured in `.mcp.json`:

```json
{
  "mcpServers": {
    "pinkrooster": {
      "type": "url",
      "url": "http://localhost:5200"
    }
  }
}
```

For other MCP clients, point them to `http://localhost:5200` (Streamable HTTP) or `http://localhost:5200/sse` (legacy SSE).

### Local Development

```bash
make dev          # Start all services with hot reload
make dev-api      # API only (hot reload)
make dev-dashboard # Dashboard only (Vite dev server)
```

### Authentication (Optional)

Both the MCP server and dashboard support optional authentication:

```bash
# In .env
API_KEY=your-api-key          # API authentication
MCP_API_KEY=your-mcp-key      # MCP server authentication
DASHBOARD_USER=admin           # Dashboard login
DASHBOARD_PASSWORD=secret      # Dashboard password
```

When no keys are configured, everything runs with open access — ideal for local development.

---

## Testing

```bash
# .NET integration tests (requires Docker for Testcontainers)
dotnet test

# Dashboard frontend tests
cd src/dashboard && npm test
```

The test suite includes 126 API/MCP integration tests and 101 dashboard frontend tests.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| MCP Server | .NET 9, [ModelContextProtocol SDK](https://github.com/modelcontextprotocol/csharp-sdk) |
| REST API | .NET 9, ASP.NET Core |
| Database | PostgreSQL 17, EF Core 9 |
| Dashboard | React 19, TypeScript, Vite, Tailwind CSS v4, shadcn/ui, TanStack Query/Table, Recharts |
| Testing | xUnit v3, Testcontainers, Respawn, Vitest, React Testing Library, MSW |
| Infrastructure | Docker Compose, nginx |

---

## License

MIT
