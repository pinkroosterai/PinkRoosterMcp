using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class StateTransitionTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    // ── CompletionState: StartedAt ──

    [Theory]
    [InlineData(CompletionState.Designing)]
    [InlineData(CompletionState.Implementing)]
    [InlineData(CompletionState.Testing)]
    [InlineData(CompletionState.InReview)]
    public async Task Issue_TransitionToActive_SetsStartedAt(CompletionState activeState)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        // Create in active state directly
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct,
            name: $"Active-{activeState}", state: activeState);

        Assert.NotNull(issue.StartedAt);
    }

    [Fact]
    public async Task Issue_TransitionToActive_PreservesStartedAtOnSubsequentTransitions()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        // Create as Implementing → sets StartedAt
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct, state: CompletionState.Implementing);
        var originalStartedAt = issue.StartedAt;
        Assert.NotNull(originalStartedAt);

        // Transition to Testing → StartedAt should stay the same (within DB precision)
        var response = await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { State = CompletionState.Testing }, ct);
        var updated = await ReadJson<IssueResponse>(response, ct);

        Assert.NotNull(updated.StartedAt);
        // PostgreSQL timestamp precision may differ from .NET ticks — compare within 1ms
        Assert.True(Math.Abs((originalStartedAt!.Value - updated.StartedAt!.Value).TotalMilliseconds) < 1);
    }

    // ── CompletionState: CompletedAt and ResolvedAt ──

    [Fact]
    public async Task Issue_TransitionToCompleted_SetsCompletedAtAndResolvedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct, state: CompletionState.Implementing);

        var response = await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { State = CompletionState.Completed }, ct);
        var completed = await ReadJson<IssueResponse>(response, ct);

        Assert.NotNull(completed.CompletedAt);
        Assert.NotNull(completed.ResolvedAt);
    }

    [Theory]
    [InlineData(CompletionState.Cancelled)]
    [InlineData(CompletionState.Replaced)]
    public async Task Issue_TransitionToTerminal_SetsResolvedAtButNotCompletedAt(CompletionState terminalState)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct, state: CompletionState.Implementing);

        var response = await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { State = terminalState }, ct);
        var updated = await ReadJson<IssueResponse>(response, ct);

        Assert.Null(updated.CompletedAt); // Only set for Completed, not other terminals
        Assert.NotNull(updated.ResolvedAt);
    }

    [Fact]
    public async Task Issue_TransitionFromTerminalToActive_ClearsTimestamps()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct, state: CompletionState.Completed);
        Assert.NotNull(issue.CompletedAt);

        // Move back to active
        var response = await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { State = CompletionState.Implementing }, ct);
        var reopened = await ReadJson<IssueResponse>(response, ct);

        Assert.Null(reopened.CompletedAt);
        Assert.Null(reopened.ResolvedAt);
        Assert.NotNull(reopened.StartedAt); // Preserved
    }

    // ── CompletionState: Same-state is no-op ──

    [Fact]
    public async Task Issue_SameStateTransition_DoesNotChangeTimestamps()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct, state: CompletionState.Implementing);
        var originalStartedAt = issue.StartedAt;

        // Patch with same state
        var response = await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { State = CompletionState.Implementing }, ct);
        var same = await ReadJson<IssueResponse>(response, ct);

        Assert.NotNull(same.StartedAt);
        Assert.True(Math.Abs((originalStartedAt!.Value - same.StartedAt!.Value).TotalMilliseconds) < 1);
    }

    // ── FeatureStatus transitions ──

    [Theory]
    [InlineData(FeatureStatus.UnderReview)]
    [InlineData(FeatureStatus.Approved)]
    [InlineData(FeatureStatus.Scheduled)]
    [InlineData(FeatureStatus.InProgress)]
    public async Task FeatureRequest_TransitionToActive_SetsStartedAt(FeatureStatus activeStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var fr = await TestHelpers.CreateFeatureRequestAsync(Client, projectId, ct,
            name: $"FR-{activeStatus}", status: activeStatus);

        Assert.NotNull(fr.StartedAt);
    }

    [Fact]
    public async Task FeatureRequest_TransitionToCompleted_SetsCompletedAndResolved()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var fr = await TestHelpers.CreateFeatureRequestAsync(Client, projectId, ct, status: FeatureStatus.InProgress);

        var response = await Client.PatchAsJsonAsync(
            $"{TestHelpers.FrPath(projectId)}/{fr.FeatureRequestNumber}",
            new UpdateFeatureRequestRequest { Status = FeatureStatus.Completed }, ct);
        var completed = await ReadJson<FeatureRequestResponse>(response, ct);

        Assert.NotNull(completed.CompletedAt);
        Assert.NotNull(completed.ResolvedAt);
    }

    [Fact]
    public async Task FeatureRequest_TransitionToRejected_SetsResolvedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var fr = await TestHelpers.CreateFeatureRequestAsync(Client, projectId, ct, status: FeatureStatus.InProgress);

        var response = await Client.PatchAsJsonAsync(
            $"{TestHelpers.FrPath(projectId)}/{fr.FeatureRequestNumber}",
            new UpdateFeatureRequestRequest { Status = FeatureStatus.Rejected }, ct);
        var updated = await ReadJson<FeatureRequestResponse>(response, ct);

        Assert.NotNull(updated.ResolvedAt);
    }

    [Fact]
    public async Task FeatureRequest_TransitionToDeferred_DoesNotSetResolvedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var fr = await TestHelpers.CreateFeatureRequestAsync(Client, projectId, ct, status: FeatureStatus.InProgress);

        var response = await Client.PatchAsJsonAsync(
            $"{TestHelpers.FrPath(projectId)}/{fr.FeatureRequestNumber}",
            new UpdateFeatureRequestRequest { Status = FeatureStatus.Deferred }, ct);
        var updated = await ReadJson<FeatureRequestResponse>(response, ct);

        // Deferred is Inactive, not Terminal — no ResolvedAt
        Assert.Null(updated.ResolvedAt);
    }

    // ── Blocked state logic (WP) ──

    [Fact]
    public async Task WorkPackage_BlockedState_CapturesPreviousActiveState()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        // Create active WP
        var wp = await TestHelpers.CreateWorkPackageAsync(Client, projectId, ct,
            state: CompletionState.Implementing);

        // Create blocker WP
        var blocker = await TestHelpers.CreateWorkPackageAsync(Client, projectId, ct,
            name: "Blocker", state: CompletionState.Implementing);

        // Add dependency → auto-blocks the WP
        await Client.PostAsJsonAsync(
            $"{TestHelpers.WpPath(projectId)}/{wp.WorkPackageNumber}/dependencies",
            new ManageDependencyRequest { DependsOnId = blocker.Id }, ct);

        // Read back the blocked WP
        var blocked = await GetJson<WorkPackageResponse>(
            $"{TestHelpers.WpPath(projectId)}/{wp.WorkPackageNumber}", ct);

        Assert.Equal("Blocked", blocked.State);
        Assert.Equal("Implementing", blocked.PreviousActiveState);
    }
}
