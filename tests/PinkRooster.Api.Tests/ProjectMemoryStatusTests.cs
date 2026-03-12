using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class ProjectMemoryStatusTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "StatusMemTest",
            Description = "Test",
            ProjectPath = $"/tmp/status-mem-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return project!.Id;
    }

    [Fact]
    public async Task ProjectStatus_NoMemories_NullSummary()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var status = await GetJson<ProjectStatusResponse>($"{BasePath}/{projectId}/status", ct);
        Assert.Null(status.Memories);
    }

    [Fact]
    public async Task ProjectStatus_WithMemories_IncludesSummary()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var memPath = $"{BasePath}/{projectId}/memories";

        await Client.PostAsJsonAsync(memPath, new UpsertProjectMemoryRequest
        {
            Name = "arch", Content = "Architecture decisions.", Tags = ["design", "api"]
        }, ct);
        await Client.PostAsJsonAsync(memPath, new UpsertProjectMemoryRequest
        {
            Name = "patterns", Content = "Code patterns.", Tags = ["code", "api"]
        }, ct);

        var status = await GetJson<ProjectStatusResponse>($"{BasePath}/{projectId}/status", ct);
        Assert.NotNull(status.Memories);
        Assert.Equal(2, status.Memories!.Total);
        Assert.Equal(2, status.Memories.RecentMemories.Count);
        Assert.True(status.Memories.TagCloud.ContainsKey("api"));
        Assert.Equal(2, status.Memories.TagCloud["api"]);
    }

    [Fact]
    public async Task ProjectStatus_RecentMemories_LimitedToFive()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        var memPath = $"{BasePath}/{projectId}/memories";

        for (var i = 1; i <= 7; i++)
        {
            await Client.PostAsJsonAsync(memPath, new UpsertProjectMemoryRequest
            {
                Name = $"mem-{i}", Content = $"Content {i}."
            }, ct);
        }

        var status = await GetJson<ProjectStatusResponse>($"{BasePath}/{projectId}/status", ct);
        Assert.NotNull(status.Memories);
        Assert.Equal(7, status.Memories!.Total);
        Assert.Equal(5, status.Memories.RecentMemories.Count);
    }
}
