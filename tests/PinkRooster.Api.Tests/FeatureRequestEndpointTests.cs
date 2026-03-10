using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class FeatureRequestEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/fr-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);
        return project!.Id;
    }

    private string FrPath(long projectId) => $"{BasePath}/{projectId}/feature-requests";
    private string WpPath(long projectId) => $"{BasePath}/{projectId}/work-packages";

    private static CreateFeatureRequestRequest MakeFrRequest(string name = "Test FR") => new()
    {
        Name = name,
        Description = "Test feature request",
        Category = FeatureCategory.Feature
    };

    // ── CRUD ──

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoFeatureRequests()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var frs = await GetJson<List<FeatureRequestResponse>>(FrPath(projectId), ct);

        Assert.NotNull(frs);
        Assert.Empty(frs);
    }

    [Fact]
    public async Task Post_CreatesFeatureRequest_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.NotNull(fr);
        Assert.Equal(1, fr.FeatureRequestNumber);
        Assert.Equal($"proj-{projectId}-fr-1", fr.FeatureRequestId);
        Assert.Equal("Test FR", fr.Name);
        Assert.Equal("Feature", fr.Category);
        Assert.Equal("Medium", fr.Priority);
        Assert.Equal("Proposed", fr.Status);
    }

    [Fact]
    public async Task Post_SequentialNumbering_AcrossMultipleFrs()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest("FR-A"), ct);
        var response = await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest("FR-B"), ct);
        var fr2 = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.Equal(2, fr2!.FeatureRequestNumber);
    }

    [Fact]
    public async Task Post_WithAllFields_PersistsOptionalFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var request = new CreateFeatureRequestRequest
        {
            Name = "Full FR",
            Description = "Full description",
            Category = FeatureCategory.Enhancement,
            Priority = Priority.Critical,
            Status = FeatureStatus.UnderReview,
            BusinessValue = "High ROI",
            UserStory = "As a user, I want...",
            Requester = "product-team",
            AcceptanceSummary = "Must pass all criteria"
        };

        var response = await Client.PostAsJsonAsync(FrPath(projectId), request, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.Equal("Enhancement", fr!.Category);
        Assert.Equal("Critical", fr.Priority);
        Assert.Equal("UnderReview", fr.Status);
        Assert.Equal("High ROI", fr.BusinessValue);
        Assert.Equal("As a user, I want...", fr.UserStory);
        Assert.Equal("product-team", fr.Requester);
        Assert.Equal("Must pass all criteria", fr.AcceptanceSummary);
    }

    [Fact]
    public async Task GetByNumber_ReturnsFeatureRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);

        var fr = await GetJson<FeatureRequestResponse>($"{FrPath(projectId)}/1", ct);

        Assert.NotNull(fr);
        Assert.Equal("Test FR", fr.Name);
    }

    [Fact]
    public async Task GetByNumber_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.GetAsync($"{FrPath(projectId)}/999", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_UpdatesFields_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);

        var update = new UpdateFeatureRequestRequest { Name = "Renamed FR", Priority = Priority.High };
        var response = await Client.PatchAsJsonAsync($"{FrPath(projectId)}/1", update, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);
        Assert.Equal("Renamed FR", fr!.Name);
        Assert.Equal("High", fr.Priority);
    }

    [Fact]
    public async Task Patch_NullFieldsAreNotChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "Original",
            Description = "Original desc",
            Category = FeatureCategory.Enhancement,
            Priority = Priority.High
        }, ct);

        // Only update name, leave others untouched
        var update = new UpdateFeatureRequestRequest { Name = "Updated" };
        var response = await Client.PatchAsJsonAsync($"{FrPath(projectId)}/1", update, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.Equal("Updated", fr!.Name);
        Assert.Equal("Original desc", fr.Description);
        Assert.Equal("Enhancement", fr.Category);
        Assert.Equal("High", fr.Priority);
    }

    [Fact]
    public async Task Patch_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.PatchAsJsonAsync($"{FrPath(projectId)}/999",
            new UpdateFeatureRequestRequest { Name = "x" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesFeatureRequest_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);

        var response = await Client.DeleteAsync($"{FrPath(projectId)}/1", ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"{FrPath(projectId)}/1", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.DeleteAsync($"{FrPath(projectId)}/999", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── State Timestamps ──

    [Fact]
    public async Task Post_WithActiveStatus_SetsStartedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var request = new CreateFeatureRequestRequest
        {
            Name = "Active FR",
            Description = "Test",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.UnderReview
        };

        var response = await Client.PostAsJsonAsync(FrPath(projectId), request, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.NotNull(fr!.StartedAt);
        Assert.Equal("UnderReview", fr.Status);
    }

    [Fact]
    public async Task Patch_StatusToActive_SetsStartedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);

        var update = new UpdateFeatureRequestRequest { Status = FeatureStatus.InProgress };
        var response = await Client.PatchAsJsonAsync($"{FrPath(projectId)}/1", update, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.Equal("InProgress", fr!.Status);
        Assert.NotNull(fr.StartedAt);
        Assert.Null(fr.CompletedAt);
    }

    [Fact]
    public async Task Patch_StatusToCompleted_SetsCompletedAtAndResolvedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.InProgress
        }, ct);

        var update = new UpdateFeatureRequestRequest { Status = FeatureStatus.Completed };
        var response = await Client.PatchAsJsonAsync($"{FrPath(projectId)}/1", update, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.Equal("Completed", fr!.Status);
        Assert.NotNull(fr.StartedAt);
        Assert.NotNull(fr.CompletedAt);
        Assert.NotNull(fr.ResolvedAt);
    }

    [Fact]
    public async Task Patch_StatusToRejected_SetsResolvedAtButNotCompletedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.UnderReview
        }, ct);

        var update = new UpdateFeatureRequestRequest { Status = FeatureStatus.Rejected };
        var response = await Client.PatchAsJsonAsync($"{FrPath(projectId)}/1", update, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.Equal("Rejected", fr!.Status);
        Assert.NotNull(fr.StartedAt);
        Assert.Null(fr.CompletedAt);
        Assert.NotNull(fr.ResolvedAt);
    }

    [Fact]
    public async Task Patch_StatusFromTerminalToActive_ClearsResolvedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.InProgress
        }, ct);

        // Move to Completed
        await Client.PatchAsJsonAsync($"{FrPath(projectId)}/1",
            new UpdateFeatureRequestRequest { Status = FeatureStatus.Completed }, ct);

        // Move back to InProgress
        var response = await Client.PatchAsJsonAsync($"{FrPath(projectId)}/1",
            new UpdateFeatureRequestRequest { Status = FeatureStatus.InProgress }, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        Assert.Equal("InProgress", fr!.Status);
        Assert.NotNull(fr.StartedAt);
        Assert.Null(fr.CompletedAt);
        Assert.Null(fr.ResolvedAt);
    }

    // ── State Filter ──

    [Fact]
    public async Task GetAll_WithStateFilter_FiltersCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Proposed (inactive)
        await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest("Inactive FR"), ct);

        // InProgress (active)
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "Active FR",
            Description = "Test",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.InProgress
        }, ct);

        // Completed (terminal)
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "Terminal FR",
            Description = "Test",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.Completed
        }, ct);

        var activeFrs = await GetJson<List<FeatureRequestResponse>>($"{FrPath(projectId)}?state=active", ct);
        var inactiveFrs = await GetJson<List<FeatureRequestResponse>>($"{FrPath(projectId)}?state=inactive", ct);
        var terminalFrs = await GetJson<List<FeatureRequestResponse>>($"{FrPath(projectId)}?state=terminal", ct);

        Assert.Single(activeFrs);
        Assert.Equal("Active FR", activeFrs[0].Name);
        Assert.Single(inactiveFrs);
        Assert.Equal("Inactive FR", inactiveFrs[0].Name);
        Assert.Single(terminalFrs);
        Assert.Equal("Terminal FR", terminalFrs[0].Name);
    }

    // ── Linked Work Packages ──

    [Fact]
    public async Task GetFeatureRequest_ReturnsLinkedWorkPackages()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create feature request
        var frResponse = await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);
        var fr = await frResponse.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        // Create WP linked to this FR
        var wpResponse = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Implementation WP",
            Description = "Implements the feature",
            LinkedFeatureRequestId = fr!.Id
        }, ct);
        Assert.Equal(HttpStatusCode.Created, wpResponse.StatusCode);

        // Fetch FR detail — should include linked WP
        var detail = await GetJson<FeatureRequestResponse>($"{FrPath(projectId)}/1", ct);

        Assert.NotNull(detail);
        Assert.Single(detail.LinkedWorkPackages);
        Assert.Equal("Implementation WP", detail.LinkedWorkPackages[0].Name);
        Assert.Equal("NotStarted", detail.LinkedWorkPackages[0].State);
        Assert.Equal("Feature", detail.LinkedWorkPackages[0].Type);
    }

    [Fact]
    public async Task GetFeatureRequest_ReturnsEmptyLinkedWorkPackages_WhenNoneLinked()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);

        var detail = await GetJson<FeatureRequestResponse>($"{FrPath(projectId)}/1", ct);

        Assert.NotNull(detail);
        Assert.Empty(detail.LinkedWorkPackages);
    }

    [Fact]
    public async Task GetFeatureRequestList_ReturnsLinkedWorkPackages()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create FR
        var frResponse = await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);
        var fr = await frResponse.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        // Create 2 WPs linked to the same FR
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "WP-1",
            Description = "First WP",
            LinkedFeatureRequestId = fr!.Id
        }, ct);
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "WP-2",
            Description = "Second WP",
            LinkedFeatureRequestId = fr.Id
        }, ct);

        var frs = await GetJson<List<FeatureRequestResponse>>(FrPath(projectId), ct);

        Assert.Single(frs);
        Assert.Equal(2, frs[0].LinkedWorkPackages.Count);
    }

    // ── Delete cascades (WP FK SetNull) ──

    [Fact]
    public async Task Delete_FeatureRequest_SetsLinkedWpFkToNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create FR then WP linked to it
        var frResponse = await Client.PostAsJsonAsync(FrPath(projectId), MakeFrRequest(), ct);
        var fr = await frResponse.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Linked WP",
            Description = "Linked to FR",
            LinkedFeatureRequestId = fr!.Id
        }, ct);

        // Delete the FR
        var deleteResponse = await Client.DeleteAsync($"{FrPath(projectId)}/1", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // WP should still exist but LinkedFeatureRequestId should be null
        var wp = await GetJson<WorkPackageResponse>($"{WpPath(projectId)}/1", ct);
        Assert.NotNull(wp);
        Assert.Null(wp.LinkedFeatureRequestId);
    }

    // ── Per-project isolation ──

    [Fact]
    public async Task SequentialNumbering_IsPerProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var proj1 = await CreateProjectAsync(ct);

        // Create a second project with unique path
        var resp2 = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "Project2",
            Description = "Test2",
            ProjectPath = $"/tmp/fr-test2-{Guid.NewGuid():N}"
        }, ct);
        var proj2 = (await resp2.Content.ReadFromJsonAsync<ProjectResponse>(ct))!.Id;

        // Create FRs in both projects
        await Client.PostAsJsonAsync(FrPath(proj1), MakeFrRequest("P1-FR1"), ct);
        await Client.PostAsJsonAsync(FrPath(proj1), MakeFrRequest("P1-FR2"), ct);
        var response = await Client.PostAsJsonAsync(FrPath(proj2), MakeFrRequest("P2-FR1"), ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(ct);

        // Project 2's first FR should be number 1, not 3
        Assert.Equal(1, fr!.FeatureRequestNumber);
    }
}
