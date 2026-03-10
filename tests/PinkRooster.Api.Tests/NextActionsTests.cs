using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class NextActionsTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<(long ProjectId, string HumanId)> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "NextActionsProject",
            Description = "Test",
            ProjectPath = $"/tmp/next-actions-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);
        return (project!.Id, project.ProjectId);
    }

    private string NextActionsPath(long projectId, int limit = 10, string? entityType = null)
    {
        var path = $"{BasePath}/{projectId}/next-actions?limit={limit}";
        if (entityType is not null)
            path += $"&entityType={entityType}";
        return path;
    }

    private string WpPath(long projectId) => $"{BasePath}/{projectId}/work-packages";
    private string IssuePath(long projectId) => $"{BasePath}/{projectId}/issues";

    // ── Basic ──

    [Fact]
    public async Task GetNextActions_Returns404_WhenProjectNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync(NextActionsPath(999999), ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNextActions_ReturnsEmptyList_WhenProjectEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId), ct);
        Assert.Empty(items);
    }

    // ── Task inclusion ──

    [Fact]
    public async Task GetNextActions_IncludesActiveTasks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create WP with phase and task
        var wpResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Active WP", Description = "d", State = CompletionState.Implementing,
            Priority = Priority.High
        }, ct);
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(ct);

        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}/tasks?phaseNumber={phase!.PhaseNumber}",
            new CreateTaskRequest { Name = "Implement feature", Description = "d", State = CompletionState.Implementing }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId), ct);

        Assert.Single(items);
        Assert.Equal("Task", items[0].Type);
        Assert.Equal("Implement feature", items[0].Name);
        Assert.Equal("High", items[0].Priority);
    }

    [Fact]
    public async Task GetNextActions_IncludesNotStartedTasks_WhenWpActive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        var wpResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Active WP", Description = "d", State = CompletionState.Implementing
        }, ct);
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(ct);

        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}/tasks?phaseNumber={phase!.PhaseNumber}",
            new CreateTaskRequest { Name = "Pending task", Description = "d" }, ct); // Default: NotStarted

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId), ct);

        Assert.Single(items);
        Assert.Equal("NotStarted", items[0].State);
    }

    [Fact]
    public async Task GetNextActions_IncludesNotStartedTasks_WhenWpNotStarted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // WP is NotStarted (default)
        var wpResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "NotStarted WP", Description = "d"
        }, ct);
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(ct);

        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}/tasks?phaseNumber={phase!.PhaseNumber}",
            new CreateTaskRequest { Name = "Pending task", Description = "d" }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "task"), ct);

        Assert.Single(items);
        Assert.Equal("Pending task", items[0].Name);
        Assert.Equal("NotStarted", items[0].State);
    }

    // ── WP inclusion (leaf WPs only) ──

    [Fact]
    public async Task GetNextActions_IncludesLeafWps_ExcludesWpsWithPhases()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Leaf WP (no phases)
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Leaf WP", Description = "d", State = CompletionState.Implementing
        }, ct);

        // WP with phase (should be excluded from WP list)
        var wpResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Parent WP", Description = "d", State = CompletionState.Implementing
        }, ct);
        var parentWp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);
        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{parentWp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "wp"), ct);

        Assert.Single(items);
        Assert.Equal("Leaf WP", items[0].Name);
    }

    // ── Issue inclusion (only unlinked) ──

    [Fact]
    public async Task GetNextActions_ExcludesIssuesLinkedToWps()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create two issues
        var issueResp1 = await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Unlinked Issue", Description = "d", IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major, State = CompletionState.Implementing
        }, ct);
        var issue1 = await issueResp1.Content.ReadFromJsonAsync<IssueResponse>(ct);

        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Linked Issue", Description = "d", IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major, State = CompletionState.Implementing
        }, ct);
        var issue2Resp = await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Another Issue", Description = "d", IssueType = IssueType.Bug,
            Severity = IssueSeverity.Minor, State = CompletionState.Implementing
        }, ct);

        // Link issue2 to a WP
        var allIssues = await GetJson<List<IssueResponse>>($"{IssuePath(projectId)}", ct);
        var linkedIssue = allIssues.First(i => i.Name == "Linked Issue");

        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "WP with issue", Description = "d", LinkedIssueId = linkedIssue.Id,
            State = CompletionState.Implementing
        }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "issue"), ct);

        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, i => i.Name == "Linked Issue");
    }

    // ── Sorting ──

    [Fact]
    public async Task GetNextActions_SortsByPriorityThenEntityType()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Low priority task
        var wpResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Low WP", Description = "d", State = CompletionState.Implementing,
            Priority = Priority.Low
        }, ct);
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);
        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(ct);
        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}/tasks?phaseNumber={phase!.PhaseNumber}",
            new CreateTaskRequest { Name = "Low Task", Description = "d", State = CompletionState.Implementing }, ct);

        // Critical issue
        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Critical Issue", Description = "d", IssueType = IssueType.Bug,
            Severity = IssueSeverity.Critical, Priority = Priority.Critical,
            State = CompletionState.Implementing
        }, ct);

        // Critical leaf WP
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Critical WP", Description = "d", State = CompletionState.Implementing,
            Priority = Priority.Critical
        }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId), ct);

        Assert.Equal(3, items.Count);
        // Critical WP before Critical Issue (WP type=1 < Issue type=2)
        Assert.Equal("Critical WP", items[0].Name);
        Assert.Equal("Critical Issue", items[1].Name);
        Assert.Equal("Low Task", items[2].Name);
    }

    // ── Limit ──

    [Fact]
    public async Task GetNextActions_RespectsLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        for (var i = 0; i < 5; i++)
        {
            await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
            {
                Name = $"Issue {i}", Description = "d", IssueType = IssueType.Bug,
                Severity = IssueSeverity.Minor, State = CompletionState.Implementing
            }, ct);
        }

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, limit: 3), ct);

        Assert.Equal(3, items.Count);
    }

    // ── Entity type filter ──

    [Fact]
    public async Task GetNextActions_FiltersEntityType()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create leaf WP
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Leaf WP", Description = "d", State = CompletionState.Implementing
        }, ct);

        // Create issue
        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Bug", Description = "d", IssueType = IssueType.Bug,
            Severity = IssueSeverity.Minor, State = CompletionState.Implementing
        }, ct);

        var wpOnly = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "wp"), ct);
        Assert.Single(wpOnly);
        Assert.Equal("WorkPackage", wpOnly[0].Type);

        var issueOnly = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "issue"), ct);
        Assert.Single(issueOnly);
        Assert.Equal("Issue", issueOnly[0].Type);
    }

    // ── Blocked exclusion ──

    [Fact]
    public async Task GetNextActions_ExcludesBlockedItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create blocker WP
        var blockerResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Blocker", Description = "d", State = CompletionState.Implementing
        }, ct);
        var blocker = await blockerResp.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        // Create dependent WP that will be auto-blocked
        var depResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Blocked WP", Description = "d", State = CompletionState.Implementing
        }, ct);
        var dep = await depResp.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{dep!.WorkPackageNumber}/dependencies",
            new ManageDependencyRequest { DependsOnId = blocker!.Id }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "wp"), ct);

        // Only the blocker should appear (leaf WPs, not blocked)
        Assert.Single(items);
        Assert.Equal("Blocker", items[0].Name);
    }
}
