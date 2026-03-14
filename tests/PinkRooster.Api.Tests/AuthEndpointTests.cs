using System.Net;
using System.Net.Http.Json;
using Microsoft.Net.Http.Headers;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class AuthEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string AuthPath = "/api/auth";

    // ── Helpers ──

    private HttpClient CreateCookieClient()
    {
        return Factory.CreateCookieClient();
    }

    private async Task<HttpResponseMessage> RegisterUserAsync(
        HttpClient client, string email, string password, string displayName, CancellationToken ct)
    {
        return await client.PostAsJsonAsync($"{AuthPath}/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        }, ct);
    }

    private async Task<HttpResponseMessage> LoginUserAsync(
        HttpClient client, string email, string password, CancellationToken ct)
    {
        return await client.PostAsJsonAsync($"{AuthPath}/login", new LoginRequest
        {
            Email = email,
            Password = password
        }, ct);
    }

    // ── Config ──

    [Fact]
    public async Task Config_NoUsers_ReturnsNotProtected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        var response = await client.GetAsync($"{AuthPath}/config", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ConfigResponse>(JsonOptions, ct);
        Assert.False(body!.IsProtected);
    }

    [Fact]
    public async Task Config_WithUsers_ReturnsProtected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "admin@test.com", "password123", "Admin", ct);

        var response = await client.GetAsync($"{AuthPath}/config", ct);
        var body = await response.Content.ReadFromJsonAsync<ConfigResponse>(JsonOptions, ct);
        Assert.True(body!.IsProtected);
    }

    // ── Registration ──

    [Fact]
    public async Task Register_FirstUser_GetsSuperUserRole()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        var response = await RegisterUserAsync(client, "first@test.com", "password123", "First User", ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions, ct);
        Assert.Equal("SuperUser", user!.GlobalRole);
        Assert.Equal("first@test.com", user.Email);
        Assert.Equal("First User", user.DisplayName);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Register_SecondUser_GetsUserRole()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "first@test.com", "password123", "First", ct);
        var response = await RegisterUserAsync(client, "second@test.com", "password456", "Second", ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions, ct);
        Assert.Equal("User", user!.GlobalRole);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "dupe@test.com", "password123", "User 1", ct);
        var response = await RegisterUserAsync(client, "dupe@test.com", "password456", "User 2", ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Login ──

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithCookie()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "login@test.com", "password123", "Login User", ct);
        var response = await LoginUserAsync(client, "login@test.com", "password123", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct);
        Assert.Equal("login@test.com", loginResponse!.User.Email);

        // Verify Set-Cookie header is present
        Assert.True(response.Headers.Contains(HeaderNames.SetCookie) ||
                    response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "wrong@test.com", "password123", "User", ct);
        var response = await LoginUserAsync(client, "wrong@test.com", "wrongpassword", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonexistentUser_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        var response = await LoginUserAsync(client, "noone@test.com", "password123", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Me ──

    [Fact]
    public async Task Me_WithValidSession_ReturnsUserProfile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "me@test.com", "password123", "Me User", ct);
        await LoginUserAsync(client, "me@test.com", "password123", ct);

        var response = await client.GetAsync($"{AuthPath}/me", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions, ct);
        Assert.Equal("me@test.com", user!.Email);
        Assert.Equal("Me User", user.DisplayName);
    }

    [Fact]
    public async Task Me_WithoutSession_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        var response = await client.GetAsync($"{AuthPath}/me", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Logout ──

    [Fact]
    public async Task Logout_ClearsSession()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "logout@test.com", "password123", "Logout User", ct);
        await LoginUserAsync(client, "logout@test.com", "password123", ct);

        // Verify session works
        var meResponse = await client.GetAsync($"{AuthPath}/me", ct);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // Logout
        var logoutResponse = await client.PostAsync($"{AuthPath}/logout", null, ct);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Verify session no longer works
        var meAfterLogout = await client.GetAsync($"{AuthPath}/me", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogout.StatusCode);
    }

    // ── API Key Auth Coexistence ──

    [Fact]
    public async Task ApiKeyAuth_StillWorks_WithSessionMiddleware()
    {
        var ct = TestContext.Current.CancellationToken;
        // Client from base class uses API key auth
        var response = await Client.GetAsync("/health", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // API key-authenticated requests to other endpoints still work
        var projectResponse = await Client.PutAsJsonAsync("/api/projects", new
        {
            Name = "AuthTest",
            Description = "Testing API key coexistence",
            ProjectPath = $"/tmp/auth-test-{Guid.NewGuid():N}"
        }, ct);
        Assert.True(projectResponse.IsSuccessStatusCode);
    }

    // ── Input Validation ──

    [Fact]
    public async Task Register_OversizedPassword_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        var response = await RegisterUserAsync(
            client, "oversize@test.com", new string('A', 10000), "Oversize", ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        var response = await RegisterUserAsync(
            client, "short@test.com", "abc", "Short", ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        var response = await RegisterUserAsync(
            client, "not-an-email", "password123", "Bad Email", ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Session Invalidation ──

    [Fact]
    public async Task ChangePassword_InvalidatesExistingSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "session-invalidate@test.com", "password123", "Test", ct);
        await LoginUserAsync(client, "session-invalidate@test.com", "password123", ct);

        // Verify session works
        var meResponse = await client.GetAsync($"{AuthPath}/me", ct);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // Change password
        await client.PostAsJsonAsync($"{AuthPath}/me/password", new ChangePasswordRequest
        {
            CurrentPassword = "password123",
            NewPassword = "newpassword456"
        }, ct);

        // Old session should no longer work
        var meAfterChange = await client.GetAsync($"{AuthPath}/me", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterChange.StatusCode);
    }

    // ── Email Change Security ──

    [Fact]
    public async Task UpdateProfile_EmailChangeWithoutPassword_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "email-nopass@test.com", "password123", "Test", ct);
        await LoginUserAsync(client, "email-nopass@test.com", "password123", ct);

        var response = await client.PatchAsJsonAsync($"{AuthPath}/me", new
        {
            email = "new-email@test.com"
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_EmailChangeWithCorrectPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "email-pass@test.com", "password123", "Test", ct);
        await LoginUserAsync(client, "email-pass@test.com", "password123", ct);

        var response = await client.PatchAsJsonAsync($"{AuthPath}/me", new
        {
            email = "new-email-ok@test.com",
            currentPassword = "password123"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions, ct);
        Assert.Equal("new-email-ok@test.com", user!.Email);
    }

    [Fact]
    public async Task UpdateProfile_EmailChangeWithWrongPassword_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "email-wrong@test.com", "password123", "Test", ct);
        await LoginUserAsync(client, "email-wrong@test.com", "password123", ct);

        var response = await client.PatchAsJsonAsync($"{AuthPath}/me", new
        {
            email = "hacked@test.com",
            currentPassword = "wrongpassword"
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Account Lockout ──

    [Fact]
    public async Task Login_LocksAfter5FailedAttempts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "lockout@test.com", "password123", "Lockout", ct);

        // 5 failed attempts
        for (var i = 0; i < 5; i++)
        {
            var fail = await LoginUserAsync(client, "lockout@test.com", "wrongpassword", ct);
            Assert.Equal(HttpStatusCode.Unauthorized, fail.StatusCode);
        }

        // 6th attempt with CORRECT password should still fail (locked)
        var locked = await LoginUserAsync(client, "lockout@test.com", "password123", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task Login_SuccessResetsFailedAttempts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        await RegisterUserAsync(client, "reset-counter@test.com", "password123", "Reset", ct);

        // 3 failed attempts (not enough to lock)
        for (var i = 0; i < 3; i++)
            await LoginUserAsync(client, "reset-counter@test.com", "wrongpassword", ct);

        // Successful login resets counter
        var success = await LoginUserAsync(client, "reset-counter@test.com", "password123", ct);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        // 3 more failed attempts (still not enough because counter was reset)
        for (var i = 0; i < 3; i++)
            await LoginUserAsync(client, "reset-counter@test.com", "wrongpassword", ct);

        // Should still be able to login (only 3 failures, not 5)
        var stillWorks = await LoginUserAsync(client, "reset-counter@test.com", "password123", ct);
        Assert.Equal(HttpStatusCode.OK, stillWorks.StatusCode);
    }

    // ── Helper types ──

    private sealed record ConfigResponse(bool IsProtected);
}
