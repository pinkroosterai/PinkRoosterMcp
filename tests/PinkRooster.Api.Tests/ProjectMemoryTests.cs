using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class ProjectMemoryTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "MemoryTest",
            Description = "Test",
            ProjectPath = $"/tmp/memory-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return project!.Id;
    }

    private string MemPath(long projectId) => $"{BasePath}/{projectId}/memories";

    // ── Create ──

    [Fact]
    public async Task Create_ReturnsNewMemory()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r = await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "arch-decisions",
            Content = "We chose PostgreSQL for its JSONB support.",
            Tags = ["architecture", "database"]
        }, ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var memory = await ReadJson<ProjectMemoryResponse>(r, ct);
        Assert.Equal("arch-decisions", memory.Name);
        Assert.Equal("We chose PostgreSQL for its JSONB support.", memory.Content);
        Assert.Equal(1, memory.MemoryNumber);
        Assert.False(memory.WasMerged);
        Assert.Contains("architecture", memory.Tags);
        Assert.Contains("database", memory.Tags);
    }

    // ── Upsert Merge ──

    [Fact]
    public async Task Upsert_SameNameMergesContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create initial memory
        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "patterns",
            Content = "First pattern.",
            Tags = ["code"]
        }, ct);

        // Upsert with same name
        var r = await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "patterns",
            Content = "Second pattern.",
            Tags = ["design"]
        }, ct);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var memory = await ReadJson<ProjectMemoryResponse>(r, ct);
        Assert.True(memory.WasMerged);
        Assert.Contains("First pattern.", memory.Content);
        Assert.Contains("---", memory.Content);
        Assert.Contains("Second pattern.", memory.Content);
        Assert.Contains("code", memory.Tags);
        Assert.Contains("design", memory.Tags);
        Assert.Equal(1, memory.MemoryNumber); // Same memory, not a new one
    }

    // ── Get By Number ──

    [Fact]
    public async Task GetByNumber_ReturnsMemory()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "test-memory",
            Content = "Some content."
        }, ct);

        var memory = await GetJson<ProjectMemoryResponse>($"{MemPath(projectId)}/1", ct);
        Assert.Equal("test-memory", memory.Name);
        Assert.Equal("Some content.", memory.Content);
    }

    [Fact]
    public async Task GetByNumber_NotFound_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r = await Client.GetAsync($"{MemPath(projectId)}/999", ct);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    // ── List ──

    [Fact]
    public async Task List_ReturnsAllMemories()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "mem-a", Content = "A"
        }, ct);
        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "mem-b", Content = "B"
        }, ct);

        var memories = await GetJson<List<ProjectMemoryListItemResponse>>(MemPath(projectId), ct);
        Assert.Equal(2, memories.Count);
    }

    [Fact]
    public async Task List_FilterByNamePattern()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "architecture", Content = "A"
        }, ct);
        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "testing", Content = "B"
        }, ct);

        var memories = await GetJson<List<ProjectMemoryListItemResponse>>(
            $"{MemPath(projectId)}?namePattern=arch", ct);
        Assert.Single(memories);
        Assert.Equal("architecture", memories[0].Name);
    }

    [Fact]
    public async Task List_FilterByTag()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "mem-1", Content = "A", Tags = ["api"]
        }, ct);
        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "mem-2", Content = "B", Tags = ["ui"]
        }, ct);

        var memories = await GetJson<List<ProjectMemoryListItemResponse>>(
            $"{MemPath(projectId)}?tag=api", ct);
        Assert.Single(memories);
        Assert.Equal("mem-1", memories[0].Name);
    }

    // ── Delete ──

    [Fact]
    public async Task Delete_RemovesMemory()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "to-delete", Content = "Gone."
        }, ct);

        var del = await Client.DeleteAsync($"{MemPath(projectId)}/1", ct);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await Client.GetAsync($"{MemPath(projectId)}/1", ct);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var del = await Client.DeleteAsync($"{MemPath(projectId)}/999", ct);
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }

    // ── Sequential Number Stability ──

    [Fact]
    public async Task MemoryNumber_NeverReused_AfterDeletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r1 = await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "mem-1", Content = "A"
        }, ct);
        var m1 = await ReadJson<ProjectMemoryResponse>(r1, ct);
        Assert.Equal(1, m1.MemoryNumber);

        var r2 = await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "mem-2", Content = "B"
        }, ct);
        var m2 = await ReadJson<ProjectMemoryResponse>(r2, ct);
        Assert.Equal(2, m2.MemoryNumber);

        // Delete memory #2
        await Client.DeleteAsync($"{MemPath(projectId)}/2", ct);

        // Create memory #3 — must NOT reuse number 2
        var r3 = await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "mem-3", Content = "C"
        }, ct);
        var m3 = await ReadJson<ProjectMemoryResponse>(r3, ct);
        Assert.Equal(3, m3.MemoryNumber);
    }

    // ── Tag Deduplication ──

    [Fact]
    public async Task Create_DeduplicatesTags()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var r = await Client.PostAsJsonAsync(MemPath(projectId), new UpsertProjectMemoryRequest
        {
            Name = "dedup-test",
            Content = "Test.",
            Tags = ["api", "API", "Api"]
        }, ct);

        var memory = await ReadJson<ProjectMemoryResponse>(r, ct);
        Assert.Single(memory.Tags);
    }
}
