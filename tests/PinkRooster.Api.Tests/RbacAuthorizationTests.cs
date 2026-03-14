using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class RbacAuthorizationTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string AuthPath = "/api/auth";

    // ── Helpers ──

    private async Task<(HttpClient Client, AuthUserResponse User)> RegisterAndLoginAsync(
        string email, CancellationToken ct)
    {
        var client = Factory.CreateCookieClient();
        await client.PostAsJsonAsync($"{AuthPath}/register", new RegisterRequest
        {
            Email = email,
            Password = "password123",
            DisplayName = email.Split('@')[0]
        }, ct);
        await client.PostAsJsonAsync($"{AuthPath}/login", new LoginRequest
        {
            Email = email,
            Password = "password123"
        }, ct);

        var meResponse = await client.GetFromJsonAsync<AuthUserResponse>($"{AuthPath}/me", ct);
        return (client, meResponse!);
    }

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync("/api/projects", new
        {
            Name = "RbacTestProject",
            Description = "Test",
            ProjectPath = $"/tmp/rbac-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return project!.Id;
    }

    private async Task AssignRoleAsync(long userId, long projectId, ProjectRole role, CancellationToken ct)
    {
        await Client.PutAsJsonAsync(
            $"/api/projects/{projectId}/roles/{userId}",
            new AssignRoleRequest { Role = role }, ct);
    }

    // ── SuperUser Tests ──

    [Fact]
    public async Task SuperUser_CanAccessAnyProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Register first user (SuperUser) and login
        var (suClient, _) = await RegisterAndLoginAsync("su-rbac@test.com", ct);

        // SuperUser can GET project entities without explicit role assignment
        var response = await suClient.GetAsync($"/api/projects/{projectId}/issues", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        suClient.Dispose();
    }

    // ── Viewer Tests ──

    [Fact]
    public async Task Viewer_CanRead_CannotWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // First user = SuperUser
        var (suClient, _) = await RegisterAndLoginAsync("su-viewer@test.com", ct);

        // Second user = Viewer
        var (viewerClient, viewerUser) = await RegisterAndLoginAsync("viewer@test.com", ct);
        await AssignRoleAsync(viewerUser.Id, projectId, ProjectRole.Viewer, ct);

        // Viewer CAN read
        var getResponse = await viewerClient.GetAsync($"/api/projects/{projectId}/issues", ct);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // Viewer CANNOT create
        var postResponse = await viewerClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/issues",
            new CreateIssueRequest
            {
                Name = "Test",
                Description = "Test",
                IssueType = IssueType.Bug,
                Severity = IssueSeverity.Major
            }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);

        suClient.Dispose();
        viewerClient.Dispose();
    }

    // ── Editor Tests ──

    [Fact]
    public async Task Editor_CanReadAndWrite_CannotDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-editor@test.com", ct);
        var (editorClient, editorUser) = await RegisterAndLoginAsync("editor@test.com", ct);
        await AssignRoleAsync(editorUser.Id, projectId, ProjectRole.Editor, ct);

        // Editor CAN create
        var postResponse = await editorClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/issues",
            new CreateIssueRequest
            {
                Name = "Editor Issue",
                Description = "Created by editor",
                IssueType = IssueType.Bug,
                Severity = IssueSeverity.Minor
            }, ct);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var issue = await postResponse.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, ct);

        // Editor CANNOT delete
        var deleteResponse = await editorClient.DeleteAsync(
            $"/api/projects/{projectId}/issues/{issue!.IssueNumber}", ct);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        suClient.Dispose();
        editorClient.Dispose();
    }

    // ── Unassigned User Tests ──

    [Fact]
    public async Task UnassignedUser_Gets403()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-unassigned@test.com", ct);
        var (unassignedClient, _) = await RegisterAndLoginAsync("unassigned@test.com", ct);

        // Unassigned user gets 403 on project-scoped endpoints
        var response = await unassignedClient.GetAsync($"/api/projects/{projectId}/issues", ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        suClient.Dispose();
        unassignedClient.Dispose();
    }

    // ── API Key Bypass ──

    [Fact]
    public async Task ApiKeyAuth_BypassesRbac()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // API key client (from base class) can still access everything
        var response = await Client.GetAsync($"/api/projects/{projectId}/issues", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var postResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/issues",
            new CreateIssueRequest
            {
                Name = "API Key Issue",
                Description = "Created via API key",
                IssueType = IssueType.Bug,
                Severity = IssueSeverity.Minor
            }, ct);
        Assert.True(postResponse.IsSuccessStatusCode);
    }

    // ── Permissions Endpoint ──

    [Fact]
    public async Task PermissionsEndpoint_ReturnsCorrectFlags()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-perms@test.com", ct);
        var (viewerClient, viewerUser) = await RegisterAndLoginAsync("viewer-perms@test.com", ct);
        await AssignRoleAsync(viewerUser.Id, projectId, ProjectRole.Viewer, ct);

        // SuperUser permissions
        var suPerms = await suClient.GetFromJsonAsync<UserPermissionsResponse>(
            $"{AuthPath}/me/permissions?projectId={projectId}", ct);
        Assert.True(suPerms!.CanRead);
        Assert.True(suPerms.CanCreate);
        Assert.True(suPerms.CanEdit);
        Assert.True(suPerms.CanDelete);
        Assert.True(suPerms.CanManageRoles);
        Assert.Equal("SuperUser", suPerms.EffectiveRole);

        // Viewer permissions
        var viewerPerms = await viewerClient.GetFromJsonAsync<UserPermissionsResponse>(
            $"{AuthPath}/me/permissions?projectId={projectId}", ct);
        Assert.True(viewerPerms!.CanRead);
        Assert.False(viewerPerms.CanCreate);
        Assert.False(viewerPerms.CanEdit);
        Assert.False(viewerPerms.CanDelete);
        Assert.False(viewerPerms.CanManageRoles);
        Assert.Equal("Viewer", viewerPerms.EffectiveRole);

        suClient.Dispose();
        viewerClient.Dispose();
    }
}
