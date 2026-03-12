using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Mcp.Clients;

public sealed class PinkRoosterApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    public async Task<ProjectResponse?> GetProjectByPathAsync(
        string projectPath, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/projects?path={Uri.EscapeDataString(projectPath)}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
    }

    public async Task<ProjectStatusResponse?> GetProjectStatusAsync(
        long projectId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"/api/projects/{projectId}/status", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ProjectStatusResponse>(JsonOptions, ct);
    }

    public async Task<List<NextActionItem>?> GetNextActionsAsync(
        long projectId, int limit = 10, string? entityType = null, CancellationToken ct = default)
    {
        var url = $"/api/projects/{projectId}/next-actions?limit={limit}";
        if (!string.IsNullOrWhiteSpace(entityType))
            url += $"&entityType={Uri.EscapeDataString(entityType)}";

        var response = await httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<List<NextActionItem>>(JsonOptions, ct);
    }

    public async Task<(ProjectResponse Project, bool IsNew)> CreateOrUpdateProjectAsync(
        CreateOrUpdateProjectRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync("/api/projects", request, ct);
        await EnsureSuccessAsync(response, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize project response.");
        return (project, response.StatusCode == HttpStatusCode.Created);
    }

    // ── Feature Request endpoints ──

    public async Task<List<FeatureRequestResponse>> GetFeatureRequestsByProjectAsync(
        long projectId, string? stateFilter = null, CancellationToken ct = default)
    {
        var url = $"/api/projects/{projectId}/feature-requests";
        if (!string.IsNullOrWhiteSpace(stateFilter))
            url += $"?state={Uri.EscapeDataString(stateFilter)}";

        return await httpClient.GetFromJsonAsync<List<FeatureRequestResponse>>(url, JsonOptions, ct) ?? [];
    }

    public async Task<FeatureRequestResponse?> GetFeatureRequestAsync(
        long projectId, int frNumber, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/projects/{projectId}/feature-requests/{frNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);
    }

    public async Task<FeatureRequestResponse> CreateFeatureRequestAsync(
        long projectId, CreateFeatureRequestRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/feature-requests", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize feature request response.");
    }

    public async Task<FeatureRequestResponse?> UpdateFeatureRequestAsync(
        long projectId, int frNumber, UpdateFeatureRequestRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/feature-requests/{frNumber}", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);
    }

    public async Task<FeatureRequestResponse?> ManageUserStoriesAsync(
        long projectId, int frNumber, ManageUserStoriesRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/feature-requests/{frNumber}/user-stories/manage", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);
    }

    // ── Issue endpoints ──

    public async Task<List<IssueResponse>> GetIssuesByProjectAsync(
        long projectId, string? stateFilter = null, CancellationToken ct = default)
    {
        var url = $"/api/projects/{projectId}/issues";
        if (!string.IsNullOrWhiteSpace(stateFilter))
            url += $"?state={Uri.EscapeDataString(stateFilter)}";

        return await httpClient.GetFromJsonAsync<List<IssueResponse>>(url, JsonOptions, ct) ?? [];
    }

    public async Task<IssueResponse?> GetIssueAsync(
        long projectId, int issueNumber, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/projects/{projectId}/issues/{issueNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, ct);
    }

    public async Task<IssueResponse> CreateIssueAsync(
        long projectId, CreateIssueRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/issues", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize issue response.");
    }

    public async Task<IssueResponse?> UpdateIssueAsync(
        long projectId, int issueNumber, UpdateIssueRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/issues/{issueNumber}", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, ct);
    }

    // ── Work Package endpoints ──

    public async Task<List<WorkPackageResponse>> GetWorkPackagesByProjectAsync(
        long projectId, string? stateFilter = null, CancellationToken ct = default)
    {
        var url = $"/api/projects/{projectId}/work-packages";
        if (!string.IsNullOrWhiteSpace(stateFilter))
            url += $"?state={Uri.EscapeDataString(stateFilter)}";

        return await httpClient.GetFromJsonAsync<List<WorkPackageResponse>>(url, JsonOptions, ct) ?? [];
    }

    public async Task<WorkPackageResponse?> GetWorkPackageAsync(
        long projectId, int wpNumber, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);
    }

    public async Task<WorkPackageResponse> CreateWorkPackageAsync(
        long projectId, CreateWorkPackageRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize work package response.");
    }

    public async Task<WorkPackageResponse?> UpdateWorkPackageAsync(
        long projectId, int wpNumber, UpdateWorkPackageRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);
    }

    public async Task<ScaffoldWorkPackageResponse> ScaffoldWorkPackageAsync(
        long projectId, ScaffoldWorkPackageRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/scaffold", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ScaffoldWorkPackageResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize scaffold response.");
    }

    public async Task<DependencyResponse> AddWorkPackageDependencyAsync(
        long projectId, int wpNumber, ManageDependencyRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/dependencies", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<DependencyResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize dependency response.");
    }

    public async Task<bool> RemoveWorkPackageDependencyAsync(
        long projectId, int wpNumber, long dependsOnWpId, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/dependencies/{dependsOnWpId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, ct);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    // ── Phase endpoints ──

    public async Task<PhaseResponse> CreatePhaseAsync(
        long projectId, int wpNumber, CreatePhaseRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/phases", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize phase response.");
    }

    public async Task<PhaseResponse?> UpdatePhaseAsync(
        long projectId, int wpNumber, int phaseNumber, UpdatePhaseRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/phases/{phaseNumber}", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);
    }

    public async Task<PhaseResponse> VerifyAcceptanceCriteriaAsync(
        long projectId, int wpNumber, int phaseNumber, VerifyAcceptanceCriteriaRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/phases/{phaseNumber}/verify", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize phase response.");
    }

    // ── Task endpoints ──

    public async Task<TaskResponse> CreateTaskAsync(
        long projectId, int wpNumber, int phaseNumber, CreateTaskRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks?phaseNumber={phaseNumber}", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize task response.");
    }

    public async Task<BatchUpdateTaskStatesResponse?> BatchUpdateTaskStatesAsync(
        long projectId, int wpNumber, BatchUpdateTaskStatesRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/batch-states", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<BatchUpdateTaskStatesResponse>(JsonOptions, ct);
    }

    public async Task<TaskResponse?> UpdateTaskAsync(
        long projectId, int wpNumber, int taskNumber, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/{taskNumber}", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions, ct);
    }

    public async Task<TaskDependencyResponse> AddTaskDependencyAsync(
        long projectId, int wpNumber, int taskNumber, ManageDependencyRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/{taskNumber}/dependencies", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TaskDependencyResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize task dependency response.");
    }

    public async Task<bool> RemoveTaskDependencyAsync(
        long projectId, int wpNumber, int taskNumber, long dependsOnTaskId, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/{taskNumber}/dependencies/{dependsOnTaskId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, ct);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    // ── Delete endpoints ──

    public async Task<bool> DeleteIssueAsync(long projectId, int issueNumber, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/projects/{projectId}/issues/{issueNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, ct);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<bool> DeleteFeatureRequestAsync(long projectId, int frNumber, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/projects/{projectId}/feature-requests/{frNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, ct);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<bool> DeleteWorkPackageAsync(long projectId, int wpNumber, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, ct);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<bool> DeletePhaseAsync(long projectId, int wpNumber, int phaseNumber, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/phases/{phaseNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, ct);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<bool> DeleteTaskAsync(long projectId, int wpNumber, int taskNumber, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/{taskNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, ct);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var error = await ReadErrorMessageAsync(response, ct);
        throw new HttpRequestException(error);
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            // Custom error shape: { "error": "message" }
            if (body.TryGetProperty("error", out var errorProp))
                return errorProp.GetString() ?? response.ReasonPhrase ?? "Unknown error";

            // ASP.NET Core ValidationProblemDetails: { "errors": { "Field": ["message"] } }
            if (body.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var field in errorsProp.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var msg in field.Value.EnumerateArray())
                            messages.Add($"{field.Name}: {msg.GetString()}");
                    }
                }
                if (messages.Count > 0)
                    return string.Join("; ", messages);
            }

            // RFC 7807 ProblemDetails: { "title": "message", "detail": "details" }
            if (body.TryGetProperty("detail", out var detailProp))
                return detailProp.GetString() ?? response.ReasonPhrase ?? "Unknown error";
            if (body.TryGetProperty("title", out var titleProp))
                return titleProp.GetString() ?? response.ReasonPhrase ?? "Unknown error";
        }
        catch
        {
            // Fall through to default
        }
        return $"{(int)response.StatusCode} {response.ReasonPhrase}";
    }
}
