using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class MemoryTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "list_memories", ReadOnly = true,
        Title = "List Project Memories", OpenWorld = false)]
    [Description(
        "Returns a compact list of project memories (ID, name, tags, updatedAt). " +
        "Supports filtering by name pattern (case-insensitive substring match) and/or tag. " +
        "For full memory content, use get_memory_details with the memory ID.")]
    public async Task<string> ListMemories(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Filter memories whose name contains this text (case-insensitive). Omit for all.")] string? namePattern = null,
        [Description("Filter memories that have this exact tag. Omit for all.")] string? tag = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        try
        {
            var memories = await apiClient.GetMemoriesByProjectAsync(projId, namePattern, tag, ct);

            if (memories.Count == 0)
                return OperationResult.SuccessMessage($"No memories found for project '{projectId}'" +
                    (namePattern is not null ? $" matching '{namePattern}'" : "") +
                    (tag is not null ? $" with tag '{tag}'" : "") + ".");

            return JsonSerializer.Serialize(memories, JsonDefaults.Indented);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to list memories: {ex.Message}");
        }
    }

    [McpServerTool(Name = "get_memory_details", ReadOnly = true,
        Title = "Get Memory Details", OpenWorld = false)]
    [Description(
        "Returns the full content and metadata for a single project memory. " +
        "Use list_memories to find memory IDs first.")]
    public async Task<string> GetMemoryDetails(
        [Description("Memory ID (e.g. 'proj-1-mem-3').")] string memoryId,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectMemoryId(memoryId, out var projId, out var memoryNumber))
            return OperationResult.Error($"Invalid memory ID format: '{memoryId}'. Expected 'proj-{{number}}-mem-{{number}}'.");

        try
        {
            var memory = await apiClient.GetMemoryAsync(projId, memoryNumber, ct);
            if (memory is null)
                return OperationResult.Warning($"Memory '{memoryId}' not found.");

            return JsonSerializer.Serialize(memory, JsonDefaults.Indented);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to fetch memory: {ex.Message}");
        }
    }

    [McpServerTool(Name = "create_or_update_memory",
        Title = "Create or Update Memory", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Creates a new project memory or merges into an existing one with the same name. " +
        "If a memory with the given name already exists, the new content is appended (separated by '---') " +
        "and tags are merged (union, deduplicated). The response indicates whether a merge occurred via 'wasMerged'. " +
        "Use this for storing project-specific knowledge, decisions, patterns, and context.")]
    public async Task<string> CreateOrUpdateMemory(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Memory name (unique per project). Used as the merge key for upsert.")] string name,
        [Description("Memory content (supports markdown).")] string content,
        [Description("Tags for categorization and filtering. On merge, new tags are added to existing ones.")] List<string>? tags = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required.");
        if (string.IsNullOrWhiteSpace(content))
            return OperationResult.Error("'content' is required.");

        try
        {
            var request = new UpsertProjectMemoryRequest
            {
                Name = name,
                Content = content,
                Tags = tags
            };

            var memory = await apiClient.UpsertMemoryAsync(projId, request, ct);
            var verb = memory.WasMerged ? "merged into" : "created";
            return OperationResult.Success(memory.MemoryId,
                $"Memory '{name}' {verb} as '{memory.MemoryId}'.");
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to create/update memory: {ex.Message}");
        }
    }

    [McpServerTool(Name = "delete_memory",
        Title = "Delete Memory", Destructive = true, OpenWorld = false)]
    [Description(
        "Permanently deletes a project memory by its ID. This action cannot be undone.")]
    public async Task<string> DeleteMemory(
        [Description("Memory ID (e.g. 'proj-1-mem-3').")] string memoryId,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectMemoryId(memoryId, out var projId, out var memoryNumber))
            return OperationResult.Error($"Invalid memory ID format: '{memoryId}'. Expected 'proj-{{number}}-mem-{{number}}'.");

        try
        {
            var deleted = await apiClient.DeleteMemoryAsync(projId, memoryNumber, ct);
            if (!deleted)
                return OperationResult.Warning($"Memory '{memoryId}' not found.");

            return OperationResult.Success(memoryId, $"Memory '{memoryId}' deleted.");
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to delete memory: {ex.Message}");
        }
    }
}
