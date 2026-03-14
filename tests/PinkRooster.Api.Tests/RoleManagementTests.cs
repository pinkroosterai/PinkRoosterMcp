using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class RoleManagementTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string AuthPath = "/api/auth";

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
        var user = await client.GetFromJsonAsync<AuthUserResponse>($"{AuthPath}/me", ct);
        return (client, user!);
    }

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync("/api/projects", new
        {
            Name = "RoleMgmtProject",
            Description = "Test",
            ProjectPath = $"/tmp/role-mgmt-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return project!.Id;
    }

    private string RolePath(long projectId) => $"/api/projects/{projectId}/roles";

    // ── SuperUser Can Assign Any Role ──

    [Fact]
    public async Task SuperUser_CanAssignAdminRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-assign@test.com", ct);
        var (_, targetUser) = await RegisterAndLoginAsync("target-admin@test.com", ct);

        var response = await suClient.PutAsJsonAsync(
            $"{RolePath(projectId)}/{targetUser.Id}",
            new AssignRoleRequest { Role = ProjectRole.Admin }, ct);
        Assert.True(response.IsSuccessStatusCode);

        var role = await response.Content.ReadFromJsonAsync<UserProjectRoleResponse>(JsonOptions, ct);
        Assert.Equal("Admin", role!.Role);

        suClient.Dispose();
    }

    // ── Admin Can Assign Editor/Viewer But Not Admin ──

    [Fact]
    public async Task Admin_CanAssignEditorRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-admin-assign@test.com", ct);
        var (adminClient, adminUser) = await RegisterAndLoginAsync("admin-assigner@test.com", ct);
        var (_, editorTarget) = await RegisterAndLoginAsync("editor-target@test.com", ct);

        // Make adminUser an Admin via API key
        await Client.PutAsJsonAsync(
            $"{RolePath(projectId)}/{adminUser.Id}",
            new AssignRoleRequest { Role = ProjectRole.Admin }, ct);

        // Admin assigns Editor role — should succeed
        var response = await adminClient.PutAsJsonAsync(
            $"{RolePath(projectId)}/{editorTarget.Id}",
            new AssignRoleRequest { Role = ProjectRole.Editor }, ct);
        Assert.True(response.IsSuccessStatusCode);

        suClient.Dispose();
        adminClient.Dispose();
    }

    [Fact]
    public async Task Admin_CannotAssignAdminRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-admin-block@test.com", ct);
        var (adminClient, adminUser) = await RegisterAndLoginAsync("admin-blocker@test.com", ct);
        var (_, target) = await RegisterAndLoginAsync("admin-target@test.com", ct);

        // Make adminUser an Admin
        await Client.PutAsJsonAsync(
            $"{RolePath(projectId)}/{adminUser.Id}",
            new AssignRoleRequest { Role = ProjectRole.Admin }, ct);

        // Admin tries to assign Admin role — should fail
        var response = await adminClient.PutAsJsonAsync(
            $"{RolePath(projectId)}/{target.Id}",
            new AssignRoleRequest { Role = ProjectRole.Admin }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        suClient.Dispose();
        adminClient.Dispose();
    }

    // ── Editor Cannot Manage Roles ──

    [Fact]
    public async Task Editor_CannotAssignRoles()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-editor-block@test.com", ct);
        var (editorClient, editorUser) = await RegisterAndLoginAsync("editor-blocker@test.com", ct);
        var (_, target) = await RegisterAndLoginAsync("role-target@test.com", ct);

        // Make user an Editor
        await Client.PutAsJsonAsync(
            $"{RolePath(projectId)}/{editorUser.Id}",
            new AssignRoleRequest { Role = ProjectRole.Editor }, ct);

        // Editor tries to assign role — should fail
        var response = await editorClient.PutAsJsonAsync(
            $"{RolePath(projectId)}/{target.Id}",
            new AssignRoleRequest { Role = ProjectRole.Viewer }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        suClient.Dispose();
        editorClient.Dispose();
    }

    // ── Project Filtering ──

    [Fact]
    public async Task NonSuperUser_OnlySeesAssignedProjects()
    {
        var ct = TestContext.Current.CancellationToken;
        var project1Id = await CreateProjectAsync(ct);

        // Create a second project
        var resp2 = await Client.PutAsJsonAsync("/api/projects", new
        {
            Name = "Project2",
            Description = "Test2",
            ProjectPath = $"/tmp/role-filter-{Guid.NewGuid():N}"
        }, ct);
        var project2 = await resp2.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-filter@test.com", ct);
        var (viewerClient, viewerUser) = await RegisterAndLoginAsync("viewer-filter@test.com", ct);

        // Assign viewer to project1 only
        await Client.PutAsJsonAsync(
            $"{RolePath(project1Id)}/{viewerUser.Id}",
            new AssignRoleRequest { Role = ProjectRole.Viewer }, ct);

        // Viewer should only see project1
        var projects = await viewerClient.GetFromJsonAsync<List<ProjectResponse>>("/api/projects", ct);
        Assert.NotNull(projects);
        Assert.Single(projects);
        Assert.Equal(project1Id, projects[0].Id);

        // SuperUser sees all projects
        var suProjects = await suClient.GetFromJsonAsync<List<ProjectResponse>>("/api/projects", ct);
        Assert.NotNull(suProjects);
        Assert.True(suProjects.Count >= 2);

        suClient.Dispose();
        viewerClient.Dispose();
    }

    // ── Remove Role ──

    [Fact]
    public async Task RemoveRole_RevokesAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var (suClient, _) = await RegisterAndLoginAsync("su-remove@test.com", ct);
        var (viewerClient, viewerUser) = await RegisterAndLoginAsync("viewer-remove@test.com", ct);

        // Assign then remove role
        await Client.PutAsJsonAsync(
            $"{RolePath(projectId)}/{viewerUser.Id}",
            new AssignRoleRequest { Role = ProjectRole.Viewer }, ct);

        // Verify access
        var beforeRemove = await viewerClient.GetAsync($"/api/projects/{projectId}/issues", ct);
        Assert.Equal(HttpStatusCode.OK, beforeRemove.StatusCode);

        // Remove role via API key
        await Client.DeleteAsync($"{RolePath(projectId)}/{viewerUser.Id}", ct);

        // Verify access revoked
        var afterRemove = await viewerClient.GetAsync($"/api/projects/{projectId}/issues", ct);
        Assert.Equal(HttpStatusCode.Forbidden, afterRemove.StatusCode);

        suClient.Dispose();
        viewerClient.Dispose();
    }
}
