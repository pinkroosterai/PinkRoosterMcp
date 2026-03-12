using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class AuditTrailEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private string AuditPath(long projectId, int issueNumber) =>
        $"/api/projects/{projectId}/issues/{issueNumber}/audit";

    // ── Issue Audit: Create ──

    [Fact]
    public async Task GetAuditLog_ReturnsCreateEntries_WithNullOldValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        var logs = await GetJson<List<IssueAuditLogResponse>>(
            AuditPath(projectId, issue.IssueNumber), ct);

        Assert.NotEmpty(logs);

        // Create entries should have null OldValue and non-null NewValue
        var nameEntry = logs.FirstOrDefault(l => l.FieldName == "Name");
        Assert.NotNull(nameEntry);
        Assert.Null(nameEntry.OldValue);
        Assert.Equal("Test Issue", nameEntry.NewValue);
    }

    [Fact]
    public async Task GetAuditLog_CreateEntries_IncludeAllFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        var logs = await GetJson<List<IssueAuditLogResponse>>(
            AuditPath(projectId, issue.IssueNumber), ct);

        var fieldNames = logs.Select(l => l.FieldName).ToHashSet();

        // Key fields should all be logged on creation
        Assert.Contains("Name", fieldNames);
        Assert.Contains("Description", fieldNames);
        Assert.Contains("IssueType", fieldNames);
        Assert.Contains("Severity", fieldNames);
        Assert.Contains("State", fieldNames);
    }

    [Fact]
    public async Task GetAuditLog_CreateEntries_HaveChangedByAndChangedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        var logs = await GetJson<List<IssueAuditLogResponse>>(
            AuditPath(projectId, issue.IssueNumber), ct);

        foreach (var log in logs)
        {
            Assert.False(string.IsNullOrEmpty(log.ChangedBy));
            Assert.True(log.ChangedAt > DateTimeOffset.MinValue);
            Assert.True(log.ChangedAt <= DateTimeOffset.UtcNow.AddSeconds(5));
        }
    }

    // ── Issue Audit: Update ──

    [Fact]
    public async Task GetAuditLog_UpdateEntries_ShowOldAndNewValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        // Update the issue name
        await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { Name = "Updated Name" }, ct);

        var logs = await GetJson<List<IssueAuditLogResponse>>(
            AuditPath(projectId, issue.IssueNumber), ct);

        // Find the update entry for Name
        var updateEntries = logs.Where(l => l.FieldName == "Name" && l.OldValue is not null).ToList();
        Assert.NotEmpty(updateEntries);

        var nameUpdate = updateEntries[0];
        Assert.Equal("Test Issue", nameUpdate.OldValue);
        Assert.Equal("Updated Name", nameUpdate.NewValue);
    }

    [Fact]
    public async Task GetAuditLog_StateChange_RecordsBothStates()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        // Change state from NotStarted to Implementing
        await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { State = CompletionState.Implementing }, ct);

        var logs = await GetJson<List<IssueAuditLogResponse>>(
            AuditPath(projectId, issue.IssueNumber), ct);

        var stateUpdate = logs.FirstOrDefault(l => l.FieldName == "State" && l.OldValue is not null);
        Assert.NotNull(stateUpdate);
        Assert.Equal("NotStarted", stateUpdate.OldValue);
        Assert.Equal("Implementing", stateUpdate.NewValue);
    }

    [Fact]
    public async Task GetAuditLog_MultipleUpdates_ProduceMultipleEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        // Two separate updates
        await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { Name = "First Update" }, ct);

        await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { Name = "Second Update" }, ct);

        var logs = await GetJson<List<IssueAuditLogResponse>>(
            AuditPath(projectId, issue.IssueNumber), ct);

        // Should have create entry + 2 update entries for Name
        var nameEntries = logs.Where(l => l.FieldName == "Name").ToList();
        Assert.Equal(3, nameEntries.Count); // 1 create + 2 updates
    }

    // ── 404 handling ──

    [Fact]
    public async Task GetAuditLog_ReturnsEmptyList_WhenIssueNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var logs = await GetJson<List<IssueAuditLogResponse>>(AuditPath(projectId, 9999), ct);

        Assert.Empty(logs);
    }
}
