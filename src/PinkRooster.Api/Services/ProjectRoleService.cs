using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class ProjectRoleService(AppDbContext db) : IProjectRoleService
{
    // Request-scoped cache to avoid repeated DB lookups
    private readonly Dictionary<(long, long), string?> _roleCache = new();

    public async Task<string?> GetEffectiveRoleAsync(long userId, long projectId, CancellationToken ct = default)
    {
        var cacheKey = (userId, projectId);
        if (_roleCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Check global role first
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
        {
            _roleCache[cacheKey] = null;
            return null;
        }

        if (user.GlobalRole == GlobalRole.SuperUser)
        {
            _roleCache[cacheKey] = "SuperUser";
            return "SuperUser";
        }

        // Check per-project role
        var projectRole = await db.UserProjectRoles
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ProjectId == projectId, ct);

        var role = projectRole?.Role.ToString();
        _roleCache[cacheKey] = role;
        return role;
    }

    public async Task<UserPermissionsResponse> GetUserPermissionsAsync(
        long userId, long projectId, CancellationToken ct = default)
    {
        var effectiveRole = await GetEffectiveRoleAsync(userId, projectId, ct);

        return new UserPermissionsResponse
        {
            EffectiveRole = effectiveRole ?? "None",
            CanRead = effectiveRole is not null,
            CanCreate = effectiveRole is "SuperUser" or "Admin" or "Editor",
            CanEdit = effectiveRole is "SuperUser" or "Admin" or "Editor",
            CanDelete = effectiveRole is "SuperUser" or "Admin",
            CanManageRoles = effectiveRole is "SuperUser" or "Admin"
        };
    }

    public async Task<UserProjectRoleResponse> AssignRoleAsync(
        long userId, long projectId, ProjectRole role, CancellationToken ct = default)
    {
        var existing = await db.UserProjectRoles
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ProjectId == projectId, ct);

        if (existing is not null)
        {
            existing.Role = role;
        }
        else
        {
            existing = new UserProjectRole
            {
                UserId = userId,
                ProjectId = projectId,
                Role = role
            };
            db.UserProjectRoles.Add(existing);
        }

        await db.SaveChangesAsync(ct);

        // Invalidate cache
        _roleCache.Remove((userId, projectId));

        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        return new UserProjectRoleResponse
        {
            UserId = userId,
            UserEmail = user.Email,
            UserDisplayName = user.DisplayName,
            ProjectId = projectId,
            Role = role.ToString(),
            CreatedAt = existing.CreatedAt
        };
    }

    public async Task<bool> RemoveRoleAsync(long userId, long projectId, CancellationToken ct = default)
    {
        var existing = await db.UserProjectRoles
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ProjectId == projectId, ct);

        if (existing is null)
            return false;

        db.UserProjectRoles.Remove(existing);
        await db.SaveChangesAsync(ct);

        _roleCache.Remove((userId, projectId));
        return true;
    }

    public async Task<List<UserProjectRoleResponse>> GetProjectRolesAsync(
        long projectId, CancellationToken ct = default)
    {
        return await db.UserProjectRoles
            .Include(r => r.User)
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Role)
            .ThenBy(r => r.User.Email)
            .Select(r => new UserProjectRoleResponse
            {
                UserId = r.UserId,
                UserEmail = r.User.Email,
                UserDisplayName = r.User.DisplayName,
                ProjectId = r.ProjectId,
                Role = r.Role.ToString(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }
}
