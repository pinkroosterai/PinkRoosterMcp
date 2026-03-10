using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class IssueEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/issue-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);
        return project!.Id;
    }

    private string IssuePath(long projectId) => $"{BasePath}/{projectId}/issues";
    private string WpPath(long projectId) => $"{BasePath}/{projectId}/work-packages";

    private static CreateIssueRequest MakeIssueRequest(string name = "Test Issue") => new()
    {
        Name = name,
        Description = "Test issue description",
        IssueType = IssueType.Bug,
        Severity = IssueSeverity.Major
    };

    // ── Linked Work Packages ──

    [Fact]
    public async Task GetIssue_ReturnsLinkedWorkPackages()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create issue
        var issueResponse = await Client.PostAsJsonAsync(IssuePath(projectId), MakeIssueRequest(), ct);
        var issue = await issueResponse.Content.ReadFromJsonAsync<IssueResponse>(ct);

        // Create WP linked to this issue
        var wpResponse = await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "Fix WP",
            Description = "Fixes the issue",
            LinkedIssueId = issue!.Id
        }, ct);
        Assert.Equal(HttpStatusCode.Created, wpResponse.StatusCode);

        // Fetch issue detail — should include linked WP
        var detail = await GetJson<IssueResponse>($"{IssuePath(projectId)}/{issue.IssueNumber}", ct);

        Assert.NotNull(detail);
        Assert.Single(detail.LinkedWorkPackages);
        Assert.Equal("Fix WP", detail.LinkedWorkPackages[0].Name);
        Assert.Equal("NotStarted", detail.LinkedWorkPackages[0].State);
        Assert.Equal("Feature", detail.LinkedWorkPackages[0].Type);
    }

    [Fact]
    public async Task GetIssueList_ReturnsLinkedWorkPackages()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create issue
        var issueResponse = await Client.PostAsJsonAsync(IssuePath(projectId), MakeIssueRequest(), ct);
        var issue = await issueResponse.Content.ReadFromJsonAsync<IssueResponse>(ct);

        // Create two WPs linked to this issue
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "WP 1", Description = "First", LinkedIssueId = issue!.Id
        }, ct);
        await Client.PostAsJsonAsync(WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = "WP 2", Description = "Second", LinkedIssueId = issue.Id
        }, ct);

        // Fetch issue list
        var issues = await GetJson<List<IssueResponse>>(IssuePath(projectId), ct);

        Assert.Single(issues);
        Assert.Equal(2, issues[0].LinkedWorkPackages.Count);
    }

    [Fact]
    public async Task GetIssue_ReturnsEmptyLinkedWorkPackages_WhenNoneLinked()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create issue with no linked WPs
        var issueResponse = await Client.PostAsJsonAsync(IssuePath(projectId), MakeIssueRequest(), ct);
        var issue = await issueResponse.Content.ReadFromJsonAsync<IssueResponse>(ct);

        var detail = await GetJson<IssueResponse>($"{IssuePath(projectId)}/{issue!.IssueNumber}", ct);

        Assert.NotNull(detail);
        Assert.Empty(detail.LinkedWorkPackages);
    }
}
