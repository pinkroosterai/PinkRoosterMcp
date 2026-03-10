using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class ProjectEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private static CreateOrUpdateProjectRequest MakeRequest(string? suffix = null) => new()
    {
        Name = $"TestProject{suffix}",
        Description = "A test project",
        ProjectPath = $"/tmp/test-project-{suffix ?? Guid.NewGuid().ToString("N")}"
    };

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoProjects()
    {
        var ct = TestContext.Current.CancellationToken;

        var projects = await GetJson<List<ProjectResponse>>("/api/projects", ct);

        Assert.NotNull(projects);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task Put_CreatesNewProject_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("create");

        var response = await Client.PutAsJsonAsync("/api/projects", request, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);

        Assert.NotNull(project);
        Assert.StartsWith("proj-", project.ProjectId);
        Assert.Equal(request.Name, project.Name);
        Assert.Equal(request.Description, project.Description);
        Assert.Equal(request.ProjectPath, project.ProjectPath);
        Assert.Equal("Active", project.Status);
    }

    [Fact]
    public async Task Put_UpdatesExistingProject_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("update");
        await Client.PutAsJsonAsync("/api/projects", request, ct);

        var updated = new CreateOrUpdateProjectRequest
        {
            Name = "UpdatedName",
            Description = "Updated desc",
            ProjectPath = request.ProjectPath
        };
        var response = await Client.PutAsJsonAsync("/api/projects", updated, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);

        Assert.NotNull(project);
        Assert.Equal("UpdatedName", project.Name);
        Assert.Equal("Updated desc", project.Description);
    }

    [Fact]
    public async Task Get_ByPath_ReturnsProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("bypath");
        await Client.PutAsJsonAsync("/api/projects", request, ct);

        var encoded = Uri.EscapeDataString(request.ProjectPath);
        var project = await GetJson<ProjectResponse>($"/api/projects?path={encoded}", ct);

        Assert.NotNull(project);
        Assert.Equal(request.Name, project.Name);
        Assert.Equal(request.ProjectPath, project.ProjectPath);
    }

    [Fact]
    public async Task Get_ByPath_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/api/projects?path=/nonexistent/path", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesProject_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("delete");
        var putResponse = await Client.PutAsJsonAsync("/api/projects", request, ct);
        var project = await putResponse.Content.ReadFromJsonAsync<ProjectResponse>(ct);

        var response = await Client.DeleteAsync($"/api/projects/{project!.Id}", ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var encoded = Uri.EscapeDataString(request.ProjectPath);
        var getResponse = await Client.GetAsync($"/api/projects?path={encoded}", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.DeleteAsync("/api/projects/999999", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_ProjectId_UsesProjectPrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("prefix");
        var response = await Client.PutAsJsonAsync("/api/projects", request, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);

        Assert.NotNull(project);
        Assert.Matches(@"^proj-\d+$", project.ProjectId);
        Assert.Equal($"proj-{project.Id}", project.ProjectId);
    }
}
