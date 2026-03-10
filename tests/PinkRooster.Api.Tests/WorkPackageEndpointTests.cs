using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class WorkPackageEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync($"{BasePath}", new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/wp-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);
        return project!.Id;
    }

    private string WpPath(long projectId) => $"{BasePath}/{projectId}/work-packages";

    private static CreateWorkPackageRequest MakeWpRequest(string name = "Test WP") => new()
    {
        Name = name,
        Description = "Test work package"
    };

    // ── CRUD ──

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoWorkPackages()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var wps = await GetJson<List<WorkPackageResponse>>(WpPath(projectId), ct);

        Assert.NotNull(wps);
        Assert.Empty(wps);
    }

    [Fact]
    public async Task Post_CreatesWorkPackage_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest(), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var wp = await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        Assert.NotNull(wp);
        Assert.Equal(1, wp.WorkPackageNumber);
        Assert.Equal($"proj-{projectId}-wp-1", wp.WorkPackageId);
        Assert.Equal("Test WP", wp.Name);
        Assert.Equal("Feature", wp.Type);
        Assert.Equal("Medium", wp.Priority);
        Assert.Equal("NotStarted", wp.State);
    }

    [Fact]
    public async Task Post_SequentialNumbering_AcrossMultipleWps()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-A"), ct);
        var response = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-B"), ct);
        var wp2 = await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        Assert.Equal(2, wp2!.WorkPackageNumber);
    }

    [Fact]
    public async Task Post_WithActiveState_SetsStartedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var request = MakeWpRequest();
        request.State = Shared.Enums.CompletionState.Implementing;

        var response = await Client.PostAsJsonAsync(WpPath(projectId), request, ct);
        var wp = await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        Assert.NotNull(wp!.StartedAt);
        Assert.Equal("Implementing", wp.State);
    }

    [Fact]
    public async Task Post_WithAllFields_PersistsOptionalFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var request = new CreateWorkPackageRequest
        {
            Name = "Full WP",
            Description = "Full description",
            Type = Shared.Enums.WorkPackageType.Refactor,
            Priority = Shared.Enums.Priority.Critical,
            Plan = "The plan",
            EstimatedComplexity = 5,
            EstimationRationale = "Medium complexity"
        };

        var response = await Client.PostAsJsonAsync(WpPath(projectId), request, ct);
        var wp = await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        Assert.Equal("Refactor", wp!.Type);
        Assert.Equal("Critical", wp.Priority);
        Assert.Equal("The plan", wp.Plan);
        Assert.Equal(5, wp.EstimatedComplexity);
        Assert.Equal("Medium complexity", wp.EstimationRationale);
    }

    [Fact]
    public async Task GetByNumber_ReturnsWorkPackage()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest(), ct);

        var wp = await GetJson<WorkPackageResponse>($"{WpPath(projectId)}/1", ct);

        Assert.NotNull(wp);
        Assert.Equal("Test WP", wp.Name);
        Assert.Empty(wp.Phases);
    }

    [Fact]
    public async Task GetByNumber_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.GetAsync($"{WpPath(projectId)}/999", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_UpdatesFields_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest(), ct);

        var update = new UpdateWorkPackageRequest { Name = "Renamed WP", Priority = Shared.Enums.Priority.High };
        var response = await Client.PatchAsJsonAsync($"{WpPath(projectId)}/1", update, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wp = await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);
        Assert.Equal("Renamed WP", wp!.Name);
        Assert.Equal("High", wp.Priority);
    }

    [Fact]
    public async Task Patch_NullFieldsAreNotChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Original",
            Description = "Original desc",
            Priority = Shared.Enums.Priority.High
        }, ct);

        // Only update name, leave description and priority untouched
        var update = new UpdateWorkPackageRequest { Name = "Updated" };
        var response = await Client.PatchAsJsonAsync($"{WpPath(projectId)}/1", update, ct);
        var wp = await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        Assert.Equal("Updated", wp!.Name);
        Assert.Equal("Original desc", wp.Description);
        Assert.Equal("High", wp.Priority);
    }

    [Fact]
    public async Task Patch_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.PatchAsJsonAsync($"{WpPath(projectId)}/999",
            new UpdateWorkPackageRequest { Name = "x" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesWorkPackage_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest(), ct);

        var response = await Client.DeleteAsync($"{WpPath(projectId)}/1", ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"{WpPath(projectId)}/1", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.DeleteAsync($"{WpPath(projectId)}/999", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── State Timestamps ──

    [Fact]
    public async Task Patch_StateToActive_SetsStartedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest(), ct);

        var update = new UpdateWorkPackageRequest { State = Shared.Enums.CompletionState.Implementing };
        var response = await Client.PatchAsJsonAsync($"{WpPath(projectId)}/1", update, ct);
        var wp = await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        Assert.Equal("Implementing", wp!.State);
        Assert.NotNull(wp.StartedAt);
        Assert.Null(wp.CompletedAt);
    }

    [Fact]
    public async Task Patch_StateToCompleted_SetsCompletedAtAndResolvedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var create = MakeWpRequest();
        create.State = Shared.Enums.CompletionState.Implementing;
        await Client.PostAsJsonAsync(WpPath(projectId), create, ct);

        var update = new UpdateWorkPackageRequest { State = Shared.Enums.CompletionState.Completed };
        var response = await Client.PatchAsJsonAsync($"{WpPath(projectId)}/1", update, ct);
        var wp = await response.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        Assert.Equal("Completed", wp!.State);
        Assert.NotNull(wp.StartedAt);
        Assert.NotNull(wp.CompletedAt);
        Assert.NotNull(wp.ResolvedAt);
    }

    // ── State Filter ──

    [Fact]
    public async Task GetAll_WithStateFilter_FiltersCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // NotStarted (inactive)
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("Inactive"), ct);
        // Implementing (active)
        var active = MakeWpRequest("Active");
        active.State = Shared.Enums.CompletionState.Implementing;
        await Client.PostAsJsonAsync(WpPath(projectId), active, ct);

        var activeWps = await GetJson<List<WorkPackageResponse>>($"{WpPath(projectId)}?state=active", ct);
        var inactiveWps = await GetJson<List<WorkPackageResponse>>($"{WpPath(projectId)}?state=inactive", ct);

        Assert.Single(activeWps);
        Assert.Equal("Active", activeWps[0].Name);
        Assert.Single(inactiveWps);
        Assert.Equal("Inactive", inactiveWps[0].Name);
    }

    // ── Summary ──

    [Fact]
    public async Task GetSummary_ReturnsCategoryCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("A"), ct); // inactive
        var active = MakeWpRequest("B");
        active.State = Shared.Enums.CompletionState.Implementing;
        await Client.PostAsJsonAsync(WpPath(projectId), active, ct); // active

        var summary = await GetJson<WorkPackageSummaryResponse>($"{WpPath(projectId)}/summary", ct);

        Assert.Equal(1, summary.ActiveCount);
        Assert.Equal(1, summary.InactiveCount);
        Assert.Equal(0, summary.TerminalCount);
    }

    // ── Dependencies ──

    [Fact]
    public async Task AddDependency_Returns201_AndAppearsInBlockedBy()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r1 = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-1"), ct);
        var wp1 = await r1.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-2"), ct);

        var depRequest = new ManageDependencyRequest { DependsOnId = wp1!.Id, Reason = "prerequisite" };
        var depResponse = await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies", depRequest, ct);

        Assert.Equal(HttpStatusCode.Created, depResponse.StatusCode);

        var wp2 = await GetJson<WorkPackageResponse>($"{WpPath(projectId)}/2", ct);
        Assert.Single(wp2.BlockedBy);
        Assert.Equal("WP-1", wp2.BlockedBy[0].Name);
        Assert.Equal("prerequisite", wp2.BlockedBy[0].Reason);
    }

    [Fact]
    public async Task AddDependency_AutoBlocksActiveWp()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r1 = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-1"), ct);
        var wp1 = await r1.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        // WP-2 starts as active (Designing)
        var req2 = MakeWpRequest("WP-2");
        req2.State = Shared.Enums.CompletionState.Designing;
        await Client.PostAsJsonAsync(WpPath(projectId), req2, ct);

        var depRequest = new ManageDependencyRequest { DependsOnId = wp1!.Id };
        await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies", depRequest, ct);

        var wp2 = await GetJson<WorkPackageResponse>($"{WpPath(projectId)}/2", ct);
        Assert.Equal("Blocked", wp2.State);
        Assert.Equal("Designing", wp2.PreviousActiveState);
    }

    [Fact]
    public async Task RemoveDependency_AutoUnblocksToRestoredState()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r1 = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-1"), ct);
        var wp1 = await r1.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        var req2 = MakeWpRequest("WP-2");
        req2.State = Shared.Enums.CompletionState.Designing;
        await Client.PostAsJsonAsync(WpPath(projectId), req2, ct);

        // Add then remove
        var depRequest = new ManageDependencyRequest { DependsOnId = wp1!.Id };
        await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies", depRequest, ct);
        await Client.DeleteAsync($"{WpPath(projectId)}/2/dependencies/{wp1.Id}", ct);

        var wp2 = await GetJson<WorkPackageResponse>($"{WpPath(projectId)}/2", ct);
        Assert.Equal("Designing", wp2.State);
        Assert.Null(wp2.PreviousActiveState);
    }

    [Fact]
    public async Task AddDependency_CircularDependency_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r1 = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-1"), ct);
        var wp1 = await r1.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);
        var r2 = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-2"), ct);
        var wp2 = await r2.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        // WP-2 depends on WP-1
        await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = wp1!.Id }, ct);

        // WP-1 depends on WP-2 → circular
        var response = await Client.PostAsJsonAsync($"{WpPath(projectId)}/1/dependencies",
            new ManageDependencyRequest { DependsOnId = wp2!.Id }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddDependency_SelfDependency_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r1 = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-1"), ct);
        var wp1 = await r1.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        var response = await Client.PostAsJsonAsync($"{WpPath(projectId)}/1/dependencies",
            new ManageDependencyRequest { DependsOnId = wp1!.Id }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddDependency_Duplicate_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r1 = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-1"), ct);
        var wp1 = await r1.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-2"), ct);

        var depRequest = new ManageDependencyRequest { DependsOnId = wp1!.Id };
        await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies", depRequest, ct);

        // Duplicate
        var response = await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies", depRequest, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── State Change Cascade Tests ──

    [Fact]
    public async Task AddDependency_ReturnsAutoBlockStateChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create WP1 (blocker) and WP2 (dependent, in Implementing state)
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-Blocker"), ct);
        var r2 = await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-Dependent"), ct);
        var wp2 = await r2.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        // Move WP2 to Implementing
        await Client.PatchAsJsonAsync($"{WpPath(projectId)}/2",
            new UpdateWorkPackageRequest { State = Shared.Enums.CompletionState.Implementing }, ct);

        // Add dependency: WP2 depends on WP1 → should auto-block WP2
        var depResponse = await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = wp2!.Id - 1 }, ct); // wp1 id is wp2.Id - 1 (sequential)

        // We need the wp1 Id - let's get it properly
        var getWp1 = await Client.GetFromJsonAsync<WorkPackageResponse>($"{WpPath(projectId)}/1", ct);

        depResponse = await Client.DeleteAsync($"{WpPath(projectId)}/2/dependencies/{getWp1!.Id}", ct);

        // Re-add with correct ID
        var addResponse = await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = getWp1.Id }, ct);
        var dep = await addResponse.Content.ReadFromJsonAsync<DependencyResponse>(ct);

        Assert.NotNull(dep!.StateChanges);
        Assert.Single(dep.StateChanges);
        Assert.Equal("WorkPackage", dep.StateChanges[0].EntityType);
        Assert.Equal("Implementing", dep.StateChanges[0].OldState);
        Assert.Equal("Blocked", dep.StateChanges[0].NewState);
        Assert.Contains("Auto-blocked", dep.StateChanges[0].Reason);
    }

    [Fact]
    public async Task Update_CompletingBlocker_ReturnsAutoUnblockStateChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create WP1 (blocker, Implementing) and WP2 (dependent, Implementing)
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-Blocker"), ct);
        await Client.PostAsJsonAsync(WpPath(projectId), MakeWpRequest("WP-Dependent"), ct);

        var getWp1 = await Client.GetFromJsonAsync<WorkPackageResponse>($"{WpPath(projectId)}/1", ct);

        // Move both to Implementing
        await Client.PatchAsJsonAsync($"{WpPath(projectId)}/1",
            new UpdateWorkPackageRequest { State = Shared.Enums.CompletionState.Implementing }, ct);
        await Client.PatchAsJsonAsync($"{WpPath(projectId)}/2",
            new UpdateWorkPackageRequest { State = Shared.Enums.CompletionState.Implementing }, ct);

        // Add dependency: WP2 blocked by WP1 (auto-blocks WP2)
        await Client.PostAsJsonAsync($"{WpPath(projectId)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = getWp1!.Id }, ct);

        // Complete WP1 → should auto-unblock WP2
        var updateResponse = await Client.PatchAsJsonAsync($"{WpPath(projectId)}/1",
            new UpdateWorkPackageRequest { State = Shared.Enums.CompletionState.Completed }, ct);
        var updatedWp = await updateResponse.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        Assert.NotNull(updatedWp!.StateChanges);
        Assert.Single(updatedWp.StateChanges);
        Assert.Equal("WorkPackage", updatedWp.StateChanges[0].EntityType);
        Assert.Equal("Blocked", updatedWp.StateChanges[0].OldState);
        Assert.Equal("Implementing", updatedWp.StateChanges[0].NewState);
        Assert.Contains("Auto-unblocked", updatedWp.StateChanges[0].Reason);
    }
}
