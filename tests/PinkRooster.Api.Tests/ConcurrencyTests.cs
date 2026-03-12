using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class ConcurrencyTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    [Fact]
    public async Task ParallelIssueCreation_AssignsUniqueNumbers()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        const int count = 10;
        var tasks = Enumerable.Range(0, count).Select(i =>
        {
            var request = new CreateIssueRequest
            {
                Name = $"Concurrent issue {i}",
                Description = "Created in parallel",
                IssueType = IssueType.Bug,
                Severity = IssueSeverity.Minor
            };
            return Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), request, ct);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        var issueNumbers = new List<int>();
        var failedCount = 0;
        foreach (var r in responses)
        {
            if (r.IsSuccessStatusCode)
            {
                var issue = await r.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, ct);
                issueNumbers.Add(issue!.IssueNumber);
            }
            else
            {
                // Serializable transaction retries may cause some 500s
                failedCount++;
            }
        }

        // At least some should succeed (serialization failures are expected under contention)
        Assert.True(issueNumbers.Count > 0, "All concurrent requests failed");

        // All assigned numbers must be unique
        Assert.Equal(issueNumbers.Count, issueNumbers.Distinct().Count());

        // All numbers must be positive
        Assert.All(issueNumbers, n => Assert.True(n > 0));
    }

    [Fact]
    public async Task ParallelWorkPackageCreation_AssignsUniqueNumbers()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        const int count = 10;
        var tasks = Enumerable.Range(0, count).Select(i =>
        {
            var request = new CreateWorkPackageRequest
            {
                Name = $"Concurrent WP {i}",
                Description = "Created in parallel"
            };
            return Client.PostAsJsonAsync(TestHelpers.WpPath(projectId), request, ct);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        var wpNumbers = new List<int>();
        foreach (var r in responses)
        {
            if (r.IsSuccessStatusCode)
            {
                var wp = await r.Content.ReadFromJsonAsync<WorkPackageResponse>(JsonOptions, ct);
                wpNumbers.Add(wp!.WorkPackageNumber);
            }
        }

        Assert.True(wpNumbers.Count > 0, "All concurrent requests failed");
        Assert.Equal(wpNumbers.Count, wpNumbers.Distinct().Count());
        Assert.All(wpNumbers, n => Assert.True(n > 0));
    }

    [Fact]
    public async Task ParallelFeatureRequestCreation_AssignsUniqueNumbers()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        const int count = 10;
        var tasks = Enumerable.Range(0, count).Select(i =>
        {
            var request = new CreateFeatureRequestRequest
            {
                Name = $"Concurrent FR {i}",
                Description = "Created in parallel",
                Category = FeatureCategory.Feature
            };
            return Client.PostAsJsonAsync(TestHelpers.FrPath(projectId), request, ct);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        var frNumbers = new List<int>();
        foreach (var r in responses)
        {
            if (r.IsSuccessStatusCode)
            {
                var fr = await r.Content.ReadFromJsonAsync<FeatureRequestResponse>(JsonOptions, ct);
                frNumbers.Add(fr!.FeatureRequestNumber);
            }
        }

        Assert.True(frNumbers.Count > 0, "All concurrent requests failed");
        Assert.Equal(frNumbers.Count, frNumbers.Distinct().Count());
        Assert.All(frNumbers, n => Assert.True(n > 0));
    }
}
