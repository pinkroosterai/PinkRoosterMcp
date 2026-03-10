using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PinkRooster.Mcp.Middleware;
using PinkRooster.Shared.Constants;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class McpApiKeyAuthMiddlewareTests
{
    private const string ValidKey = "test-mcp-key-12345";

    private static McpApiKeyAuthMiddleware CreateMiddleware(
        RequestDelegate next,
        string[]? apiKeys = null)
    {
        var configData = new Dictionary<string, string?>();
        if (apiKeys is not null)
        {
            for (var i = 0; i < apiKeys.Length; i++)
                configData[$"Auth:ApiKeys:{i}"] = apiKeys[i];
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return new McpApiKeyAuthMiddleware(next, configuration);
    }

    private static DefaultHttpContext CreateHttpContext(string path, string? apiKey = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (apiKey is not null)
            context.Request.Headers[AuthConstants.ApiKeyHeaderName] = apiKey;
        return context;
    }

    private static async Task<T?> ReadResponseBody<T>(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<T>(response.Body);
    }

    [Fact]
    public async Task Request_WithoutKey_WhenKeysConfigured_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, [ValidKey]);
        var context = CreateHttpContext("/");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Request_WithValidKey_WhenKeysConfigured_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, [ValidKey]);
        var context = CreateHttpContext("/", ValidKey);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Request_WithInvalidKey_WhenKeysConfigured_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, [ValidKey]);
        var context = CreateHttpContext("/", "wrong-key");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_BypassesAuth()
    {
        var ct = TestContext.Current.CancellationToken;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, [ValidKey]);
        var context = CreateHttpContext("/health");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Request_WithoutKey_WhenNoKeysConfigured_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, []);
        var context = CreateHttpContext("/");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Request_WhenNullKeysConfig_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext("/");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Request_WithValidKey_StoresCallerIdentity()
    {
        var ct = TestContext.Current.CancellationToken;
        var middleware = CreateMiddleware(_ => Task.CompletedTask, [ValidKey]);
        var context = CreateHttpContext("/", ValidKey);

        await middleware.InvokeAsync(context);

        Assert.True(context.Items.ContainsKey(AuthConstants.CallerIdentityKey));
        var identity = context.Items[AuthConstants.CallerIdentityKey] as string;
        Assert.Equal("test-mcp...", identity);
    }
}
