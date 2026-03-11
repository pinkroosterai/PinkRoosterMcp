using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class DeleteEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/delete-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return project!.Id;
    }

    // ── Issue deletion ──

    [Fact]
    public async Task DeleteIssue_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/issues", new CreateIssueRequest
        {
            Name = "Bug",
            Description = "A bug",
            IssueType = Shared.Enums.IssueType.Bug,
            Severity = Shared.Enums.IssueSeverity.Major
        }, ct);

        var response = await Client.DeleteAsync($"{BasePath}/{projectId}/issues/1", ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it's gone
        var getResponse = await Client.GetAsync($"{BasePath}/{projectId}/issues/1", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteIssue_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.DeleteAsync($"{BasePath}/{projectId}/issues/999", ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Feature Request deletion ──

    [Fact]
    public async Task DeleteFeatureRequest_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/feature-requests", new CreateFeatureRequestRequest
        {
            Name = "New Feature",
            Description = "A feature",
            Category = Shared.Enums.FeatureCategory.Feature
        }, ct);

        var response = await Client.DeleteAsync($"{BasePath}/{projectId}/feature-requests/1", ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFeatureRequest_ClearsWpLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create FR and capture its ID
        var frResponse = await Client.PostAsJsonAsync($"{BasePath}/{projectId}/feature-requests", new CreateFeatureRequestRequest
        {
            Name = "Linked FR",
            Description = "A feature",
            Category = Shared.Enums.FeatureCategory.Feature
        }, ct);
        var fr = await frResponse.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);

        // Create WP linked to FR (using DB Id for the FK)
        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages", new CreateWorkPackageRequest
        {
            Name = "Linked WP",
            Description = "Test",
            LinkedFeatureRequestIds = [fr!.Id]
        }, ct);

        // Delete the FR (using per-project number in URL)
        var deleteResponse = await Client.DeleteAsync($"{BasePath}/{projectId}/feature-requests/{fr.FeatureRequestNumber}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify WP still exists but link is cleared
        var wpResponse = await Client.GetAsync($"{BasePath}/{projectId}/work-packages/1", ct);
        Assert.Equal(HttpStatusCode.OK, wpResponse.StatusCode);
        var wp = await wpResponse.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);
        Assert.Empty(wp!.LinkedFeatureRequestIds);
    }

    // ── Work Package deletion (cascades to phases/tasks) ──

    [Fact]
    public async Task DeleteWorkPackage_Returns204_CascadesToPhasesAndTasks()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create WP
        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages", new CreateWorkPackageRequest
        {
            Name = "WP with phases",
            Description = "Test"
        }, ct);

        // Create phase with tasks
        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages/1/phases", new CreatePhaseRequest
        {
            Name = "Phase 1",
            Tasks =
            [
                new CreateTaskRequest { Name = "Task 1", Description = "d" },
                new CreateTaskRequest { Name = "Task 2", Description = "d" }
            ]
        }, ct);

        // Delete WP
        var response = await Client.DeleteAsync($"{BasePath}/{projectId}/work-packages/1", ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify WP is gone
        var getResponse = await Client.GetAsync($"{BasePath}/{projectId}/work-packages/1", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ── Phase deletion (cascades to tasks) ──

    [Fact]
    public async Task DeletePhase_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages", new CreateWorkPackageRequest
        {
            Name = "WP",
            Description = "Test"
        }, ct);

        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages/1/phases", new CreatePhaseRequest
        {
            Name = "Phase to delete",
            Tasks = [new CreateTaskRequest { Name = "Task", Description = "d" }]
        }, ct);

        var response = await Client.DeleteAsync($"{BasePath}/{projectId}/work-packages/1/phases/1", ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── Task deletion ──

    [Fact]
    public async Task DeleteTask_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages", new CreateWorkPackageRequest
        {
            Name = "WP",
            Description = "Test"
        }, ct);

        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages/1/phases", new CreatePhaseRequest
        {
            Name = "Phase",
            Tasks = [new CreateTaskRequest { Name = "Doomed task", Description = "d" }]
        }, ct);

        var response = await Client.DeleteAsync($"{BasePath}/{projectId}/work-packages/1/tasks/1", ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages", new CreateWorkPackageRequest
        {
            Name = "WP",
            Description = "Test"
        }, ct);

        var response = await Client.DeleteAsync($"{BasePath}/{projectId}/work-packages/1/tasks/999", ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
