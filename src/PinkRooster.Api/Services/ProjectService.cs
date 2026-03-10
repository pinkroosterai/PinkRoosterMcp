using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

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
