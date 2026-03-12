using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Tests.Fixtures;

/// <summary>
/// Shared test helper methods to reduce duplication across integration test files.
/// </summary>
public static class TestHelpers
{
    private const string BasePath = "/api/projects";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ── Project helpers ──

    public static async Task<long> CreateProjectAsync(HttpClient client, CancellationToken ct, string? suffix = null)
    {
        suffix ??= Guid.NewGuid().ToString("N");
        var response = await client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/test-{suffix}"
        }, ct);
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return project!.Id;
    }

    public static async Task<(long ProjectId, string HumanId)> CreateProjectWithHumanIdAsync(
        HttpClient client, CancellationToken ct, string? suffix = null)
    {
        suffix ??= Guid.NewGuid().ToString("N");
        var response = await client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/test-{suffix}"
        }, ct);
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return (project!.Id, project.ProjectId);
    }

    // ── Issue helpers ──

    public static string IssuePath(long projectId) => $"{BasePath}/{projectId}/issues";

    public static CreateIssueRequest MakeIssueRequest(string name = "Test Issue") => new()
    {
        Name = name,
        Description = "Test issue description",
        IssueType = IssueType.Bug,
        Severity = IssueSeverity.Major
    };

    public static async Task<IssueResponse> CreateIssueAsync(
        HttpClient client, long projectId, CancellationToken ct,
        string name = "Test Issue", IssueType type = IssueType.Bug,
        IssueSeverity severity = IssueSeverity.Major, CompletionState state = CompletionState.NotStarted)
    {
        var request = new CreateIssueRequest
        {
            Name = name,
            Description = "Test issue description",
            IssueType = type,
            Severity = severity,
            State = state
        };
        var response = await client.PostAsJsonAsync(IssuePath(projectId), request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, ct))!;
    }

    // ── Feature Request helpers ──

    public static string FrPath(long projectId) => $"{BasePath}/{projectId}/feature-requests";

    public static CreateFeatureRequestRequest MakeFrRequest(string name = "Test FR") => new()
    {
        Name = name,
        Description = "Test feature request",
        Category = FeatureCategory.Feature
    };

    public static async Task<FeatureRequestResponse> CreateFeatureRequestAsync(
        HttpClient client, long projectId, CancellationToken ct,
        string name = "Test FR", FeatureCategory category = FeatureCategory.Feature,
        FeatureStatus status = FeatureStatus.Proposed)
    {
        var request = new CreateFeatureRequestRequest
        {
            Name = name,
            Description = "Test feature request",
            Category = category,
            Status = status
        };
        var response = await client.PostAsJsonAsync(FrPath(projectId), request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct))!;
    }

    // ── Work Package helpers ──

    public static string WpPath(long projectId) => $"{BasePath}/{projectId}/work-packages";

    public static CreateWorkPackageRequest MakeWpRequest(string name = "Test WP") => new()
    {
        Name = name,
        Description = "Test work package"
    };

    public static async Task<WorkPackageResponse> CreateWorkPackageAsync(
        HttpClient client, long projectId, CancellationToken ct,
        string name = "Test WP", CompletionState state = CompletionState.NotStarted)
    {
        var request = new CreateWorkPackageRequest
        {
            Name = name,
            Description = "Test work package",
            State = state
        };
        var response = await client.PostAsJsonAsync(WpPath(projectId), request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct))!;
    }

    // ── Phase + Task helpers ──

    public static async Task<(long ProjectId, int WpNumber)> CreateProjectAndWpAsync(
        HttpClient client, CancellationToken ct)
    {
        var projectId = await CreateProjectAsync(client, ct);
        var wp = await CreateWorkPackageAsync(client, projectId, ct);
        return (projectId, wp.WorkPackageNumber);
    }

    public static async Task<(long ProjectId, int WpNumber, int PhaseNumber)> CreateProjectWpAndPhaseAsync(
        HttpClient client, CancellationToken ct)
    {
        var projectId = await CreateProjectAsync(client, ct);
        var wp = await CreateWorkPackageAsync(client, projectId, ct);
        await client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        return (projectId, wp.WorkPackageNumber, 1);
    }

    public static string TaskPath(long projectId, int wpNumber) =>
        $"{BasePath}/{projectId}/work-packages/{wpNumber}/tasks";
}
