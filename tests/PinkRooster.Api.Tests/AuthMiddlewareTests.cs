using System.Net;
using PinkRooster.Api.Tests.Fixtures;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class AuthMiddlewareTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    [Fact]
    public async Task Request_WithoutApiKey_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var unauthClient = Factory.CreateClient();

        var response = await unauthClient.GetAsync("/api/projects", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithInvalidApiKey_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var badClient = Factory.CreateClient();
        badClient.DefaultRequestHeaders.Add("X-Api-Key", "invalid-key");

        var response = await badClient.GetAsync("/api/projects", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithValidApiKey_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/api/projects", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_NoApiKey_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var unauthClient = Factory.CreateClient();

        var response = await unauthClient.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
