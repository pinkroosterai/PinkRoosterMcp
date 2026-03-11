using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class FeatureRequestUserStoryTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/us-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return project!.Id;
    }

    private string FrPath(long projectId) => $"{BasePath}/{projectId}/feature-requests";
    private string ManagePath(long projectId, int frNumber) =>
        $"{FrPath(projectId)}/{frNumber}/user-stories/manage";

    // ── Create with user stories ──

    [Fact]
    public async Task Post_WithUserStories_PersistsStories()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = new CreateFeatureRequestRequest
        {
            Name = "FR with stories",
            Description = "Test",
            Category = FeatureCategory.Feature,
            UserStories =
            [
                new() { Role = "developer", Goal = "write tests faster", Benefit = "better coverage" },
                new() { Role = "manager", Goal = "track progress", Benefit = "visibility" }
            ]
        };

        var response = await Client.PostAsJsonAsync(FrPath(projectId), request, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);

        Assert.Equal(2, fr!.UserStories.Count);
        Assert.Equal("developer", fr.UserStories[0].Role);
        Assert.Equal("write tests faster", fr.UserStories[0].Goal);
        Assert.Equal("better coverage", fr.UserStories[0].Benefit);
        Assert.Equal("manager", fr.UserStories[1].Role);
    }

    [Fact]
    public async Task Post_WithoutUserStories_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = new CreateFeatureRequestRequest
        {
            Name = "FR no stories",
            Description = "Test",
            Category = FeatureCategory.Feature
        };

        var response = await Client.PostAsJsonAsync(FrPath(projectId), request, ct);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);

        Assert.NotNull(fr!.UserStories);
        Assert.Empty(fr.UserStories);
    }

    // ── Manage: Add ──

    [Fact]
    public async Task ManageAdd_AppendsStory()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature
        }, ct);

        var response = await Client.PostAsJsonAsync(ManagePath(projectId, 1),
            new ManageUserStoriesRequest { Action = "Add", Role = "tester", Goal = "run tests", Benefit = "quality" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);
        Assert.Single(fr!.UserStories);
        Assert.Equal("tester", fr.UserStories[0].Role);
    }

    [Fact]
    public async Task ManageAdd_MissingFields_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature
        }, ct);

        var response = await Client.PostAsJsonAsync(ManagePath(projectId, 1),
            new ManageUserStoriesRequest { Action = "Add", Role = "tester" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Manage: Update ──

    [Fact]
    public async Task ManageUpdate_ReplacesStoryAtIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature,
            UserStories = [new() { Role = "old", Goal = "old goal", Benefit = "old benefit" }]
        }, ct);

        var response = await Client.PostAsJsonAsync(ManagePath(projectId, 1),
            new ManageUserStoriesRequest { Action = "Update", Index = 0, Role = "new", Goal = "new goal", Benefit = "new benefit" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);
        Assert.Single(fr!.UserStories);
        Assert.Equal("new", fr.UserStories[0].Role);
        Assert.Equal("new goal", fr.UserStories[0].Goal);
    }

    [Fact]
    public async Task ManageUpdate_InvalidIndex_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature
        }, ct);

        var response = await Client.PostAsJsonAsync(ManagePath(projectId, 1),
            new ManageUserStoriesRequest { Action = "Update", Index = 5, Role = "x", Goal = "x", Benefit = "x" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Manage: Remove ──

    [Fact]
    public async Task ManageRemove_DeletesStoryAtIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature,
            UserStories =
            [
                new() { Role = "first", Goal = "g1", Benefit = "b1" },
                new() { Role = "second", Goal = "g2", Benefit = "b2" }
            ]
        }, ct);

        var response = await Client.PostAsJsonAsync(ManagePath(projectId, 1),
            new ManageUserStoriesRequest { Action = "Remove", Index = 0 }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fr = await response.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);
        Assert.Single(fr!.UserStories);
        Assert.Equal("second", fr.UserStories[0].Role);
    }

    [Fact]
    public async Task ManageRemove_InvalidIndex_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature
        }, ct);

        var response = await Client.PostAsJsonAsync(ManagePath(projectId, 1),
            new ManageUserStoriesRequest { Action = "Remove", Index = 0 }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Not found ──

    [Fact]
    public async Task Manage_Returns404_WhenFrNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var response = await Client.PostAsJsonAsync(ManagePath(projectId, 999),
            new ManageUserStoriesRequest { Action = "Add", Role = "x", Goal = "x", Benefit = "x" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Round-trip persistence ──

    [Fact]
    public async Task UserStories_PersistAcrossReads()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);
        await Client.PostAsJsonAsync(FrPath(projectId), new CreateFeatureRequestRequest
        {
            Name = "FR",
            Description = "Test",
            Category = FeatureCategory.Feature
        }, ct);

        // Add two stories
        await Client.PostAsJsonAsync(ManagePath(projectId, 1),
            new ManageUserStoriesRequest { Action = "Add", Role = "a", Goal = "ga", Benefit = "ba" }, ct);
        await Client.PostAsJsonAsync(ManagePath(projectId, 1),
            new ManageUserStoriesRequest { Action = "Add", Role = "b", Goal = "gb", Benefit = "bb" }, ct);

        // Read back
        var fr = await GetJson<FeatureRequestResponse>($"{FrPath(projectId)}/1", ct);
        Assert.Equal(2, fr.UserStories.Count);
        Assert.Equal("a", fr.UserStories[0].Role);
        Assert.Equal("b", fr.UserStories[1].Role);
    }
}
