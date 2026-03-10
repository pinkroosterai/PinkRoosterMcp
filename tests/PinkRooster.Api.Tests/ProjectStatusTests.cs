using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class ProjectStatusTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<(long ProjectId, string HumanId)> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "StatusTestProject",
            Description = "Test",
            ProjectPath = $"/tmp/status-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);
        return (project!.Id, project.ProjectId);
    }

    private string StatusPath(long projectId) => $"{BasePath}/{projectId}/status";
    private string IssuePath(long projectId) => $"{BasePath}/{projectId}/issues";
    private string WpPath(long projectId) => $"{BasePath}/{projectId}/work-packages";

    // ── Basic ──

    [Fact]
    public async Task GetStatus_Returns404_WhenProjectNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync(StatusPath(999999), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_ReturnsZeroCounts_WhenProjectEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, humanId) = await CreateProjectAsync(ct);

        var status = await GetJson<ProjectStatusResponse>(StatusPath(projectId), ct);

        Assert.Equal(humanId, status.ProjectId);
        Assert.Equal("StatusTestProject", status.Name);
        Assert.Equal("Active", status.Status);

        Assert.Equal(0, status.Issues.Total);
        Assert.Equal(0, status.Issues.Active);
        Assert.Equal(0, status.Issues.Inactive);
        Assert.Equal(0, status.Issues.Terminal);
        Assert.Equal(0, status.Issues.PercentComplete);
        Assert.Empty(status.Issues.ActiveItems);
        Assert.Empty(status.Issues.InactiveItems);

        Assert.Equal(0, status.WorkPackages.Total);
        Assert.Equal(0, status.WorkPackages.TerminalCount);
        Assert.Equal(0, status.WorkPackages.PercentComplete);
        Assert.Empty(status.WorkPackages.Active);
        Assert.Empty(status.WorkPackages.Inactive);
        Assert.Empty(status.WorkPackages.Blocked);
    }

    // ── Issue counts ──

    [Fact]
    public async Task GetStatus_IssueCountsMatchStateCategories()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create issues in different states
        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Active Issue", Description = "d", IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major, State = CompletionState.Implementing
        }, ct);

        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "NotStarted Issue", Description = "d", IssueType = IssueType.Defect,
            Severity = IssueSeverity.Minor, State = CompletionState.NotStarted
        }, ct);

        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Completed Issue", Description = "d", IssueType = IssueType.Regression,
            Severity = IssueSeverity.Trivial, State = CompletionState.Completed
        }, ct);

        var status = await GetJson<ProjectStatusResponse>(StatusPath(projectId), ct);

        Assert.Equal(3, status.Issues.Total);
        Assert.Equal(1, status.Issues.Active);
        Assert.Equal(1, status.Issues.Inactive);
        Assert.Equal(1, status.Issues.Terminal);
        Assert.Equal(33, status.Issues.PercentComplete); // 1/3 = 33%
    }

    // ── Issue lists ──

    [Fact]
    public async Task GetStatus_IssueListsContainCorrectItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Bug In Progress", Description = "d", IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major, State = CompletionState.Implementing
        }, ct);

        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Waiting Issue", Description = "d", IssueType = IssueType.Defect,
            Severity = IssueSeverity.Minor, State = CompletionState.NotStarted
        }, ct);

        var status = await GetJson<ProjectStatusResponse>(StatusPath(projectId), ct);

        Assert.Single(status.Issues.ActiveItems);
        Assert.Equal("Bug In Progress", status.Issues.ActiveItems[0].Name);

        Assert.Single(status.Issues.InactiveItems);
        Assert.Equal("Waiting Issue", status.Issues.InactiveItems[0].Name);
    }

    // ── WP categorization ──

    [Fact]
    public async Task GetStatus_WpListsCategorizeByState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Active WP
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Active WP", Description = "d", State = CompletionState.Implementing
        }, ct);

        // NotStarted WP
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "NotStarted WP", Description = "d"
        }, ct);

        // Completed WP
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Done WP", Description = "d", State = CompletionState.Completed
        }, ct);

        var status = await GetJson<ProjectStatusResponse>(StatusPath(projectId), ct);

        Assert.Equal(3, status.WorkPackages.Total);
        Assert.Equal(1, status.WorkPackages.TerminalCount);
        Assert.Equal(33, status.WorkPackages.PercentComplete);

        Assert.Single(status.WorkPackages.Active);
        Assert.Equal("Active WP", status.WorkPackages.Active[0].Name);

        Assert.Single(status.WorkPackages.Inactive);
        Assert.Equal("NotStarted WP", status.WorkPackages.Inactive[0].Name);

        Assert.Empty(status.WorkPackages.Blocked);
    }

    // ── Blocked WP separation ──

    [Fact]
    public async Task GetStatus_BlockedWpsSeparatedFromInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create a blocker WP (active)
        var blockerResponse = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Blocker WP", Description = "d", State = CompletionState.Implementing
        }, ct);
        var blockerWp = await blockerResponse.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        // Create dependent WP (will auto-block)
        var dependentResponse = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Blocked WP", Description = "d", State = CompletionState.Implementing
        }, ct);
        var dependentWp = await dependentResponse.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        // Add dependency → auto-blocks the dependent
        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{dependentWp!.WorkPackageNumber}/dependencies",
            new ManageDependencyRequest { DependsOnId = blockerWp!.Id }, ct);

        // Also create a NotStarted WP
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Waiting WP", Description = "d"
        }, ct);

        var status = await GetJson<ProjectStatusResponse>(StatusPath(projectId), ct);

        // Blocked should contain only the auto-blocked WP
        Assert.Single(status.WorkPackages.Blocked);
        Assert.Equal("Blocked WP", status.WorkPackages.Blocked[0].Name);

        // Inactive should contain only NotStarted (not the blocked one)
        Assert.Single(status.WorkPackages.Inactive);
        Assert.Equal("Waiting WP", status.WorkPackages.Inactive[0].Name);

        // Active should contain only the blocker
        Assert.Single(status.WorkPackages.Active);
        Assert.Equal("Blocker WP", status.WorkPackages.Active[0].Name);
    }

    // ── PercentComplete edge cases ──

    [Fact]
    public async Task GetStatus_PercentComplete_ZeroWhenEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        var status = await GetJson<ProjectStatusResponse>(StatusPath(projectId), ct);

        Assert.Equal(0, status.Issues.PercentComplete);
        Assert.Equal(0, status.WorkPackages.PercentComplete);
    }
}
