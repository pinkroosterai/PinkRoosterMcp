using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class PaginationEdgeCaseTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string ActivityLogPath = "/api/activity-logs";

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    public async Task GetAll_NegativeOrZeroPage_ClampedToPage1(int page)
    {
        var ct = TestContext.Current.CancellationToken;

        // Generate at least one log entry
        await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.GetAsync($"{ActivityLogPath}?page={page}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadJson<PaginatedResponse<ActivityLogResponse>>(response, ct);
        Assert.Equal(1, result.Page);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    public async Task GetAll_NegativeOrZeroPageSize_ClampedToMin1(int pageSize)
    {
        var ct = TestContext.Current.CancellationToken;

        await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.GetAsync($"{ActivityLogPath}?pageSize={pageSize}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadJson<PaginatedResponse<ActivityLogResponse>>(response, ct);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task GetAll_PageSizeExceedsMax_ClampedTo200()
    {
        var ct = TestContext.Current.CancellationToken;

        await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.GetAsync($"{ActivityLogPath}?pageSize=500", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadJson<PaginatedResponse<ActivityLogResponse>>(response, ct);
        Assert.Equal(200, result.PageSize);
    }

    [Fact]
    public async Task GetAll_VeryLargePageNumber_ReturnsEmptyItems()
    {
        var ct = TestContext.Current.CancellationToken;

        await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.GetAsync($"{ActivityLogPath}?page=999999", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadJson<PaginatedResponse<ActivityLogResponse>>(response, ct);
        Assert.Equal(999999, result.Page);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAll_DefaultValues_ReturnsSensibleDefaults()
    {
        var ct = TestContext.Current.CancellationToken;

        await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.GetAsync(ActivityLogPath, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadJson<PaginatedResponse<ActivityLogResponse>>(response, ct);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
    }

    [Fact]
    public async Task GetAll_BothParamsInvalid_ClampedCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;

        await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.GetAsync($"{ActivityLogPath}?page=-5&pageSize=0", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadJson<PaginatedResponse<ActivityLogResponse>>(response, ct);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }
}
