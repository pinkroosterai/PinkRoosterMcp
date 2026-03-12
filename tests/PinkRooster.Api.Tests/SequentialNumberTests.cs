using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class SequentialNumberTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "SeqNumTest",
            Description = "Test",
            ProjectPath = $"/tmp/seqnum-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return project!.Id;
    }

    // ── Issue Number Stability ──

    [Fact]
    public async Task IssueNumber_NeverReused_AfterDeletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var issuePath = $"{BasePath}/{projectId}/issues";

        // Create issue #1
        var r1 = await Client.PostAsJsonAsync(issuePath, new CreateIssueRequest
        {
            Name = "Issue 1", Description = "d", IssueType = IssueType.Bug, Severity = IssueSeverity.Major
        }, ct);
        var issue1 = await ReadJson<IssueResponse>(r1, ct);
        Assert.Equal(1, issue1.IssueNumber);

        // Create issue #2
        var r2 = await Client.PostAsJsonAsync(issuePath, new CreateIssueRequest
        {
            Name = "Issue 2", Description = "d", IssueType = IssueType.Bug, Severity = IssueSeverity.Minor
        }, ct);
        var issue2 = await ReadJson<IssueResponse>(r2, ct);
        Assert.Equal(2, issue2.IssueNumber);

        // Delete issue #2 (the highest)
        var del = await Client.DeleteAsync($"{issuePath}/{issue2.IssueNumber}", ct);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Create issue #3 — must NOT reuse number 2
        var r3 = await Client.PostAsJsonAsync(issuePath, new CreateIssueRequest
        {
            Name = "Issue 3", Description = "d", IssueType = IssueType.Bug, Severity = IssueSeverity.Trivial
        }, ct);
        var issue3 = await ReadJson<IssueResponse>(r3, ct);
        Assert.Equal(3, issue3.IssueNumber);
    }

    // ── Feature Request Number Stability ──

    [Fact]
    public async Task FrNumber_NeverReused_AfterDeletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var frPath = $"{BasePath}/{projectId}/feature-requests";

        var r1 = await Client.PostAsJsonAsync(frPath, new CreateFeatureRequestRequest
        {
            Name = "FR 1", Description = "d", Category = FeatureCategory.Feature, Priority = Priority.Medium
        }, ct);
        var fr1 = await ReadJson<FeatureRequestResponse>(r1, ct);
        Assert.Equal(1, fr1.FeatureRequestNumber);

        var r2 = await Client.PostAsJsonAsync(frPath, new CreateFeatureRequestRequest
        {
            Name = "FR 2", Description = "d", Category = FeatureCategory.Enhancement, Priority = Priority.Low
        }, ct);
        var fr2 = await ReadJson<FeatureRequestResponse>(r2, ct);
        Assert.Equal(2, fr2.FeatureRequestNumber);

        // Delete FR #2
        var del = await Client.DeleteAsync($"{frPath}/{fr2.FeatureRequestNumber}", ct);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Create FR #3 — must NOT reuse number 2
        var r3 = await Client.PostAsJsonAsync(frPath, new CreateFeatureRequestRequest
        {
            Name = "FR 3", Description = "d", Category = FeatureCategory.Improvement, Priority = Priority.High
        }, ct);
        var fr3 = await ReadJson<FeatureRequestResponse>(r3, ct);
        Assert.Equal(3, fr3.FeatureRequestNumber);
    }

    // ── Work Package Number Stability ──

    [Fact]
    public async Task WpNumber_NeverReused_AfterDeletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var wpPath = $"{BasePath}/{projectId}/work-packages";

        var r1 = await Client.PostAsJsonAsync(wpPath, new CreateWorkPackageRequest
        {
            Name = "WP 1", Description = "d"
        }, ct);
        var wp1 = await ReadJson<WorkPackageResponse>(r1, ct);
        Assert.Equal(1, wp1.WorkPackageNumber);

        var r2 = await Client.PostAsJsonAsync(wpPath, new CreateWorkPackageRequest
        {
            Name = "WP 2", Description = "d"
        }, ct);
        var wp2 = await ReadJson<WorkPackageResponse>(r2, ct);
        Assert.Equal(2, wp2.WorkPackageNumber);

        // Delete WP #2
        var del = await Client.DeleteAsync($"{wpPath}/{wp2.WorkPackageNumber}", ct);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Create WP #3 — must NOT reuse number 2
        var r3 = await Client.PostAsJsonAsync(wpPath, new CreateWorkPackageRequest
        {
            Name = "WP 3", Description = "d"
        }, ct);
        var wp3 = await ReadJson<WorkPackageResponse>(r3, ct);
        Assert.Equal(3, wp3.WorkPackageNumber);
    }

    // ── Phase Number Stability ──

    [Fact]
    public async Task PhaseNumber_NeverReused_AfterDeletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var wpPath = $"{BasePath}/{projectId}/work-packages";

        // Create WP
        var wpR = await Client.PostAsJsonAsync(wpPath, new CreateWorkPackageRequest
        {
            Name = "WP", Description = "d"
        }, ct);
        var wp = await ReadJson<WorkPackageResponse>(wpR, ct);

        // Create phase #1
        var p1R = await Client.PostAsJsonAsync($"{wpPath}/{wp.WorkPackageNumber}/phases", new CreatePhaseRequest
        {
            Name = "Phase 1"
        }, ct);
        var p1 = await ReadJson<PhaseResponse>(p1R, ct);
        Assert.Equal(1, p1.PhaseNumber);

        // Create phase #2
        var p2R = await Client.PostAsJsonAsync($"{wpPath}/{wp.WorkPackageNumber}/phases", new CreatePhaseRequest
        {
            Name = "Phase 2"
        }, ct);
        var p2 = await ReadJson<PhaseResponse>(p2R, ct);
        Assert.Equal(2, p2.PhaseNumber);

        // Delete phase #2
        var del = await Client.DeleteAsync($"{wpPath}/{wp.WorkPackageNumber}/phases/{p2.PhaseNumber}", ct);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Create phase #3 — must NOT reuse number 2
        var p3R = await Client.PostAsJsonAsync($"{wpPath}/{wp.WorkPackageNumber}/phases", new CreatePhaseRequest
        {
            Name = "Phase 3"
        }, ct);
        var p3 = await ReadJson<PhaseResponse>(p3R, ct);
        Assert.Equal(3, p3.PhaseNumber);
    }

    // ── Task Number Stability ──

    [Fact]
    public async Task TaskNumber_NeverReused_AfterDeletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var wpPath = $"{BasePath}/{projectId}/work-packages";

        // Create WP with a phase
        var wpR = await Client.PostAsJsonAsync(wpPath, new CreateWorkPackageRequest
        {
            Name = "WP", Description = "d"
        }, ct);
        var wp = await ReadJson<WorkPackageResponse>(wpR, ct);

        var phR = await Client.PostAsJsonAsync($"{wpPath}/{wp.WorkPackageNumber}/phases", new CreatePhaseRequest
        {
            Name = "Phase 1"
        }, ct);
        var phase = await ReadJson<PhaseResponse>(phR, ct);

        var taskPath = $"{wpPath}/{wp.WorkPackageNumber}/tasks";

        // Create task #1
        var t1R = await Client.PostAsJsonAsync(
            $"{taskPath}?phaseNumber={phase.PhaseNumber}",
            new CreateTaskRequest { Name = "Task 1", Description = "d" }, ct);
        var t1 = await ReadJson<TaskResponse>(t1R, ct);
        Assert.Equal(1, t1.TaskNumber);

        // Create task #2
        var t2R = await Client.PostAsJsonAsync(
            $"{taskPath}?phaseNumber={phase.PhaseNumber}",
            new CreateTaskRequest { Name = "Task 2", Description = "d" }, ct);
        var t2 = await ReadJson<TaskResponse>(t2R, ct);
        Assert.Equal(2, t2.TaskNumber);

        // Delete task #2
        var del = await Client.DeleteAsync($"{taskPath}/{t2.TaskNumber}", ct);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Create task #3 — must NOT reuse number 2
        var t3R = await Client.PostAsJsonAsync(
            $"{taskPath}?phaseNumber={phase.PhaseNumber}",
            new CreateTaskRequest { Name = "Task 3", Description = "d" }, ct);
        var t3 = await ReadJson<TaskResponse>(t3R, ct);
        Assert.Equal(3, t3.TaskNumber);
    }
}
