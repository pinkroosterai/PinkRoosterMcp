using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class ActivityLogEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string ActivityLogPath = "/api/activity-logs";

    [Fact]
    public async Task GetAll_ReturnsActivityLogs_AfterApiCalls()
    {
        var ct = TestContext.Current.CancellationToken;

        // Make some API calls that will generate activity logs
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        var result = await GetJson<PaginatedResponse<ActivityLogResponse>>(ActivityLogPath, ct);

        Assert.NotNull(result);
        Assert.True(result.Items.Count > 0, "Expected at least one activity log entry");
        Assert.True(result.TotalCount > 0);
    }

    [Fact]
    public async Task GetAll_ReturnsCorrectFields()
    {
        var ct = TestContext.Current.CancellationToken;

        // Generate a known request
        await TestHelpers.CreateProjectAsync(Client, ct);

        var result = await GetJson<PaginatedResponse<ActivityLogResponse>>(ActivityLogPath, ct);

        var entry = result.Items[0];
        Assert.False(string.IsNullOrEmpty(entry.HttpMethod));
        Assert.False(string.IsNullOrEmpty(entry.Path));
        Assert.True(entry.StatusCode > 0);
        Assert.True(entry.DurationMs >= 0);
        Assert.True(entry.Timestamp > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task GetAll_IncludesCallerIdentity()
    {
        var ct = TestContext.Current.CancellationToken;

        await TestHelpers.CreateProjectAsync(Client, ct);

        var result = await GetJson<PaginatedResponse<ActivityLogResponse>>(ActivityLogPath, ct);

        // Authenticated requests should have a caller identity
        var authenticatedEntry = result.Items.FirstOrDefault(e => e.CallerIdentity is not null);
        Assert.NotNull(authenticatedEntry);
    }

    [Fact]
    public async Task GetAll_SupportsPagination()
    {
        var ct = TestContext.Current.CancellationToken;

        // Generate multiple requests
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        await TestHelpers.CreateIssueAsync(Client, projectId, ct, "Issue 1");
        await TestHelpers.CreateIssueAsync(Client, projectId, ct, "Issue 2");
        await TestHelpers.CreateIssueAsync(Client, projectId, ct, "Issue 3");

        // Request page 1 with small page size
        var page1 = await GetJson<PaginatedResponse<ActivityLogResponse>>(
            $"{ActivityLogPath}?page=1&pageSize=2", ct);

        Assert.NotNull(page1);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.TotalCount > 2);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        // Request page 2
        var page2 = await GetJson<PaginatedResponse<ActivityLogResponse>>(
            $"{ActivityLogPath}?page=2&pageSize=2", ct);

        Assert.Equal(2, page2.Page);
        Assert.True(page2.HasPreviousPage);
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyPage_WhenNoLogs()
    {
        var ct = TestContext.Current.CancellationToken;

        // The GET request itself generates a log, so we just check structure
        var result = await GetJson<PaginatedResponse<ActivityLogResponse>>(ActivityLogPath, ct);

        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.Page >= 1);
        Assert.True(result.PageSize >= 1);
    }

    [Fact]
    public async Task GetAll_LogsRecordHttpMethods()
    {
        var ct = TestContext.Current.CancellationToken;

        // Generate PUT (project create) and POST (issue create)
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        // Also generate a GET by reading issue list
        await Client.GetAsync(TestHelpers.IssuePath(projectId), ct);

        var result = await GetJson<PaginatedResponse<ActivityLogResponse>>(
            $"{ActivityLogPath}?pageSize=200", ct);

        var methods = result.Items.Select(e => e.HttpMethod).Distinct().ToList();
        Assert.Contains("PUT", methods);
        Assert.Contains("POST", methods);
        Assert.Contains("GET", methods);
    }
}
