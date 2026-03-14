using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.Data;

namespace PinkRooster.Api.Tests.Fixtures;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-key-12345678";

    private readonly string _connectionString;
    private readonly bool _configureApiKey;

    public ApiFactory(string connectionString, bool configureApiKey = true)
    {
        _connectionString = connectionString;
        _configureApiKey = configureApiKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        if (_configureApiKey)
        {
            builder.ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:ApiKeys:0"] = TestApiKey
                });
            });
        }

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_connectionString));
        });
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);
        return client;
    }

    public HttpClient CreateCookieClient()
    {
        var handler = new CookieHandler(Server.CreateHandler());
        return new HttpClient(handler) { BaseAddress = Server.BaseAddress };
    }

    /// <summary>
    /// DelegatingHandler that manages cookies from Set-Cookie headers,
    /// since WebApplicationFactory's in-memory test server doesn't handle cookies automatically.
    /// </summary>
    private sealed class CookieHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        private readonly System.Net.CookieContainer _cookies = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Attach stored cookies to request
            var cookieHeader = _cookies.GetCookieHeader(request.RequestUri!);
            if (!string.IsNullOrEmpty(cookieHeader))
                request.Headers.Add("Cookie", cookieHeader);

            var response = await base.SendAsync(request, cancellationToken);

            // Store cookies from response
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var cookie in setCookies)
                    _cookies.SetCookies(request.RequestUri!, cookie);
            }

            return response;
        }
    }
}
