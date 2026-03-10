using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class ProjectService(AppDbContext db) : IProjectService
{
    public async Task<List<ProjectResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Projects
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

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ProjectStatusResponse?> GetStatusAsync(long projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return null;

        var issueStates = await db.Issues
            .Where(i => i.ProjectId == projectId)
            .Select(i => new { i.Id, i.State, IssueNumber = i.IssueNumber, i.Name, i.ProjectId })
            .ToListAsync(ct);

        var issueActive = issueStates.Where(i => CompletionStateConstants.ActiveStates.Contains(i.State)).ToList();
        var issueInactive = issueStates.Where(i => CompletionStateConstants.InactiveStates.Contains(i.State)).ToList();
        var issueTerminal = issueStates.Count(i => CompletionStateConstants.TerminalStates.Contains(i.State));

        var frStates = await db.FeatureRequests
            .Where(fr => fr.ProjectId == projectId)
            .Select(fr => new { fr.Id, fr.Status, fr.FeatureRequestNumber, fr.Name, fr.ProjectId })
            .ToListAsync(ct);

        var frActive = frStates.Where(fr => FeatureStatusConstants.ActiveStates.Contains(fr.Status)).ToList();
        var frInactive = frStates.Where(fr => FeatureStatusConstants.InactiveStates.Contains(fr.Status)).ToList();
        var frTerminal = frStates.Count(fr => FeatureStatusConstants.TerminalStates.Contains(fr.Status));

        var wpData = await db.WorkPackages
            .Where(w => w.ProjectId == projectId)
            .Select(w => new { w.Id, w.WorkPackageNumber, w.Name, w.State, w.ProjectId })
            .ToListAsync(ct);

        var wpActive = wpData.Where(w => CompletionStateConstants.ActiveStates.Contains(w.State) && w.State != CompletionState.Blocked).ToList();
        var wpInactive = wpData.Where(w => w.State == CompletionState.NotStarted).ToList();
        var wpBlocked = wpData.Where(w => w.State == CompletionState.Blocked).ToList();
        var wpTerminal = wpData.Count(w => CompletionStateConstants.TerminalStates.Contains(w.State));

        return new ProjectStatusResponse
        {
            ProjectId = $"proj-{project.Id}",
            Name = project.Name,
            Status = project.Status.ToString(),
            Issues = new EntityStatusSummary
            {
                Total = issueStates.Count,
                Active = issueActive.Count,
                Inactive = issueInactive.Count,
                Terminal = issueTerminal,
                PercentComplete = issueStates.Count > 0 ? issueTerminal * 100 / issueStates.Count : 0,
                ActiveItems = issueActive.Select(i => new StatusItem
                {
                    Id = $"proj-{i.ProjectId}-issue-{i.IssueNumber}",
                    Name = i.Name
                }).ToList(),
                InactiveItems = issueInactive.Select(i => new StatusItem
                {
                    Id = $"proj-{i.ProjectId}-issue-{i.IssueNumber}",
                    Name = i.Name
                }).ToList()
            },
            FeatureRequests = new EntityStatusSummary
            {
                Total = frStates.Count,
                Active = frActive.Count,
                Inactive = frInactive.Count,
                Terminal = frTerminal,
                PercentComplete = frStates.Count > 0 ? frTerminal * 100 / frStates.Count : 0,
                ActiveItems = frActive.Select(fr => new StatusItem
                {
                    Id = $"proj-{fr.ProjectId}-fr-{fr.FeatureRequestNumber}",
                    Name = fr.Name
                }).ToList(),
                InactiveItems = frInactive.Select(fr => new StatusItem
                {
                    Id = $"proj-{fr.ProjectId}-fr-{fr.FeatureRequestNumber}",
                    Name = fr.Name
                }).ToList()
            },
            WorkPackages = new WorkPackageStatusSummary
            {
                Total = wpData.Count,
                TerminalCount = wpTerminal,
                PercentComplete = wpData.Count > 0 ? wpTerminal * 100 / wpData.Count : 0,
                Active = wpActive.Select(w => new StatusItem
                {
                    Id = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}",
                    Name = w.Name
                }).ToList(),
                Inactive = wpInactive.Select(w => new StatusItem
                {
                    Id = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}",
                    Name = w.Name
                }).ToList(),
                Blocked = wpBlocked.Select(w => new StatusItem
                {
                    Id = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}",
                    Name = w.Name
                }).ToList()
            }
        };
    }

    public async Task<List<NextActionItem>?> GetNextActionsAsync(
        long projectId, int limit = 10, string? entityType = null, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return null;

        var items = new List<NextActionItem>();

        // Query 1: Actionable tasks — active states + NotStarted (if WP is active), never Blocked
        if (entityType is null or "task")
        {
            var tasks = await db.WorkPackageTasks
                .Include(t => t.WorkPackage)
                .Where(t => t.WorkPackage.ProjectId == projectId)
                .Where(t => !CompletionStateConstants.TerminalStates.Contains(t.State))
                .Where(t => t.State != CompletionState.Blocked)
                .Where(t => CompletionStateConstants.ActiveStates.Contains(t.State)
                    || (t.State == CompletionState.NotStarted
                        && CompletionStateConstants.ActiveStates.Contains(t.WorkPackage.State)))
                .Select(t => new
                {
                    t.WorkPackage.ProjectId,
                    t.WorkPackage.WorkPackageNumber,
                    t.TaskNumber,
                    t.Name,
                    t.State,
                    t.SortOrder,
                    t.WorkPackage.Priority
                })
                .ToListAsync(ct);

            items.AddRange(tasks.Select(t => new NextActionItem
            {
                Type = "Task",
                Id = $"proj-{t.ProjectId}-wp-{t.WorkPackageNumber}-task-{t.TaskNumber}",
                Name = t.Name,
                Priority = t.Priority.ToString(),
                State = t.State.ToString(),
                ParentId = $"proj-{t.ProjectId}-wp-{t.WorkPackageNumber}"
            }));
        }

        // Query 2: Actionable WPs — active/NotStarted, not Blocked, zero phases (leaf WPs only)
        if (entityType is null or "wp")
        {
            var wps = await db.WorkPackages
                .Where(w => w.ProjectId == projectId)
                .Where(w => !CompletionStateConstants.TerminalStates.Contains(w.State))
                .Where(w => w.State != CompletionState.Blocked)
                .Where(w => !db.WorkPackagePhases.Any(p => p.WorkPackage.Id == w.Id))
                .Select(w => new
                {
                    w.ProjectId,
                    w.WorkPackageNumber,
                    w.Name,
                    w.State,
                    w.Priority
                })
                .ToListAsync(ct);

            items.AddRange(wps.Select(w => new NextActionItem
            {
                Type = "WorkPackage",
                Id = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}",
                Name = w.Name,
                Priority = w.Priority.ToString(),
                State = w.State.ToString(),
                ParentId = $"proj-{w.ProjectId}"
            }));
        }

        // Query 3: Actionable issues — active/NotStarted, not Blocked, no linked WPs
        if (entityType is null or "issue")
        {
            var linkedIssueIds = db.WorkPackages
                .Where(w => w.ProjectId == projectId && w.LinkedIssueId != null)
                .Select(w => w.LinkedIssueId!.Value);

            var issues = await db.Issues
                .Where(i => i.ProjectId == projectId)
                .Where(i => !CompletionStateConstants.TerminalStates.Contains(i.State))
                .Where(i => i.State != CompletionState.Blocked)
                .Where(i => !linkedIssueIds.Contains(i.Id))
                .Select(i => new
                {
                    i.ProjectId,
                    i.IssueNumber,
                    i.Name,
                    i.State,
                    i.Priority
                })
                .ToListAsync(ct);

            items.AddRange(issues.Select(i => new NextActionItem
            {
                Type = "Issue",
                Id = $"proj-{i.ProjectId}-issue-{i.IssueNumber}",
                Name = i.Name,
                Priority = i.Priority.ToString(),
                State = i.State.ToString(),
                ParentId = $"proj-{i.ProjectId}"
            }));
        }

        // Query 4: Actionable feature requests — active (UnderReview, Approved), not terminal/deferred
        if (entityType is null or "featurerequest")
        {
            var frs = await db.FeatureRequests
                .Where(fr => fr.ProjectId == projectId)
                .Where(fr => FeatureStatusConstants.ActiveStates.Contains(fr.Status)
                    && fr.Status != FeatureStatus.InProgress) // InProgress = WPs handle it
                .Select(fr => new
                {
                    fr.ProjectId,
                    fr.FeatureRequestNumber,
                    fr.Name,
                    fr.Status,
                    fr.Priority
                })
                .ToListAsync(ct);

            items.AddRange(frs.Select(fr => new NextActionItem
            {
                Type = "FeatureRequest",
                Id = $"proj-{fr.ProjectId}-fr-{fr.FeatureRequestNumber}",
                Name = fr.Name,
                Priority = fr.Priority.ToString(),
                State = fr.Status.ToString(),
                ParentId = $"proj-{fr.ProjectId}"
            }));
        }

        // Sort: Priority ordinal → entity type (Task=0, WP=1, Issue=2, FR=3) → name
        var typeOrder = new Dictionary<string, int> { ["Task"] = 0, ["WorkPackage"] = 1, ["Issue"] = 2, ["FeatureRequest"] = 3 };

        return items
            .OrderBy(i => Enum.TryParse<Priority>(i.Priority, out var p) ? (int)p : 99)
            .ThenBy(i => typeOrder.GetValueOrDefault(i.Type, 99))
            .ThenBy(i => i.Name)
            .Take(limit)
            .ToList();
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
