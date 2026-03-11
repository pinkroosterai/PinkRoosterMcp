using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Mcp.Clients;

public sealed class PinkRoosterApiClient(HttpClient httpClient)
{
    public async Task<ProjectResponse?> GetProjectByPathAsync(
        string projectPath, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/projects?path={Uri.EscapeDataString(projectPath)}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);
    }

    public async Task<ProjectStatusResponse?> GetProjectStatusAsync(
        long projectId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"/api/projects/{projectId}/status", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ProjectStatusResponse>(ct);
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
        return await response.Content.ReadFromJsonAsync<List<NextActionItem>>(ct);
    }

    public async Task<(ProjectResponse Project, bool IsNew)> CreateOrUpdateProjectAsync(
        CreateOrUpdateProjectRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync("/api/projects", request, ct);
        await EnsureSuccessAsync(response, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct)
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

        return await httpClient.GetFromJsonAsync<List<FeatureRequestResponse>>(url, ct) ?? [];
    }

    public async Task<FeatureRequestResponse?> GetFeatureRequestAsync(
        long projectId, int frNumber, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/projects/{projectId}/feature-requests/{frNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);
    }

    public async Task<FeatureRequestResponse> CreateFeatureRequestAsync(
        long projectId, CreateFeatureRequestRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/feature-requests", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct)
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
        return await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);
    }

    public async Task<FeatureRequestResponse?> ManageUserStoriesAsync(
        long projectId, int frNumber, ManageUserStoriesRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/feature-requests/{frNumber}/user-stories/manage", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);
    }

    // ── Issue endpoints ──

    public async Task<List<IssueResponse>> GetIssuesByProjectAsync(
        long projectId, string? stateFilter = null, CancellationToken ct = default)
    {
        var url = $"/api/projects/{projectId}/issues";
        if (!string.IsNullOrWhiteSpace(stateFilter))
            url += $"?state={Uri.EscapeDataString(stateFilter)}";

        return await httpClient.GetFromJsonAsync<List<IssueResponse>>(url, ct) ?? [];
    }

    public async Task<IssueResponse?> GetIssueAsync(
        long projectId, int issueNumber, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/projects/{projectId}/issues/{issueNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<IssueResponse>(ct);
    }

    public async Task<IssueResponse> CreateIssueAsync(
        long projectId, CreateIssueRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/issues", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<IssueResponse>(ct)
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
        return await response.Content.ReadFromJsonAsync<IssueResponse>(ct);
    }

    // ── Work Package endpoints ──

    public async Task<List<WorkPackageResponse>> GetWorkPackagesByProjectAsync(
        long projectId, string? stateFilter = null, CancellationToken ct = default)
    {
        var url = $"/api/projects/{projectId}/work-packages";
        if (!string.IsNullOrWhiteSpace(stateFilter))
            url += $"?state={Uri.EscapeDataString(stateFilter)}";

        return await httpClient.GetFromJsonAsync<List<WorkPackageResponse>>(url, ct) ?? [];
    }

    public async Task<WorkPackageResponse?> GetWorkPackageAsync(
        long projectId, int wpNumber, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);
    }

    public async Task<WorkPackageResponse> CreateWorkPackageAsync(
        long projectId, CreateWorkPackageRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct)
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
        return await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);
    }

    public async Task<ScaffoldWorkPackageResponse> ScaffoldWorkPackageAsync(
        long projectId, ScaffoldWorkPackageRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/scaffold", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ScaffoldWorkPackageResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize scaffold response.");
    }

    public async Task<DependencyResponse> AddWorkPackageDependencyAsync(
        long projectId, int wpNumber, ManageDependencyRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/dependencies", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<DependencyResponse>(ct)
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
        return await response.Content.ReadFromJsonAsync<PhaseResponse>(ct)
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
        return await response.Content.ReadFromJsonAsync<PhaseResponse>(ct);
    }

    // ── Task endpoints ──

    public async Task<TaskResponse> CreateTaskAsync(
        long projectId, int wpNumber, int phaseNumber, CreateTaskRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks?phaseNumber={phaseNumber}", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TaskResponse>(ct)
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
        return await response.Content.ReadFromJsonAsync<BatchUpdateTaskStatesResponse>(ct);
    }

    public async Task<TaskResponse?> UpdateTaskAsync(
        long projectId, int wpNumber, int taskNumber, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/{taskNumber}", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TaskResponse>(ct);
    }

    public async Task<TaskDependencyResponse> AddTaskDependencyAsync(
        long projectId, int wpNumber, int taskNumber, ManageDependencyRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/{taskNumber}/dependencies", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TaskDependencyResponse>(ct)
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
            if (body.TryGetProperty("error", out var errorProp))
                return errorProp.GetString() ?? response.ReasonPhrase ?? "Unknown error";
        }
        catch
        {
            // Fall through to default
        }
        return $"{(int)response.StatusCode} {response.ReasonPhrase}";
    }
}
