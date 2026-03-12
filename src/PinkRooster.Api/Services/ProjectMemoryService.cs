using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public sealed class ProjectMemoryService(AppDbContext db, IEventBroadcaster broadcaster) : IProjectMemoryService
{
    public async Task<List<ProjectMemoryListItemResponse>> GetByProjectAsync(
        long projectId, string? namePattern, string? tag, CancellationToken ct = default)
    {
        var query = db.ProjectMemories
            .Where(m => m.ProjectId == projectId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(namePattern))
            query = query.Where(m => EF.Functions.ILike(m.Name, $"%{namePattern}%"));

        var memories = await query
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(tag))
            memories = memories.Where(m => m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();

        return memories.Select(m => new ProjectMemoryListItemResponse
        {
            MemoryId = $"proj-{m.ProjectId}-mem-{m.MemoryNumber}",
            Name = m.Name,
            Tags = m.Tags,
            UpdatedAt = m.UpdatedAt
        }).ToList();
    }

    public async Task<ProjectMemoryResponse?> GetByNumberAsync(
        long projectId, int memoryNumber, CancellationToken ct = default)
    {
        var memory = await db.ProjectMemories
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.MemoryNumber == memoryNumber, ct);

        return memory is null ? null : ToResponse(memory);
    }

    public async Task<ProjectMemoryResponse> UpsertAsync(
        long projectId, UpsertProjectMemoryRequest request, string changedBy, CancellationToken ct = default)
    {
        var existing = await db.ProjectMemories
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.Name == request.Name, ct);

        if (existing is not null)
            return await MergeAsync(existing, request, changedBy, ct);

        return await CreateAsync(projectId, request, changedBy, ct);
    }

    public async Task<bool> DeleteAsync(long projectId, int memoryNumber, CancellationToken ct = default)
    {
        var memory = await db.ProjectMemories
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.MemoryNumber == memoryNumber, ct);

        if (memory is null) return false;

        db.ProjectMemories.Remove(memory);
        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "ProjectMemory",
            EntityId = $"proj-{projectId}-mem-{memory.MemoryNumber}",
            Action = "deleted",
            ProjectId = projectId
        });

        return true;
    }

    // ── Private helpers ──

    private async Task<ProjectMemoryResponse> CreateAsync(
        long projectId, UpsertProjectMemoryRequest request, string changedBy, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellation);

            var project = await db.Projects.FirstAsync(p => p.Id == projectId, cancellation);
            var nextNumber = project.NextMemoryNumber;
            project.NextMemoryNumber++;

            var memory = new ProjectMemory
            {
                MemoryNumber = nextNumber,
                ProjectId = projectId,
                Name = request.Name,
                Content = request.Content,
                Tags = request.Tags?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? []
            };

            db.ProjectMemories.Add(memory);

            var auditEntries = new List<ProjectMemoryAuditLog>();
            var createAudit = () => new ProjectMemoryAuditLog
            {
                ProjectMemory = memory, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow
            };
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Name", memory.Name);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Content", memory.Content);
            if (memory.Tags.Count > 0)
                AuditHelper.AddCreateEntry(auditEntries, createAudit, "Tags", JsonSerializer.Serialize(memory.Tags));
            db.ProjectMemoryAuditLogs.AddRange(auditEntries);

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            broadcaster.Publish(new ServerEvent
            {
                EventType = "entity:changed",
                EntityType = "ProjectMemory",
                EntityId = $"proj-{projectId}-mem-{memory.MemoryNumber}",
                Action = "created",
                ProjectId = projectId
            });

            return ToResponse(memory, wasMerged: false);
        }, ct);
    }

    private async Task<ProjectMemoryResponse> MergeAsync(
        ProjectMemory existing, UpsertProjectMemoryRequest request, string changedBy, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var auditEntries = new List<ProjectMemoryAuditLog>();
        var audit = () => new ProjectMemoryAuditLog
        {
            ProjectMemoryId = existing.Id, FieldName = default!, ChangedBy = changedBy, ChangedAt = now
        };

        // Merge content: append with separator
        var mergedContent = existing.Content + "\n\n---\n\n" + request.Content;
        AuditHelper.AuditAndSet(auditEntries, audit, "Content", existing.Content, mergedContent, v => existing.Content = v);

        // Merge tags: union (case-insensitive deduplicated)
        if (request.Tags is { Count: > 0 })
        {
            var oldTagsJson = JsonSerializer.Serialize(existing.Tags);
            var mergedTags = existing.Tags
                .Union(request.Tags, StringComparer.OrdinalIgnoreCase)
                .ToList();
            existing.Tags = mergedTags;
            var newTagsJson = JsonSerializer.Serialize(existing.Tags);
            if (oldTagsJson != newTagsJson)
            {
                auditEntries.Add(new ProjectMemoryAuditLog
                {
                    ProjectMemoryId = existing.Id,
                    FieldName = "Tags",
                    OldValue = oldTagsJson,
                    NewValue = newTagsJson,
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
            }
        }

        if (auditEntries.Count > 0)
            db.ProjectMemoryAuditLogs.AddRange(auditEntries);

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "ProjectMemory",
            EntityId = $"proj-{existing.ProjectId}-mem-{existing.MemoryNumber}",
            Action = "updated",
            ProjectId = existing.ProjectId
        });

        return ToResponse(existing, wasMerged: true);
    }

    private static ProjectMemoryResponse ToResponse(ProjectMemory memory, bool wasMerged = false) => new()
    {
        MemoryId = $"proj-{memory.ProjectId}-mem-{memory.MemoryNumber}",
        ProjectId = $"proj-{memory.ProjectId}",
        MemoryNumber = memory.MemoryNumber,
        Name = memory.Name,
        Content = memory.Content,
        Tags = memory.Tags,
        CreatedAt = memory.CreatedAt,
        UpdatedAt = memory.UpdatedAt,
        WasMerged = wasMerged
    };
}
