using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class SessionMiddlewareTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string AuthPath = "/api/auth";

    private HttpClient CreateCookieClient()
    {
        return Factory.CreateCookieClient();
    }

    private async Task<HttpClient> CreateAuthenticatedCookieClientAsync(CancellationToken ct)
    {
        var client = CreateCookieClient();

        await client.PostAsJsonAsync($"{AuthPath}/register", new RegisterRequest
        {
            Email = $"session-test-{Guid.NewGuid():N}@test.com",
            Password = "password123",
            DisplayName = "Session Test User"
        }, ct);

        await client.PostAsJsonAsync($"{AuthPath}/login", new LoginRequest
        {
            Email = $"session-test-{Guid.NewGuid():N}@test.com",
            Password = "password123"
        }, ct);

        return client;
    }

    [Fact]
    public async Task ApiKeyAuth_CreatesProject_WithKeyPrefixAsCallerIdentity()
    {
        var ct = TestContext.Current.CancellationToken;
        // Client from base class uses API key auth
        var response = await Client.PutAsJsonAsync("/api/projects", new
        {
            Name = "ApiKeyProject",
            Description = "Test",
            ProjectPath = $"/tmp/session-test-{Guid.NewGuid():N}"
        }, ct);
        Assert.True(response.IsSuccessStatusCode);

        // Check activity log — CallerIdentity should be API key prefix
        var logs = await GetJson<PaginatedResponse<ActivityLogResponse>>(
            "/api/activity-logs?pageSize=1", ct);
        Assert.NotNull(logs.Items);
        Assert.NotEmpty(logs.Items);
        Assert.Contains("...", logs.Items[0].CallerIdentity ?? "");
    }

    [Fact]
    public async Task CookieAuth_CreatesProject_WithEmailAsCallerIdentity()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateCookieClient();

        // Register and login
        var email = $"caller-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync($"{AuthPath}/register", new RegisterRequest
        {
            Email = email,
            Password = "password123",
            DisplayName = "Caller Test"
        }, ct);
        await client.PostAsJsonAsync($"{AuthPath}/login", new LoginRequest
        {
            Email = email,
            Password = "password123"
        }, ct);

        // Create a project using cookie auth
        var response = await client.PutAsJsonAsync("/api/projects", new
        {
            Name = "CookieProject",
            Description = "Test",
            ProjectPath = $"/tmp/cookie-test-{Guid.NewGuid():N}"
        }, ct);
        Assert.True(response.IsSuccessStatusCode);

        // Check activity log — CallerIdentity should be the user's email
        // Use the API-key client to read logs (more reliable)
        var logs = await GetJson<PaginatedResponse<ActivityLogResponse>>(
            "/api/activity-logs?pageSize=5", ct);
        var putLog = logs.Items.FirstOrDefault(l =>
            l.Path.Contains("projects") && l.HttpMethod == "PUT" && l.CallerIdentity == email);
        Assert.NotNull(putLog);
    }

    [Fact]
    public async Task NoCookieNoApiKey_OpenAccess_FallsThrough()
    {
        var ct = TestContext.Current.CancellationToken;

        // Create a factory with NO API keys configured
        var noKeyFactory = new ApiFactory(postgres.ConnectionString, configureApiKey: false);
        await postgres.EnsureMigratedAsync(noKeyFactory);
        var client = noKeyFactory.CreateClient();

        try
        {
            // Without any auth, health should always work
            var response = await client.GetAsync("/health", ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Auth config should be accessible
            var configResponse = await client.GetAsync($"{AuthPath}/config", ct);
            Assert.Equal(HttpStatusCode.OK, configResponse.StatusCode);
        }
        finally
        {
            client.Dispose();
            await noKeyFactory.DisposeAsync();
        }
    }

    // ── Helper types ──

    private sealed record PaginatedResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize);
    private sealed record ActivityLogResponse(
        string HttpMethod, string Path, int StatusCode,
        string? CallerIdentity, DateTimeOffset Timestamp);
}
