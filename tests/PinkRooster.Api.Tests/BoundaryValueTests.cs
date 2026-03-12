using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class BoundaryValueTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    // ── Issue Boundaries ──

    [Fact]
    public async Task Issue_LongName_AcceptedAndStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var longName = new string('A', 200);

        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct, name: longName);

        Assert.Equal(longName, issue.Name);
    }

    [Fact]
    public async Task Issue_ExcessivelyLongName_Returns500()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var tooLong = new string('A', 1000);

        var request = new CreateIssueRequest
        {
            Name = tooLong,
            Description = "Test",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Minor
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), request, ct);

        // API rejects or DB constraint prevents excessively long names
        Assert.False(r.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Issue_EmptyDescription_Accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var request = new CreateIssueRequest
        {
            Name = "Empty desc issue",
            Description = "",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Minor
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task Issue_MinimalFields_Created()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var request = new CreateIssueRequest
        {
            Name = "Minimal",
            Description = "d",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Minor
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var issue = await ReadJson<IssueResponse>(r, ct);
        Assert.Equal(1, issue.IssueNumber);
        Assert.Equal("NotStarted", issue.State);
    }

    [Fact]
    public async Task Issue_MaximalFields_Created()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var request = new CreateIssueRequest
        {
            Name = "Maximal issue",
            Description = "Full description with details",
            IssueType = IssueType.SecurityVulnerability,
            Severity = IssueSeverity.Critical,
            Priority = Priority.Critical,
            State = CompletionState.Implementing,
            AffectedComponent = "Auth module",
            StepsToReproduce = "1. Login\n2. Navigate to /admin\n3. Observe error",
            Attachments =
            [
                new FileReferenceDto
                {
                    FileName = "screenshot.png",
                    RelativePath = "docs/screenshot.png",
                    Description = "Error screenshot"
                }
            ]
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var issue = await ReadJson<IssueResponse>(r, ct);
        Assert.Equal("Auth module", issue.AffectedComponent);
        Assert.NotNull(issue.StartedAt); // Implementing sets StartedAt
    }

    // ── Work Package Boundaries ──

    [Fact]
    public async Task WorkPackage_ZeroComplexity_Accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var request = new CreateWorkPackageRequest
        {
            Name = "Zero complexity",
            Description = "Simple task",
            EstimatedComplexity = 0
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.WpPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var wp = await ReadJson<WorkPackageResponse>(r, ct);
        Assert.Equal(0, wp.EstimatedComplexity);
    }

    [Fact]
    public async Task WorkPackage_HighComplexity_Accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var request = new CreateWorkPackageRequest
        {
            Name = "Complex WP",
            Description = "Very complex",
            EstimatedComplexity = 100
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.WpPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var wp = await ReadJson<WorkPackageResponse>(r, ct);
        Assert.Equal(100, wp.EstimatedComplexity);
    }

    [Fact]
    public async Task WorkPackage_MinimalFields_UsesDefaults()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var request = new CreateWorkPackageRequest
        {
            Name = "Minimal WP",
            Description = "Just basics"
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.WpPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var wp = await ReadJson<WorkPackageResponse>(r, ct);
        Assert.Equal("NotStarted", wp.State);
        Assert.Null(wp.EstimatedComplexity);
    }

    // ── Feature Request Boundaries ──

    [Fact]
    public async Task FeatureRequest_VeryLongDescription_Accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var longDesc = new string('B', 5000);
        var request = new CreateFeatureRequestRequest
        {
            Name = "Long desc FR",
            Description = longDesc,
            Category = FeatureCategory.Feature
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.FrPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var fr = await ReadJson<FeatureRequestResponse>(r, ct);
        Assert.Equal(longDesc, fr.Description);
    }

    [Fact]
    public async Task FeatureRequest_MinimalFields_Created()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var request = new CreateFeatureRequestRequest
        {
            Name = "Minimal FR",
            Description = "d",
            Category = FeatureCategory.Improvement
        };
        var r = await Client.PostAsJsonAsync(TestHelpers.FrPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var fr = await ReadJson<FeatureRequestResponse>(r, ct);
        Assert.Equal("Proposed", fr.Status);
        Assert.Equal(1, fr.FeatureRequestNumber);
    }

    // ── Project Boundaries ──

    [Fact]
    public async Task Project_SingleCharName_Accepted()
    {
        var ct = TestContext.Current.CancellationToken;

        var r = await Client.PutAsJsonAsync("/api/projects", new CreateOrUpdateProjectRequest
        {
            Name = "X",
            Description = "",
            ProjectPath = $"/tmp/single-char-{Guid.NewGuid():N}"
        }, ct);

        Assert.True(r.IsSuccessStatusCode);
        var project = await ReadJson<ProjectResponse>(r, ct);
        Assert.Equal("X", project.Name);
    }

    // ── Memory Boundaries ──

    [Fact]
    public async Task Memory_EmptyTags_Accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var r = await Client.PostAsJsonAsync($"/api/projects/{projectId}/memories",
            new UpsertProjectMemoryRequest
            {
                Name = "no-tags",
                Content = "Content without tags.",
                Tags = []
            }, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var memory = await ReadJson<ProjectMemoryResponse>(r, ct);
        Assert.Empty(memory.Tags);
    }

    [Fact]
    public async Task Memory_SingleCharContent_Accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var r = await Client.PostAsJsonAsync($"/api/projects/{projectId}/memories",
            new UpsertProjectMemoryRequest
            {
                Name = "tiny",
                Content = "x"
            }, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var memory = await ReadJson<ProjectMemoryResponse>(r, ct);
        Assert.Equal("x", memory.Content);
    }
}
