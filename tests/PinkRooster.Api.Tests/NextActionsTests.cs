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
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
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
    private string FrPath(long projectId) => $"{BasePath}/{projectId}/feature-requests";

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
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);

        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

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
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);

        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

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
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);

        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

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
        var parentWp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);
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
        var issue1 = await issueResp1.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, ct);

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
            Name = "WP with issue", Description = "d", LinkedIssueIds = [linkedIssue.Id],
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
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);
        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);
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
        var blocker = await blockerResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);

        // Create dependent WP that will be auto-blocked
        var depResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Blocked WP", Description = "d", State = CompletionState.Implementing
        }, ct);
        var dep = await depResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);

        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{dep!.WorkPackageNumber}/dependencies",
            new ManageDependencyRequest { DependsOnId = blocker!.Id }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "wp"), ct);

        // Only the blocker should appear (leaf WPs, not blocked)
        Assert.Single(items);
        Assert.Equal("Blocker", items[0].Name);
    }

    // ── Blocked/Terminal WP task exclusion ──

    [Fact]
    public async Task GetNextActions_ExcludesTasksFromBlockedWps()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create blocker WP
        var blockerResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Blocker", Description = "d", State = CompletionState.Implementing
        }, ct);
        var blocker = await blockerResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);

        // Create WP with phase and task
        var wpResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Dependent WP", Description = "d", State = CompletionState.Implementing
        }, ct);
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);

        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}/tasks?phaseNumber={phase!.PhaseNumber}",
            new CreateTaskRequest { Name = "Blocked task", Description = "d", State = CompletionState.Implementing }, ct);

        // Block the WP via dependency — WP auto-blocked, but task stays Implementing
        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}/dependencies",
            new ManageDependencyRequest { DependsOnId = blocker!.Id }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "task"), ct);

        // Task from blocked WP should NOT appear
        Assert.DoesNotContain(items, i => i.Name == "Blocked task");
    }

    [Fact]
    public async Task GetNextActions_ExcludesTasksFromReplacedWps()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create WP with phase and task
        var wpResp = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Old WP", Description = "d", State = CompletionState.Implementing
        }, ct);
        var wp = await wpResp.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);

        var phaseResp = await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp!.WorkPackageNumber}/phases",
            new CreatePhaseRequest { Name = "Phase 1" }, ct);
        var phase = await phaseResp.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

        await Client.PostAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}/tasks?phaseNumber={phase!.PhaseNumber}",
            new CreateTaskRequest { Name = "Orphaned task", Description = "d", State = CompletionState.Implementing }, ct);

        // Set WP to Replaced — no downward cascade to tasks
        await Client.PatchAsJsonAsync(
            $"{WpPath(projectId)}/{wp.WorkPackageNumber}",
            new UpdateWorkPackageRequest { State = CompletionState.Replaced }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "task"), ct);

        // Task from replaced WP should NOT appear
        Assert.DoesNotContain(items, i => i.Name == "Orphaned task");
    }

    // ── Issue resurfacing after terminal WP ──

    [Fact]
    public async Task GetNextActions_ResurfacesIssuesLinkedToCancelledWps()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create an issue
        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Persistent Bug", Description = "d", IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major, State = CompletionState.Implementing
        }, ct);

        // Link it to a WP
        var allIssues = await GetJson<List<IssueResponse>>($"{IssuePath(projectId)}", ct);
        var issue = allIssues.First(i => i.Name == "Persistent Bug");

        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Failed WP", Description = "d", LinkedIssueIds = [issue.Id],
            State = CompletionState.Implementing
        }, ct);

        // Cancel the WP
        await Client.PatchAsJsonAsync(
            $"{WpPath(projectId)}/1",
            new UpdateWorkPackageRequest { State = CompletionState.Cancelled }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "issue"), ct);

        // Issue should resurface — the only WP link is to a terminal WP
        Assert.Single(items);
        Assert.Equal("Persistent Bug", items[0].Name);
    }

    // ── Feature Request inclusion ──

    [Fact]
    public async Task GetNextActions_IncludesProposedFeatureRequests()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "New Feature Idea",
            Description = "A proposed feature",
            Category = FeatureCategory.Feature,
            Priority = Priority.High,
            Status = FeatureStatus.Proposed
        }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "featurerequest"), ct);

        Assert.Single(items);
        Assert.Equal("FeatureRequest", items[0].Type);
        Assert.Equal("New Feature Idea", items[0].Name);
        Assert.Equal("Proposed", items[0].State);
    }

    [Fact]
    public async Task GetNextActions_ExcludesDeferredFeatureRequests()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Proposed FR — should appear
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "Active FR",
            Description = "d",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.Proposed
        }, ct);

        // Deferred FR — should NOT appear
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "Deferred FR",
            Description = "d",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.Deferred
        }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "featurerequest"), ct);

        Assert.Single(items);
        Assert.Equal("Active FR", items[0].Name);
    }

    [Fact]
    public async Task GetNextActions_IncludesActiveFrsButExcludesInProgress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Approved FR — should appear
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "Approved FR",
            Description = "d",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.Approved
        }, ct);

        // InProgress FR — should NOT appear (WPs handle it)
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "InProgress FR",
            Description = "d",
            Category = FeatureCategory.Feature,
            Status = FeatureStatus.InProgress
        }, ct);

        var items = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "featurerequest"), ct);

        Assert.Single(items);
        Assert.Equal("Approved FR", items[0].Name);
    }

    [Fact]
    public async Task GetNextActions_EntityTypeFilter_ReturnsOnlyFrs()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, _) = await CreateProjectAsync(ct);

        // Create an issue
        await Client.PostAsJsonAsync(IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Bug",
            Description = "d",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Minor,
            State = CompletionState.Implementing
        }, ct);

        // Create a FR
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "Feature Idea",
            Description = "d",
            Category = FeatureCategory.Enhancement,
            Status = FeatureStatus.Proposed
        }, ct);

        var frOnly = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "featurerequest"), ct);
        Assert.Single(frOnly);
        Assert.Equal("FeatureRequest", frOnly[0].Type);

        var issueOnly = await GetJson<List<NextActionItem>>(NextActionsPath(projectId, entityType: "issue"), ct);
        Assert.Single(issueOnly);
        Assert.Equal("Issue", issueOnly[0].Type);
    }
}
