using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class ProjectService(AppDbContext db) : IProjectService
{
    public async Task<List<ProjectResponse>> GetAllAsync(long? userId = null, CancellationToken ct = default)
    {
        var query = db.Projects.AsQueryable();

        // Filter by user access if userId is provided
        if (userId is not null)
        {
            // Check if user is SuperUser (global access)
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is not null && user.GlobalRole != GlobalRole.SuperUser)
            {
                // Non-SuperUser: only show projects where user has a role
                var accessibleProjectIds = db.UserProjectRoles
                    .Where(r => r.UserId == userId)
                    .Select(r => r.ProjectId);

                query = query.Where(p => accessibleProjectIds.Contains(p.Id));
            }
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => ToResponse(p))
            .ToListAsync(ct);
    }

    public async Task<ProjectResponse?> GetByPathAsync(string projectPath, CancellationToken ct = default)
    {
        var project = await db.Projects
            .FirstOrDefaultAsync(p => p.ProjectPath == projectPath, ct);

        return project is null ? null : ToResponse(project);
    }

    public async Task<(ProjectResponse Project, bool IsNew)> CreateOrUpdateAsync(
        CreateOrUpdateProjectRequest request, CancellationToken ct = default)
    {
        var existing = await db.Projects
            .FirstOrDefaultAsync(p => p.ProjectPath == request.ProjectPath, ct);

        if (existing is not null)
        {
            existing.Name = request.Name;
            existing.Description = request.Description;
            await db.SaveChangesAsync(ct);
            return (ToResponse(existing), false);
        }

        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            ProjectPath = request.ProjectPath
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return (ToResponse(project), true);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var project = await db.Projects.FindAsync([id], ct);
        if (project is null) return false;

        // Delete children in dependency order (leaves first)
        var wpIds = await db.WorkPackages.Where(w => w.ProjectId == id).Select(w => w.Id).ToListAsync(ct);
        if (wpIds.Count > 0)
        {
            var phaseIds = await db.WorkPackagePhases.Where(p => wpIds.Contains(p.WorkPackageId)).Select(p => p.Id).ToListAsync(ct);

            await db.TaskAuditLogs.Where(a => wpIds.Contains(a.Task.WorkPackageId)).ExecuteDeleteAsync(ct);
            await db.WorkPackageTaskDependencies.Where(d => wpIds.Contains(d.DependentTask.WorkPackageId)).ExecuteDeleteAsync(ct);
            await db.WorkPackageTasks.Where(t => wpIds.Contains(t.WorkPackageId)).ExecuteDeleteAsync(ct);

            await db.PhaseAuditLogs.Where(a => phaseIds.Contains(a.PhaseId)).ExecuteDeleteAsync(ct);
            await db.AcceptanceCriteria.Where(ac => phaseIds.Contains(ac.PhaseId)).ExecuteDeleteAsync(ct);
            await db.WorkPackagePhases.Where(p => wpIds.Contains(p.WorkPackageId)).ExecuteDeleteAsync(ct);

            await db.WorkPackageAuditLogs.Where(a => wpIds.Contains(a.WorkPackageId)).ExecuteDeleteAsync(ct);
            await db.WorkPackageDependencies.Where(d => wpIds.Contains(d.DependentWorkPackageId) || wpIds.Contains(d.DependsOnWorkPackageId)).ExecuteDeleteAsync(ct);
            await db.WorkPackageIssueLinks.Where(l => wpIds.Contains(l.WorkPackageId)).ExecuteDeleteAsync(ct);
            await db.WorkPackageFeatureRequestLinks.Where(l => wpIds.Contains(l.WorkPackageId)).ExecuteDeleteAsync(ct);
            await db.WorkPackages.Where(w => w.ProjectId == id).ExecuteDeleteAsync(ct);
        }

        await db.IssueAuditLogs.Where(a => a.Issue.ProjectId == id).ExecuteDeleteAsync(ct);
        await db.Issues.Where(i => i.ProjectId == id).ExecuteDeleteAsync(ct);

        await db.FeatureRequestAuditLogs.Where(a => a.FeatureRequest.ProjectId == id).ExecuteDeleteAsync(ct);
        await db.FeatureRequests.Where(fr => fr.ProjectId == id).ExecuteDeleteAsync(ct);

        await db.ProjectMemoryAuditLogs.Where(a => a.ProjectMemory.ProjectId == id).ExecuteDeleteAsync(ct);
        await db.ProjectMemories.Where(m => m.ProjectId == id).ExecuteDeleteAsync(ct);

        await db.UserProjectRoles.Where(r => r.ProjectId == id).ExecuteDeleteAsync(ct);

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ProjectStatusResponse?> GetStatusAsync(long projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return null;

        return new ProjectStatusResponse
        {
            ProjectId = $"proj-{project.Id}",
            Name = project.Name,
            Status = project.Status.ToString(),
            Issues = await GetIssueStatusAsync(projectId, ct),
            FeatureRequests = await GetFeatureRequestStatusAsync(projectId, ct),
            WorkPackages = await GetWorkPackageStatusAsync(projectId, ct),
            Memories = await GetMemoryStatusAsync(projectId, ct)
        };
    }

    public async Task<List<NextActionItem>?> GetNextActionsAsync(
        long projectId, int limit = 10, string? entityType = null, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return null;

        var items = new List<NextActionItem>();

        if (entityType is null or "task")
            items.AddRange(await GetActionableTasksAsync(projectId, limit, ct));

        if (entityType is null or "wp")
            items.AddRange(await GetActionableWorkPackagesAsync(projectId, limit, ct));

        if (entityType is null or "issue")
            items.AddRange(await GetActionableIssuesAsync(projectId, limit, ct));

        if (entityType is null or "featurerequest")
            items.AddRange(await GetActionableFeatureRequestsAsync(projectId, limit, ct));

        var typeOrder = new Dictionary<string, int> { ["Task"] = 0, ["WorkPackage"] = 1, ["Issue"] = 2, ["FeatureRequest"] = 3 };

        return items
            .OrderBy(i => Enum.TryParse<Priority>(i.Priority, out var p) ? (int)p : 99)
            .ThenBy(i => typeOrder.GetValueOrDefault(i.Type, 99))
            .ThenBy(i => i.Name)
            .Take(limit)
            .ToList();
    }

    // ── GetStatusAsync helpers ──

    private async Task<EntityStatusSummary> GetIssueStatusAsync(long projectId, CancellationToken ct)
    {
        var issueStates = await db.Issues
            .Where(i => i.ProjectId == projectId)
            .Select(i => new { i.State, i.IssueNumber, i.Name, i.ProjectId })
            .ToListAsync(ct);

        var active = issueStates.Where(i => CompletionStateConstants.ActiveStates.Contains(i.State)).ToList();
        var inactive = issueStates.Where(i => CompletionStateConstants.InactiveStates.Contains(i.State)).ToList();
        var terminal = issueStates.Count(i => CompletionStateConstants.TerminalStates.Contains(i.State));

        return new EntityStatusSummary
        {
            Total = issueStates.Count,
            Active = active.Count,
            Inactive = inactive.Count,
            Terminal = terminal,
            PercentComplete = issueStates.Count > 0 ? terminal * 100 / issueStates.Count : 0,
            ActiveItems = active.Select(i => new StatusItem { Id = $"proj-{i.ProjectId}-issue-{i.IssueNumber}", Name = i.Name }).ToList(),
            InactiveItems = inactive.Select(i => new StatusItem { Id = $"proj-{i.ProjectId}-issue-{i.IssueNumber}", Name = i.Name }).ToList()
        };
    }

    private async Task<EntityStatusSummary> GetFeatureRequestStatusAsync(long projectId, CancellationToken ct)
    {
        var frStates = await db.FeatureRequests
            .Where(fr => fr.ProjectId == projectId)
            .Select(fr => new { fr.Status, fr.FeatureRequestNumber, fr.Name, fr.ProjectId })
            .ToListAsync(ct);

        var active = frStates.Where(fr => FeatureStatusConstants.ActiveStates.Contains(fr.Status)).ToList();
        var inactive = frStates.Where(fr => FeatureStatusConstants.InactiveStates.Contains(fr.Status)).ToList();
        var terminal = frStates.Count(fr => FeatureStatusConstants.TerminalStates.Contains(fr.Status));

        return new EntityStatusSummary
        {
            Total = frStates.Count,
            Active = active.Count,
            Inactive = inactive.Count,
            Terminal = terminal,
            PercentComplete = frStates.Count > 0 ? terminal * 100 / frStates.Count : 0,
            ActiveItems = active.Select(fr => new StatusItem { Id = $"proj-{fr.ProjectId}-fr-{fr.FeatureRequestNumber}", Name = fr.Name }).ToList(),
            InactiveItems = inactive.Select(fr => new StatusItem { Id = $"proj-{fr.ProjectId}-fr-{fr.FeatureRequestNumber}", Name = fr.Name }).ToList()
        };
    }

    private async Task<WorkPackageStatusSummary> GetWorkPackageStatusAsync(long projectId, CancellationToken ct)
    {
        var wpData = await db.WorkPackages
            .Where(w => w.ProjectId == projectId)
            .Select(w => new { w.WorkPackageNumber, w.Name, w.State, w.ProjectId })
            .ToListAsync(ct);

        var active = wpData.Where(w => CompletionStateConstants.ActiveStates.Contains(w.State) && w.State != CompletionState.Blocked).ToList();
        var inactive = wpData.Where(w => w.State == CompletionState.NotStarted).ToList();
        var blocked = wpData.Where(w => w.State == CompletionState.Blocked).ToList();
        var terminal = wpData.Count(w => CompletionStateConstants.TerminalStates.Contains(w.State));

        return new WorkPackageStatusSummary
        {
            Total = wpData.Count,
            TerminalCount = terminal,
            PercentComplete = wpData.Count > 0 ? terminal * 100 / wpData.Count : 0,
            Active = active.Select(w => new StatusItem { Id = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}", Name = w.Name }).ToList(),
            Inactive = inactive.Select(w => new StatusItem { Id = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}", Name = w.Name }).ToList(),
            Blocked = blocked.Select(w => new StatusItem { Id = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}", Name = w.Name }).ToList()
        };
    }

    private async Task<MemoryStatusSummary?> GetMemoryStatusAsync(long projectId, CancellationToken ct)
    {
        var memories = await db.ProjectMemories
            .Where(m => m.ProjectId == projectId)
            .OrderByDescending(m => m.UpdatedAt)
            .Select(m => new { m.MemoryNumber, m.Name, m.Tags, m.ProjectId })
            .ToListAsync(ct);

        if (memories.Count == 0) return null;

        return new MemoryStatusSummary
        {
            Total = memories.Count,
            RecentMemories = memories.Take(5).Select(m => new MemoryStatusItem
            {
                MemoryId = $"proj-{m.ProjectId}-mem-{m.MemoryNumber}",
                Name = m.Name,
                Tags = m.Tags
            }).ToList(),
            TagCloud = memories
                .SelectMany(m => m.Tags)
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    // ── GetNextActionsAsync helpers ──

    private async Task<List<NextActionItem>> GetActionableTasksAsync(long projectId, int limit, CancellationToken ct)
    {
        var tasks = await db.WorkPackageTasks
            .Include(t => t.WorkPackage).ThenInclude(w => w.LinkedIssueLinks).ThenInclude(l => l.Issue)
            .Include(t => t.WorkPackage).ThenInclude(w => w.LinkedFeatureRequestLinks).ThenInclude(l => l.FeatureRequest)
            .Where(t => t.WorkPackage.ProjectId == projectId)
            .Where(t => !CompletionStateConstants.TerminalStates.Contains(t.WorkPackage.State)
                && t.WorkPackage.State != CompletionState.Blocked)
            .Where(t => !CompletionStateConstants.TerminalStates.Contains(t.State))
            .Where(t => t.State != CompletionState.Blocked)
            .Where(t => CompletionStateConstants.ActiveStates.Contains(t.State)
                || t.State == CompletionState.NotStarted)
            .OrderBy(t => t.WorkPackage.Priority)
            .ThenBy(t => t.Name)
            .Take(limit)
            .ToListAsync(ct);

        return tasks.Select(t => new NextActionItem
        {
            Type = "Task",
            Id = $"proj-{t.WorkPackage.ProjectId}-wp-{t.WorkPackage.WorkPackageNumber}-task-{t.TaskNumber}",
            Name = t.Name,
            Priority = t.WorkPackage.Priority.ToString(),
            State = t.State.ToString(),
            ParentId = $"proj-{t.WorkPackage.ProjectId}-wp-{t.WorkPackage.WorkPackageNumber}",
            WorkPackageType = t.WorkPackage.Type.ToString(),
            EstimatedComplexity = t.WorkPackage.EstimatedComplexity,
            LinkedIssueNames = t.WorkPackage.LinkedIssueLinks.Count > 0
                ? t.WorkPackage.LinkedIssueLinks.Select(l => l.Issue.Name).ToList() : null,
            LinkedFrNames = t.WorkPackage.LinkedFeatureRequestLinks.Count > 0
                ? t.WorkPackage.LinkedFeatureRequestLinks.Select(l => l.FeatureRequest.Name).ToList() : null
        }).ToList();
    }

    private async Task<List<NextActionItem>> GetActionableWorkPackagesAsync(long projectId, int limit, CancellationToken ct)
    {
        var wps = await db.WorkPackages
            .Include(w => w.LinkedIssueLinks).ThenInclude(l => l.Issue)
            .Include(w => w.LinkedFeatureRequestLinks).ThenInclude(l => l.FeatureRequest)
            .Where(w => w.ProjectId == projectId)
            .Where(w => !CompletionStateConstants.TerminalStates.Contains(w.State))
            .Where(w => w.State != CompletionState.Blocked)
            .Where(w => !db.WorkPackagePhases.Any(p => p.WorkPackage.Id == w.Id))
            .OrderBy(w => w.Priority)
            .ThenBy(w => w.Name)
            .Take(limit)
            .ToListAsync(ct);

        return wps.Select(w => new NextActionItem
        {
            Type = "WorkPackage",
            Id = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}",
            Name = w.Name,
            Priority = w.Priority.ToString(),
            State = w.State.ToString(),
            ParentId = $"proj-{w.ProjectId}",
            WorkPackageType = w.Type.ToString(),
            EstimatedComplexity = w.EstimatedComplexity,
            LinkedIssueNames = w.LinkedIssueLinks.Count > 0
                ? w.LinkedIssueLinks.Select(l => l.Issue.Name).ToList() : null,
            LinkedFrNames = w.LinkedFeatureRequestLinks.Count > 0
                ? w.LinkedFeatureRequestLinks.Select(l => l.FeatureRequest.Name).ToList() : null
        }).ToList();
    }

    private async Task<List<NextActionItem>> GetActionableIssuesAsync(long projectId, int limit, CancellationToken ct)
    {
        var linkedIssueIds = db.WorkPackageIssueLinks
            .Where(l => l.WorkPackage.ProjectId == projectId
                && !CompletionStateConstants.TerminalStates.Contains(l.WorkPackage.State))
            .Select(l => l.IssueId);

        var issues = await db.Issues
            .Where(i => i.ProjectId == projectId)
            .Where(i => !CompletionStateConstants.TerminalStates.Contains(i.State))
            .Where(i => i.State != CompletionState.Blocked)
            .Where(i => !linkedIssueIds.Contains(i.Id))
            .Select(i => new { i.ProjectId, i.IssueNumber, i.Name, i.State, i.Priority, i.IssueType, i.Severity })
            .OrderBy(i => i.Priority)
            .ThenBy(i => i.Name)
            .Take(limit)
            .ToListAsync(ct);

        return issues.Select(i => new NextActionItem
        {
            Type = "Issue",
            Id = $"proj-{i.ProjectId}-issue-{i.IssueNumber}",
            Name = i.Name,
            Priority = i.Priority.ToString(),
            State = i.State.ToString(),
            ParentId = $"proj-{i.ProjectId}",
            IssueType = i.IssueType.ToString(),
            Severity = i.Severity.ToString()
        }).ToList();
    }

    private async Task<List<NextActionItem>> GetActionableFeatureRequestsAsync(long projectId, int limit, CancellationToken ct)
    {
        var frs = await db.FeatureRequests
            .Where(fr => fr.ProjectId == projectId)
            .Where(fr => (FeatureStatusConstants.ActiveStates.Contains(fr.Status)
                && fr.Status != FeatureStatus.InProgress)
                || fr.Status == FeatureStatus.Proposed)
            .Select(fr => new { fr.ProjectId, fr.FeatureRequestNumber, fr.Name, fr.Status, fr.Priority, fr.Category })
            .OrderBy(fr => fr.Priority)
            .ThenBy(fr => fr.Name)
            .Take(limit)
            .ToListAsync(ct);

        return frs.Select(fr => new NextActionItem
        {
            Type = "FeatureRequest",
            Id = $"proj-{fr.ProjectId}-fr-{fr.FeatureRequestNumber}",
            Name = fr.Name,
            Priority = fr.Priority.ToString(),
            State = fr.Status.ToString(),
            ParentId = $"proj-{fr.ProjectId}",
            Category = fr.Category.ToString()
        }).ToList();
    }

    private static ProjectResponse ToResponse(Project p) => new()
    {
        ProjectId = $"proj-{p.Id}",
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        ProjectPath = p.ProjectPath,
        Status = p.Status.ToString(),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
