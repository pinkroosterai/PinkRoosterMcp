using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class EnumCoverageTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    // ── IssueType (6 values) ──

    [Theory]
    [InlineData(IssueType.Bug)]
    [InlineData(IssueType.Defect)]
    [InlineData(IssueType.Regression)]
    [InlineData(IssueType.TechnicalDebt)]
    [InlineData(IssueType.PerformanceIssue)]
    [InlineData(IssueType.SecurityVulnerability)]
    public async Task Issue_AcceptsAllIssueTypes(IssueType issueType)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct,
            name: $"Issue-{issueType}", type: issueType);

        Assert.Equal(issueType.ToString(), issue.IssueType);
    }

    // ── IssueSeverity (4 values) ──

    [Theory]
    [InlineData(IssueSeverity.Critical)]
    [InlineData(IssueSeverity.Major)]
    [InlineData(IssueSeverity.Minor)]
    [InlineData(IssueSeverity.Trivial)]
    public async Task Issue_AcceptsAllSeverities(IssueSeverity severity)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct,
            name: $"Issue-{severity}", severity: severity);

        Assert.Equal(severity.ToString(), issue.Severity);
    }

    // ── Priority (4 values) — tested on Issues ──

    [Theory]
    [InlineData(Priority.Critical)]
    [InlineData(Priority.High)]
    [InlineData(Priority.Medium)]
    [InlineData(Priority.Low)]
    public async Task Issue_AcceptsAllPriorities(Priority priority)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), new CreateIssueRequest
        {
            Name = $"Issue-{priority}",
            Description = "d",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major,
            Priority = priority
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var issue = await ReadJson<IssueResponse>(response, ct);
        Assert.Equal(priority.ToString(), issue.Priority);
    }

    // ── WorkPackageType (5 values) ──

    [Theory]
    [InlineData(WorkPackageType.Feature)]
    [InlineData(WorkPackageType.BugFix)]
    [InlineData(WorkPackageType.Refactor)]
    [InlineData(WorkPackageType.Spike)]
    [InlineData(WorkPackageType.Chore)]
    public async Task WorkPackage_AcceptsAllTypes(WorkPackageType wpType)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.PostAsJsonAsync(TestHelpers.WpPath(projectId), new CreateWorkPackageRequest
        {
            Name = $"WP-{wpType}",
            Description = "d",
            Type = wpType
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var wp = await ReadJson<WorkPackageResponse>(response, ct);
        Assert.Equal(wpType.ToString(), wp.Type);
    }

    // ── FeatureCategory (3 values) ──

    [Theory]
    [InlineData(FeatureCategory.Feature)]
    [InlineData(FeatureCategory.Enhancement)]
    [InlineData(FeatureCategory.Improvement)]
    public async Task FeatureRequest_AcceptsAllCategories(FeatureCategory category)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var fr = await TestHelpers.CreateFeatureRequestAsync(Client, projectId, ct,
            name: $"FR-{category}", category: category);

        Assert.Equal(category.ToString(), fr.Category);
    }

    // ── FeatureStatus (8 values) ──

    [Theory]
    [InlineData(FeatureStatus.Proposed)]
    [InlineData(FeatureStatus.UnderReview)]
    [InlineData(FeatureStatus.Approved)]
    [InlineData(FeatureStatus.Scheduled)]
    [InlineData(FeatureStatus.InProgress)]
    [InlineData(FeatureStatus.Completed)]
    [InlineData(FeatureStatus.Rejected)]
    [InlineData(FeatureStatus.Deferred)]
    public async Task FeatureRequest_AcceptsAllStatuses(FeatureStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var fr = await TestHelpers.CreateFeatureRequestAsync(Client, projectId, ct,
            name: $"FR-{status}", status: status);

        Assert.Equal(status.ToString(), fr.Status);
    }

    // ── CompletionState (9 values) — tested on Issues ──

    [Theory]
    [InlineData(CompletionState.NotStarted)]
    [InlineData(CompletionState.Designing)]
    [InlineData(CompletionState.Implementing)]
    [InlineData(CompletionState.Testing)]
    [InlineData(CompletionState.InReview)]
    [InlineData(CompletionState.Completed)]
    [InlineData(CompletionState.Cancelled)]
    [InlineData(CompletionState.Blocked)]
    [InlineData(CompletionState.Replaced)]
    public async Task Issue_AcceptsAllCompletionStates(CompletionState state)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct,
            name: $"Issue-{state}", state: state);

        Assert.Equal(state.ToString(), issue.State);
    }
}
