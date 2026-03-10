using System.Net.Http.Json;
using Xunit;

namespace PinkRooster.Api.Tests.Fixtures;

[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTest : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    protected ApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    protected IntegrationTest(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async ValueTask InitializeAsync()
    {
        Factory = new ApiFactory(_postgres.ConnectionString);
        await _postgres.EnsureMigratedAsync(Factory);
        await _postgres.ResetDatabaseAsync();
        Client = Factory.CreateAuthenticatedClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    protected async Task<T> GetJson<T>(string url, CancellationToken ct = default) where T : class
    {
        return (await Client.GetFromJsonAsync<T>(url, ct))!;
    }
}
