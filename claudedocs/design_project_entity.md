# Design: Project Entity — Full Vertical Slice

## Overview

Introduces the **Project** domain entity — the first business entity and root navigation node in PinkRooster. Projects are created exclusively via MCP tools (by AI agents), viewable and deletable via the dashboard, and served by a REST API that both consumers share.

### Key Architectural Decisions

1. **MCP ↔ API response boundary**: The API returns its own DTOs (in Shared). The MCP layer maps these to MCP-specific response classes before returning to agents. MCP response types (`OperationResult`, `ResponseType`, tool-specific responses) live exclusively in the MCP project — never in Shared or API.
2. **Human-readable ID**: `proj-{Id}` derived at read-time from the DB auto-increment PK. Never stored as a column.
3. **No GUIDs**: The entire slice uses `long` for primary keys.
4. **Project creation**: MCP tools only. No create/edit UI in dashboard.
5. **Project deletion**: Dashboard only (with confirmation dialog). No MCP delete tool.

---

## 1. Database Schema

```sql
CREATE TABLE projects (
    id            bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name          varchar(200) NOT NULL,
    description   varchar(1000) NOT NULL,
    project_path  varchar(1024) NOT NULL,
    status        varchar(20)  NOT NULL DEFAULT 'Active',
    created_at    timestamptz  NOT NULL DEFAULT now(),
    updated_at    timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ix_projects_project_path ON projects (project_path);
CREATE INDEX ix_projects_status ON projects (status);
```

`updated_at` is auto-set via `SaveChangesAsync` override in `AppDbContext`.

---

## 2. Shared Layer

### Enums

**`Enums/ProjectStatus.cs`**
```csharp
namespace PinkRooster.Shared.Enums;

public enum ProjectStatus
{
    Active,
    Archived
}
```

### DTOs

**`DTOs/Requests/CreateOrUpdateProjectRequest.cs`**
```csharp
namespace PinkRooster.Shared.DTOs.Requests;

public sealed class CreateOrUpdateProjectRequest
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ProjectPath { get; init; }
}
```

**`DTOs/Responses/ProjectResponse.cs`**
```csharp
namespace PinkRooster.Shared.DTOs.Responses;

public sealed class ProjectResponse
{
    public required string ProjectId { get; init; }        // "proj-42"
    public required long Id { get; init; }                 // 42 (for dashboard delete)
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ProjectPath { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
```

### Constants

**`Constants/ApiRoutes.cs`** — add:
```csharp
public static class Projects
{
    public const string Route = $"{Base}/projects";
}
```

---

## 3. Data Layer

### Entity

**`Entities/Project.cs`**
```csharp
namespace PinkRooster.Data.Entities;

public sealed class Project
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string ProjectPath { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### Configuration

**`Configurations/ProjectConfiguration.cs`**
```csharp
namespace PinkRooster.Data.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ProjectPath).HasColumnName("project_path").HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20)
            .HasConversion<string>().HasDefaultValue(ProjectStatus.Active);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasIndex(x => x.ProjectPath).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
```

### AppDbContext Changes

```csharp
// Add DbSet
public DbSet<Project> Projects => Set<Project>();

// Override SaveChangesAsync to auto-set UpdatedAt
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    foreach (var entry in ChangeTracker.Entries<Project>()
        .Where(e => e.State == EntityState.Modified))
    {
        entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
    }
    return base.SaveChangesAsync(cancellationToken);
}
```

> **Note**: The `SaveChangesAsync` override targets `Project` specifically. If more entities need auto-timestamps later, introduce a shared `IHasTimestamps` interface and generalize at that point.

### Migration

```bash
dotnet ef migrations add AddProjectEntity --project src/PinkRooster.Data --startup-project src/PinkRooster.Api
```

---

## 4. API Layer

### Service Interface

**`Services/IProjectService.cs`**
```csharp
namespace PinkRooster.Api.Services;

public interface IProjectService
{
    Task<List<ProjectResponse>> GetAllAsync(CancellationToken ct = default);
    Task<ProjectResponse?> GetByPathAsync(string projectPath, CancellationToken ct = default);
    Task<(ProjectResponse Project, bool IsNew)> CreateOrUpdateAsync(
        CreateOrUpdateProjectRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
```

### Service Implementation

**`Services/ProjectService.cs`**

Key behaviors:
- `GetAllAsync` → returns all projects ordered by `CreatedAt` desc, maps to `ProjectResponse`
- `GetByPathAsync` → finds by exact path match, returns null if not found
- `CreateOrUpdateAsync` → queries by path; if exists, updates `Name`/`Description`/`Status`; if not, creates. Returns tuple with `isNew` flag
- `DeleteAsync` → finds by DB id, removes, returns success boolean
- Mapping: `Project` entity → `ProjectResponse` with `ProjectId = $"proj-{entity.Id}"`

### Controller

**`Controllers/ProjectController.cs`**

```
[ApiController]
[Route(ApiRoutes.Projects.Route)]
public sealed class ProjectController(IProjectService projectService) : ControllerBase

    [HttpGet]
    GetAll() → Ok(List<ProjectResponse>)

    [HttpGet] with [FromQuery] string path
    GetByPath(string path) → Ok(ProjectResponse) | NotFound()
    // Note: Both GetAll and GetByPath are on [HttpGet].
    // Differentiated by: if `path` query param is present → GetByPath, else → GetAll.
    // Implemented as single method: if path is null/empty → return all, else → find by path.

    [HttpPut]
    CreateOrUpdate(CreateOrUpdateProjectRequest request)
    → isNew ? Created (201) with ProjectResponse : Ok (200) with ProjectResponse

    [HttpDelete("{id:long}")]
    Delete(long id) → NoContent (204) | NotFound (404)
```

### DI Registration

In `Program.cs`:
```csharp
builder.Services.AddScoped<IProjectService, ProjectService>();
```

---

## 5. MCP Layer

### Response Types

All MCP-specific. These live in `PinkRooster.Mcp/Responses/`.

**`Responses/ResponseType.cs`**
```csharp
namespace PinkRooster.Mcp.Responses;

public enum ResponseType
{
    Success,
    Warning,
    Error
}
```

**`Responses/JsonDefaults.cs`**
```csharp
namespace PinkRooster.Mcp.Responses;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Indented = new(JsonSerializerOptions.Web)
    {
        WriteIndented = true
    };
}
```

**`Responses/OperationResult.cs`**
```csharp
namespace PinkRooster.Mcp.Responses;

public record OperationResult(ResponseType ResponseType, string Message)
{
    public static string Success(string message) =>
        JsonSerializer.Serialize(new OperationResult(ResponseType.Success, message), JsonDefaults.Indented);

    public static string Warning(string message) =>
        JsonSerializer.Serialize(new OperationResult(ResponseType.Warning, message), JsonDefaults.Indented);

    public static string Error(string message) =>
        JsonSerializer.Serialize(new OperationResult(ResponseType.Error, message), JsonDefaults.Indented);
}
```

**`Responses/ProjectOverviewResponse.cs`**

Tailored for AI agent consumption — excludes timestamps, keeps only what an agent needs.

```csharp
namespace PinkRooster.Mcp.Responses;

public sealed class ProjectOverviewResponse
{
    public required string ProjectId { get; init; }     // "proj-42"
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ProjectPath { get; init; }
    public required string Status { get; init; }
}
```

### API Client Extensions

**`Clients/PinkRoosterApiClient.cs`** — add methods:

```csharp
public async Task<ProjectResponse?> GetProjectByPathAsync(
    string projectPath, CancellationToken ct = default)
{
    var response = await httpClient.GetAsync(
        $"/api/projects?path={Uri.EscapeDataString(projectPath)}", ct);
    if (response.StatusCode == HttpStatusCode.NotFound) return null;
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);
}

public async Task<(ProjectResponse Project, bool IsNew)> CreateOrUpdateProjectAsync(
    CreateOrUpdateProjectRequest request, CancellationToken ct = default)
{
    var response = await httpClient.PutAsJsonAsync("/api/projects", request, ct);
    response.EnsureSuccessStatusCode();
    var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct)
        ?? throw new InvalidOperationException("Failed to deserialize project response.");
    return (project, response.StatusCode == HttpStatusCode.Created);
}
```

### MCP Tools

**`Tools/ProjectTools.cs`**

```csharp
[McpServerToolType]
public sealed class ProjectTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "get_project_overview", ReadOnly = true)]
    [Description(
        "Call first when starting work on a project. " +
        "Returns an overview of the project including the project id.")]
    public async Task<string> GetProjectOverview(
        [Description("Absolute path to the project root directory.")] string projectPath,
        CancellationToken ct = default)
    {
        var project = await apiClient.GetProjectByPathAsync(projectPath, ct);

        if (project is null)
            return OperationResult.Warning(
                $"No project found at '{projectPath}'. " +
                "Call create_or_update_project to register it.");

        var overview = new ProjectOverviewResponse
        {
            ProjectId = project.ProjectId,
            Name = project.Name,
            Description = project.Description,
            ProjectPath = project.ProjectPath,
            Status = project.Status
        };
        return JsonSerializer.Serialize(overview, JsonDefaults.Indented);
    }

    [McpServerTool(Name = "create_or_update_project")]
    [Description("Creates or updates a project, matched by path. Returns the project id.")]
    public async Task<string> CreateOrUpdateProject(
        [Description("Display name.")] string name,
        [Description("Short description.")] string description,
        [Description("Absolute path to the project root directory.")] string projectPath,
        CancellationToken ct = default)
    {
        var request = new CreateOrUpdateProjectRequest
        {
            Name = name,
            Description = description,
            ProjectPath = projectPath
        };

        var (project, isNew) = await apiClient.CreateOrUpdateProjectAsync(request, ct);

        return isNew
            ? OperationResult.Success($"Project '{name}' created with id {project.ProjectId}.")
            : OperationResult.Success($"Project '{name}' ({project.ProjectId}) updated.");
    }
}
```

---

## 6. Dashboard

### TypeScript Types

**`types/index.ts`** — add:
```typescript
export interface Project {
  projectId: string;   // "proj-42"
  id: number;          // 42 (for delete)
  name: string;
  description: string;
  projectPath: string;
  status: "Active" | "Archived";
  createdAt: string;
  updatedAt: string;
}
```

### API Functions

**`api/projects.ts`**
```typescript
import { apiFetch } from "./client";
import type { Project } from "@/types";

export function getProjects(): Promise<Project[]> {
  return apiFetch<Project[]>("/projects");
}

export function deleteProject(id: number): Promise<void> {
  return apiFetch(`/projects/${id}`, { method: "DELETE" });
}
```

### Hooks

**`hooks/use-projects.ts`**
```typescript
// useProjects()       → useQuery wrapping getProjects()
// useDeleteProject()  → useMutation wrapping deleteProject(), invalidates ["projects"]
```

**`hooks/use-project-context.ts`**
```typescript
// React context providing:
// {
//   selectedProject: Project | null;
//   setSelectedProject: (project: Project) => void;
//   clearSelectedProject: () => void;
// }
//
// Persists selected project ID to localStorage.
// On mount, resolves the stored ID against the project list.
// Exports: ProjectProvider (wraps App), useProjectContext (hook).
```

### Components

**`components/layout/project-switcher.tsx`**

Dropdown in the sidebar header. Shows:
- Selected project name + `proj-{id}` badge
- Truncated project path underneath
- Dropdown list of all projects to switch between
- "All Projects" link at bottom → navigates to `/projects`

When no project is selected, shows "Select a project" placeholder.

**Sidebar integration** — replaces the static `SidebarHeader`:
```
┌─────────────────────────────────────────────┐
│  ▾  MyProject                      proj-1   │
│     /home/user/myproject                    │
├─────────────────────────────────────────────┤
│  Navigation                                 │
│  ● Dashboard                                │
│  ● Activity Log                             │
│                                             │
│ ───────────────────────                     │
│  All Projects                               │
└─────────────────────────────────────────────┘
```

### Pages

**`pages/project-list-page.tsx`**

| Column | Content |
|--------|---------|
| Project ID | `proj-42` as badge |
| Name | Project name |
| Path | Truncated project path |
| Status | Badge (green=Active, gray=Archived) |
| Created | Relative time |
| Actions | Delete button (with confirmation dialog) |

**Empty state**: When no projects exist, show a centered card:
> "No projects yet. Projects are created by AI agents via MCP tools."

Clicking a row sets that project as the selected context and navigates to `/`.

### Routes

**`App.tsx`** — updated:
```tsx
<ProjectProvider>
  <BrowserRouter>
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<DashboardPage />} />
        <Route path="projects" element={<ProjectListPage />} />
        <Route path="activity" element={<ActivityLogPage />} />
      </Route>
    </Routes>
  </BrowserRouter>
</ProjectProvider>
```

### Dashboard Page Update

When no project is selected, `DashboardPage` shows a prompt:
> "Select a project to get started" with a button linking to `/projects`.

When a project is selected, shows the existing stats cards (unchanged for now).

---

## 7. File Manifest

### Create

| File | Layer |
|------|-------|
| `src/PinkRooster.Shared/Enums/ProjectStatus.cs` | Shared |
| `src/PinkRooster.Shared/DTOs/Requests/CreateOrUpdateProjectRequest.cs` | Shared |
| `src/PinkRooster.Shared/DTOs/Responses/ProjectResponse.cs` | Shared |
| `src/PinkRooster.Data/Entities/Project.cs` | Data |
| `src/PinkRooster.Data/Configurations/ProjectConfiguration.cs` | Data |
| `src/PinkRooster.Api/Services/IProjectService.cs` | Api |
| `src/PinkRooster.Api/Services/ProjectService.cs` | Api |
| `src/PinkRooster.Api/Controllers/ProjectController.cs` | Api |
| `src/PinkRooster.Mcp/Responses/ResponseType.cs` | Mcp |
| `src/PinkRooster.Mcp/Responses/JsonDefaults.cs` | Mcp |
| `src/PinkRooster.Mcp/Responses/OperationResult.cs` | Mcp |
| `src/PinkRooster.Mcp/Responses/ProjectOverviewResponse.cs` | Mcp |
| `src/PinkRooster.Mcp/Tools/ProjectTools.cs` | Mcp |
| `src/dashboard/src/api/projects.ts` | Dashboard |
| `src/dashboard/src/hooks/use-projects.ts` | Dashboard |
| `src/dashboard/src/hooks/use-project-context.tsx` | Dashboard |
| `src/dashboard/src/components/layout/project-switcher.tsx` | Dashboard |
| `src/dashboard/src/pages/project-list-page.tsx` | Dashboard |

### Modify

| File | Change |
|------|--------|
| `src/PinkRooster.Shared/Constants/ApiRoutes.cs` | Add `Projects` route |
| `src/PinkRooster.Data/AppDbContext.cs` | Add `DbSet<Project>`, `SaveChangesAsync` override |
| `src/PinkRooster.Api/Program.cs` | Register `IProjectService` |
| `src/PinkRooster.Mcp/Clients/PinkRoosterApiClient.cs` | Add project methods |
| `src/dashboard/src/types/index.ts` | Add `Project` interface |
| `src/dashboard/src/App.tsx` | Add `ProjectProvider`, project list route |
| `src/dashboard/src/components/layout/app-sidebar.tsx` | Add `ProjectSwitcher`, "All Projects" nav item |

---

## 8. Build Sequence

| Step | What | Depends on |
|------|------|------------|
| 1 | Shared: `ProjectStatus` enum | — |
| 2 | Shared: `CreateOrUpdateProjectRequest`, `ProjectResponse`, `ApiRoutes` update | Step 1 |
| 3 | Data: `Project` entity, `ProjectConfiguration` | Step 1 |
| 4 | Data: `AppDbContext` changes (DbSet + SaveChangesAsync override) | Step 3 |
| 5 | Data: EF migration | Step 4 |
| 6 | Api: `IProjectService`, `ProjectService` | Steps 2, 4 |
| 7 | Api: `ProjectController`, DI registration | Step 6 |
| 8 | Mcp: `Responses/` directory (ResponseType, JsonDefaults, OperationResult, ProjectOverviewResponse) | — |
| 9 | Mcp: `PinkRoosterApiClient` extensions | Step 2 |
| 10 | Mcp: `ProjectTools` | Steps 8, 9 |
| 11 | Dashboard: types, api functions, hooks | — |
| 12 | Dashboard: project context provider | Step 11 |
| 13 | Dashboard: project switcher, project list page | Step 12 |
| 14 | Dashboard: sidebar + App.tsx + DashboardPage updates | Step 13 |
| 15 | Build + verify | All |

---

## 9. Acceptance Criteria

1. `create_or_update_project` with a new path → creates project, returns `OperationResult.Success` with `proj-{Id}` in message
2. `create_or_update_project` with existing path → updates name/description, returns `OperationResult.Success` with `proj-{Id}`
3. `get_project_overview` with existing path → returns `ProjectOverviewResponse` (no timestamps)
4. `get_project_overview` with unknown path → returns `OperationResult.Warning` prompting creation
5. `GET /api/projects` → returns `ProjectResponse[]` with all fields including timestamps
6. `DELETE /api/projects/{id}` → removes project, returns 204
7. Dashboard project list shows all projects with status badges
8. Dashboard project list shows empty state when no projects exist
9. Dashboard delete requires confirmation dialog
10. Project switcher in sidebar allows switching between projects
11. Selected project persists across page refreshes (localStorage)
12. Dashboard page shows "select a project" prompt when none selected
13. DB has unique constraint on `project_path`
14. No GUIDs anywhere in the slice
15. MCP response types (`OperationResult`, `ResponseType`, `ProjectOverviewResponse`) exist only in MCP project
