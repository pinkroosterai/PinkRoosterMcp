using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class SseEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var request = new CreateOrUpdateProjectRequest
        {
            Name = "SSE Test Project",
            Description = "Project for SSE tests",
            ProjectPath = $"/tmp/sse-test-{Guid.NewGuid():N}"
        };
        var response = await Client.PutAsJsonAsync("/api/projects", request, ct);
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);
        return long.Parse(project!.ProjectId.Replace("proj-", ""));
    }

    [Fact]
    public async Task SseEndpoint_ReturnsEventStreamContentType()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/events");
        try
        {
            var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Expected — we cancel after reading headers
        }
    }

    [Fact]
    public async Task SseEndpoint_RequiresApiKey()
    {
        var ct = TestContext.Current.CancellationToken;

        using var unauthClient = Factory.CreateClient();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects/1/events");
        var response = await unauthClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SseEndpoint_StreamsEntityChangedEvent_WhenIssueCreated()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        // Start SSE connection in background
        var sseRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/events");
        var sseResponse = await Client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        sseResponse.EnsureSuccessStatusCode();

        var stream = await sseResponse.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Create an issue to trigger an event
        var issueRequest = new CreateIssueRequest
        {
            Name = "SSE Test Issue",
            Description = "Testing SSE events",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Minor
        };

        // Use a second client to create the issue (the first client's stream is open)
        using var client2 = Factory.CreateAuthenticatedClient();
        var issueResponse = await client2.PostAsJsonAsync($"/api/projects/{projectId}/issues", issueRequest, cts.Token);
        issueResponse.EnsureSuccessStatusCode();

        // Read SSE lines until we find an entity:changed event or timeout
        var foundEntityEvent = false;
        var eventType = "";
        var eventData = "";

        while (!cts.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line == null) break;

            if (line.StartsWith("event: "))
            {
                eventType = line["event: ".Length..];
            }
            else if (line.StartsWith("data: "))
            {
                eventData = line["data: ".Length..];
            }
            else if (line == "" && eventType.Length > 0)
            {
                // End of event block
                if (eventType == "entity:changed")
                {
                    var json = JsonDocument.Parse(eventData);
                    var entityType = json.RootElement.GetProperty("entityType").GetString();
                    var action = json.RootElement.GetProperty("action").GetString();

                    if (entityType == "Issue" && action == "created")
                    {
                        foundEntityEvent = true;
                        break;
                    }
                }
                eventType = "";
                eventData = "";
            }
        }

        Assert.True(foundEntityEvent, "Expected to receive an entity:changed SSE event for Issue creation");
    }
}
