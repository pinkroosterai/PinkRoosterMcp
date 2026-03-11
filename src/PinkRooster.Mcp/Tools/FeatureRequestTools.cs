using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Helpers;
using PinkRooster.Mcp.Inputs;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Enums;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class FeatureRequestTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "create_or_update_feature_request",
        Title = "Create or Update Feature Request", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a new feature request or updates an existing one. " +
        "To create: provide projectId and required fields (name, description, category). " +
        "To update: provide projectId and featureRequestId, plus any fields to change. " +
        "Feature requests track ideas and enhancements — use create_or_update_issue for bugs.")]
    public async Task<string> CreateOrUpdateFeatureRequest(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Feature request ID (e.g. 'proj-1-fr-3'). Omit to create a new feature request.")] string? featureRequestId = null,
        [Description("Feature request name/title.")] string? name = null,
        [Description("Detailed description of the feature request.")] string? description = null,
        [Description("Feature category. Required for create.")] FeatureCategory? category = null,
        [Description("Priority level. Default: Medium.")] Priority? priority = null,
        [Description("Feature status (e.g. Proposed, UnderReview, Approved, Scheduled, InProgress, Completed, Rejected, Deferred). Omit to keep current.")] FeatureStatus? status = null,
        [Description("Business value / justification for the feature.")] string? businessValue = null,
        [Description("User stories for this feature (structured role/goal/benefit). Only applied on create.")] List<UserStoryInput>? userStories = null,
        [Description("Who or what requested this feature.")] string? requester = null,
        [Description("High-level acceptance criteria summary.")] string? acceptanceSummary = null,
        [Description("File attachments.")] List<FileReferenceInput>? attachments = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        try
        {
            if (featureRequestId is not null)
                return await UpdateExisting(projId, featureRequestId, name, description, category,
                    priority, status, businessValue, requester, acceptanceSummary, attachments, ct);

            return await CreateNew(projId, name, description, category,
                priority, status, businessValue, userStories, requester, acceptanceSummary, attachments, ct);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to create/update feature request: {ex.Message}");
        }
    }

    [McpServerTool(Name = "get_feature_request_details", ReadOnly = true,
        Title = "Get Feature Request Details", OpenWorld = false)]
    [Description(
        "Returns all fields for a single feature request including status timestamps, attachments, and linked work packages. " +
        "For listing multiple feature requests, use get_feature_requests instead.")]
    public async Task<string> GetFeatureRequestDetails(
        [Description("Feature request ID (e.g. 'proj-1-fr-3').")] string featureRequestId,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseFeatureRequestId(featureRequestId, out var projId, out var frNumber))
            return OperationResult.Error($"Invalid feature request ID format: '{featureRequestId}'. Expected 'proj-{{number}}-fr-{{number}}'.");

        try
        {
            var fr = await apiClient.GetFeatureRequestAsync(projId, frNumber, ct);
            if (fr is null)
                return OperationResult.Warning($"Feature request '{featureRequestId}' not found.");

            var detail = new FeatureRequestDetailResponse
            {
                FeatureRequestId = fr.FeatureRequestId,
                ProjectId = fr.ProjectId,
                Name = fr.Name,
                Description = fr.Description,
                Category = fr.Category,
                Priority = fr.Priority,
                Status = fr.Status,
                BusinessValue = fr.BusinessValue,
                UserStories = fr.UserStories,
                Requester = fr.Requester,
                AcceptanceSummary = fr.AcceptanceSummary,
                Attachments = fr.Attachments,
                LinkedWorkPackages = McpInputParser.NullIfEmpty(fr.LinkedWorkPackages),
                StartedAt = fr.StartedAt,
                CompletedAt = fr.CompletedAt,
                ResolvedAt = fr.ResolvedAt,
                CreatedAt = fr.CreatedAt,
                UpdatedAt = fr.UpdatedAt
            };

            return JsonSerializer.Serialize(detail, JsonDefaults.Indented);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to fetch feature request: {ex.Message}");
        }
    }

    [McpServerTool(Name = "get_feature_requests", ReadOnly = true,
        Title = "Get Feature Requests", OpenWorld = false)]
    [Description(
        "Returns a compact list of feature requests (ID, name, status, priority, category) for a project. " +
        "For full data, use get_feature_request_details instead. " +
        "For feature request counts by category, use get_project_status.")]
    public async Task<string> GetFeatureRequests(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Filter by status category. Omit for all feature requests.")] StateFilterCategory? stateFilter = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        try
        {
            var stateFilterStr = stateFilter?.ToString().ToLowerInvariant();
            var frs = await apiClient.GetFeatureRequestsByProjectAsync(projId, stateFilterStr, ct);

            if (frs.Count == 0)
                return OperationResult.SuccessMessage($"No feature requests found for project '{projectId}'" +
                    (stateFilter is not null ? $" with filter '{stateFilter}'." : "."));

            var items = frs.Select(fr => new
            {
                fr.FeatureRequestId,
                fr.Name,
                fr.Status,
                fr.Priority,
                fr.Category,
                fr.Requester,
                LinkedWorkPackageCount = fr.LinkedWorkPackages.Count,
                fr.CreatedAt
            }).ToList();

            return JsonSerializer.Serialize(items, JsonDefaults.Indented);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to fetch feature requests: {ex.Message}");
        }
    }

    [McpServerTool(Name = "manage_user_stories",
        Title = "Manage User Stories", Destructive = false, OpenWorld = false)]
    [Description(
        "Add, update, or remove a user story on a feature request. " +
        "Each user story has structured fields: role, goal, benefit (maps to 'As a [role], I want [goal], so that [benefit]'). " +
        "Use index (0-based) to target a specific story for Update or Remove.")]
    public async Task<string> ManageUserStories(
        [Description("Feature request ID (e.g. 'proj-1-fr-3').")] string featureRequestId,
        [Description("Action to perform.")] UserStoryAction action,
        [Description("0-based index of the user story to update or remove. Required for Update and Remove.")] int? index = null,
        [Description("The user role (e.g. 'developer', 'project manager'). Required for Add and Update.")] string? role = null,
        [Description("What the user wants to achieve. Required for Add and Update.")] string? goal = null,
        [Description("Why the user wants this (the benefit). Required for Add and Update.")] string? benefit = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseFeatureRequestId(featureRequestId, out var projId, out var frNumber))
            return OperationResult.Error($"Invalid feature request ID format: '{featureRequestId}'. Expected 'proj-{{number}}-fr-{{number}}'.");

        try
        {
            var request = new ManageUserStoriesRequest
            {
                Action = action.ToString(),
                Index = index,
                Role = role,
                Goal = goal,
                Benefit = benefit
            };

            var fr = await apiClient.ManageUserStoriesAsync(projId, frNumber, request, ct);
            if (fr is null)
                return OperationResult.Warning($"Feature request '{featureRequestId}' not found.");

            var actionVerb = action switch
            {
                UserStoryAction.Add => "added to",
                UserStoryAction.Update => "updated on",
                UserStoryAction.Remove => "removed from",
                _ => "modified on"
            };

            return OperationResult.Success(featureRequestId,
                $"User story {actionVerb} '{featureRequestId}'. Total stories: {fr.UserStories.Count}.");
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to manage user stories: {ex.Message}");
        }
    }

    [McpServerTool(Name = "delete_feature_request",
        Title = "Delete Feature Request", Destructive = true, OpenWorld = false)]
    [Description(
        "Permanently deletes a feature request. Linked work packages will have their FR link cleared (not deleted). " +
        "This action cannot be undone.")]
    public async Task<string> DeleteFeatureRequest(
        [Description("Feature request ID (e.g. 'proj-1-fr-3').")] string featureRequestId,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseFeatureRequestId(featureRequestId, out var projId, out var frNumber))
            return OperationResult.Error($"Invalid feature request ID format: '{featureRequestId}'. Expected 'proj-{{number}}-fr-{{number}}'.");

        try
        {
            var deleted = await apiClient.DeleteFeatureRequestAsync(projId, frNumber, ct);
            return deleted
                ? OperationResult.Success(featureRequestId, $"Deleted feature request '{featureRequestId}'.")
                : OperationResult.Warning($"Feature request '{featureRequestId}' not found.");
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Error($"API error: {ex.Message}");
        }
    }

    // ── Private helpers ──

    private async Task<string> CreateNew(
        long projId, string? name, string? description, FeatureCategory? category,
        Priority? priority, FeatureStatus? status, string? businessValue, List<UserStoryInput>? userStories,
        string? requester, string? acceptanceSummary, List<FileReferenceInput>? attachments, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required when creating a feature request.");
        if (string.IsNullOrWhiteSpace(description))
            return OperationResult.Error("'description' is required when creating a feature request.");
        if (category is null)
            return OperationResult.Error("'category' is required when creating a feature request.");

        var request = new CreateFeatureRequestRequest
        {
            Name = name,
            Description = description,
            Category = category.Value,
            Priority = priority ?? Priority.Medium,
            Status = status ?? FeatureStatus.Proposed,
            BusinessValue = businessValue,
            UserStories = McpInputParser.MapUserStories(userStories),
            Requester = requester,
            AcceptanceSummary = acceptanceSummary,
            Attachments = McpInputParser.MapFileReferences(attachments)
        };

        var created = await apiClient.CreateFeatureRequestAsync(projId, request, ct);
        return OperationResult.Success(created.FeatureRequestId, $"Feature request '{name}' created.");
    }

    private async Task<string> UpdateExisting(
        long projId, string featureRequestId, string? name, string? description, FeatureCategory? category,
        Priority? priority, FeatureStatus? status, string? businessValue,
        string? requester, string? acceptanceSummary, List<FileReferenceInput>? attachments, CancellationToken ct)
    {
        if (!IdParser.TryParseFeatureRequestId(featureRequestId, out var parsedProjId, out var frNumber))
            return OperationResult.Error($"Invalid feature request ID format: '{featureRequestId}'. Expected 'proj-{{number}}-fr-{{number}}'.");

        if (parsedProjId != projId)
            return OperationResult.Error($"Feature request ID '{featureRequestId}' does not belong to project 'proj-{projId}'.");

        var request = new UpdateFeatureRequestRequest
        {
            Name = name,
            Description = description,
            Category = category,
            Priority = priority,
            Status = status,
            BusinessValue = businessValue,
            Requester = requester,
            AcceptanceSummary = acceptanceSummary,
            Attachments = attachments is not null ? McpInputParser.MapFileReferences(attachments) : null
        };

        var updated = await apiClient.UpdateFeatureRequestAsync(projId, frNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Feature request '{featureRequestId}' not found.");

        return OperationResult.Success(featureRequestId, $"Feature request '{featureRequestId}' updated.");
    }
}
