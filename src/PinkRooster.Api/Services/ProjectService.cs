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
