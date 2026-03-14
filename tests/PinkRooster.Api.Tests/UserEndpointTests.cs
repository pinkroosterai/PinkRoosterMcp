using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Controllers;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class UserEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string AuthPath = "/api/auth";
    private const string UsersPath = "/api/users";

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

    // ── SuperUser CRUD ──

    [Fact]
    public async Task SuperUser_CanListUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var (suClient, _) = await RegisterAndLoginAsync("su-list@test.com", ct);

        var response = await suClient.GetAsync(UsersPath, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<List<AuthUserResponse>>(JsonOptions, ct);
        Assert.NotNull(users);
        Assert.NotEmpty(users);

        suClient.Dispose();
    }

    [Fact]
    public async Task SuperUser_CanCreateUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var (suClient, _) = await RegisterAndLoginAsync("su-create@test.com", ct);

        var response = await suClient.PostAsJsonAsync(UsersPath, new CreateUserRequest
        {
            Email = "newuser@test.com",
            Password = "password123",
            DisplayName = "New User",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions, ct);
        Assert.Equal("newuser@test.com", user!.Email);
        Assert.Equal("User", user.GlobalRole);

        suClient.Dispose();
    }

    [Fact]
    public async Task SuperUser_CanDeactivateUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var (suClient, _) = await RegisterAndLoginAsync("su-deactivate@test.com", ct);
        var (_, targetUser) = await RegisterAndLoginAsync("target-deactivate@test.com", ct);

        var response = await suClient.DeleteAsync($"{UsersPath}/{targetUser.Id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deactivated
        var detail = await suClient.GetFromJsonAsync<AuthUserResponse>($"{UsersPath}/{targetUser.Id}", ct);
        Assert.False(detail!.IsActive);

        suClient.Dispose();
    }

    // ── Non-SuperUser Restrictions ──

    [Fact]
    public async Task NonSuperUser_CannotListUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _) = await RegisterAndLoginAsync("su-block@test.com", ct);
        var (viewerClient, _) = await RegisterAndLoginAsync("viewer-block@test.com", ct);

        var response = await viewerClient.GetAsync(UsersPath, ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        viewerClient.Dispose();
    }

    [Fact]
    public async Task NonSuperUser_CanViewSelf()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _) = await RegisterAndLoginAsync("su-self@test.com", ct);
        var (viewerClient, viewerUser) = await RegisterAndLoginAsync("viewer-self@test.com", ct);

        var response = await viewerClient.GetAsync($"{UsersPath}/{viewerUser.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        viewerClient.Dispose();
    }

    // ── Profile Update ──

    [Fact]
    public async Task UpdateProfile_UpdatesDisplayName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync("profile@test.com", ct);

        var response = await client.PatchAsJsonAsync($"{AuthPath}/me", new UpdateProfileRequest
        {
            DisplayName = "Updated Name"
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions, ct);
        Assert.Equal("Updated Name", user!.DisplayName);

        client.Dispose();
    }

    // ── Password Change ──

    [Fact]
    public async Task ChangePassword_WithCorrectCurrent_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync("pw-change@test.com", ct);

        var response = await client.PostAsJsonAsync($"{AuthPath}/me/password", new ChangePasswordRequest
        {
            CurrentPassword = "password123",
            NewPassword = "newpassword456"
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify can login with new password
        await client.PostAsJsonAsync($"{AuthPath}/logout", new { }, ct);
        var loginResponse = await client.PostAsJsonAsync($"{AuthPath}/login", new LoginRequest
        {
            Email = "pw-change@test.com",
            Password = "newpassword456"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        client.Dispose();
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrent_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync("pw-wrong@test.com", ct);

        var response = await client.PostAsJsonAsync($"{AuthPath}/me/password", new ChangePasswordRequest
        {
            CurrentPassword = "wrongpassword",
            NewPassword = "newpassword456"
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        client.Dispose();
    }

    // ── API Key Bypass ──

    [Fact]
    public async Task ApiKeyAuth_CanListUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        // Create a user first
        var (_, _) = await RegisterAndLoginAsync("apikey-user@test.com", ct);

        // API key client can list users
        var response = await Client.GetAsync(UsersPath, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
