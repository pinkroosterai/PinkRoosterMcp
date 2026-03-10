using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class ScaffoldEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<long> CreateProjectAsync(CancellationToken ct)
    {
        var response = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/scaffold-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ct);
        return project!.Id;
    }

    private string ScaffoldPath(long projectId) => $"{BasePath}/{projectId}/work-packages/scaffold";
    private string WpDetailPath(long projectId, int wpNumber) => $"{BasePath}/{projectId}/work-packages/{wpNumber}";

    private static ScaffoldWorkPackageRequest MakeRequest(
        string name = "Test WP",
        List<ScaffoldPhaseRequest>? phases = null) => new()
    {
        Name = name,
        Description = "Test scaffold",
        Phases = phases ?? [new ScaffoldPhaseRequest { Name = "Phase 1" }]
    };

    // ── Basic scaffolding ──

    [Fact]
    public async Task Scaffold_CreatesFullStructure_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = MakeRequest(phases:
        [
            new ScaffoldPhaseRequest
            {
                Name = "Design",
                Tasks =
                [
                    new ScaffoldTaskRequest { Name = "Task A", Description = "First task" },
                    new ScaffoldTaskRequest { Name = "Task B", Description = "Second task" }
                ]
            },
            new ScaffoldPhaseRequest
            {
                Name = "Implementation",
                Tasks =
                [
                    new ScaffoldTaskRequest { Name = "Task C", Description = "Third task" },
                    new ScaffoldTaskRequest { Name = "Task D", Description = "Fourth task" }
                ]
            }
        ]);

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScaffoldWorkPackageResponse>(ct);
        Assert.NotNull(result);
        Assert.Equal(2, result.Phases.Count);
        Assert.Equal(4, result.TotalTasks);
        Assert.Equal(2, result.Phases[0].TaskIds.Count);
        Assert.Equal(2, result.Phases[1].TaskIds.Count);

        // Verify the WP actually exists with full structure
        var wp = await GetJson<WorkPackageResponse>(WpDetailPath(projectId, 1), ct);
        Assert.Equal("Test WP", wp.Name);
        Assert.Equal(2, wp.Phases.Count);
        Assert.Equal("Design", wp.Phases[0].Name);
        Assert.Equal("Implementation", wp.Phases[1].Name);
        Assert.Equal(2, wp.Phases[0].Tasks.Count);
        Assert.Equal(2, wp.Phases[1].Tasks.Count);
    }

    [Fact]
    public async Task Scaffold_ResponseContainsCorrectIds()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = MakeRequest(phases:
        [
            new ScaffoldPhaseRequest
            {
                Name = "Phase 1",
                Tasks =
                [
                    new ScaffoldTaskRequest { Name = "T1", Description = "D1" },
                    new ScaffoldTaskRequest { Name = "T2", Description = "D2" }
                ]
            }
        ]);

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);
        var result = await response.Content.ReadFromJsonAsync<ScaffoldWorkPackageResponse>(ct);

        Assert.NotNull(result);
        Assert.StartsWith("proj-", result.WorkPackageId);
        Assert.Single(result.Phases);
        Assert.StartsWith("proj-", result.Phases[0].PhaseId);
        Assert.Contains("-phase-1", result.Phases[0].PhaseId);
        Assert.Equal(2, result.Phases[0].TaskIds.Count);
        Assert.Contains("-task-1", result.Phases[0].TaskIds[0]);
        Assert.Contains("-task-2", result.Phases[0].TaskIds[1]);
    }

    // ── Task dependencies ──

    [Fact]
    public async Task Scaffold_WithTaskDependencies_CreatesDeps()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = MakeRequest(phases:
        [
            new ScaffoldPhaseRequest
            {
                Name = "Phase 1",
                Tasks =
                [
                    new ScaffoldTaskRequest { Name = "Foundation", Description = "Base work" },
                    new ScaffoldTaskRequest
                    {
                        Name = "Build on Foundation",
                        Description = "Depends on foundation",
                        DependsOnTaskIndices = [0]
                    }
                ]
            }
        ]);

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScaffoldWorkPackageResponse>(ct);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalDependencies);

        // Verify dependency in actual WP detail
        var wp = await GetJson<WorkPackageResponse>(WpDetailPath(projectId, 1), ct);
        var task2 = wp.Phases[0].Tasks[1];
        Assert.Single(task2.BlockedBy);
        Assert.Contains("Foundation", task2.BlockedBy[0].Name);
    }

    [Fact]
    public async Task Scaffold_CircularTaskDependency_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = MakeRequest(phases:
        [
            new ScaffoldPhaseRequest
            {
                Name = "Phase 1",
                Tasks =
                [
                    new ScaffoldTaskRequest { Name = "A", Description = "D", DependsOnTaskIndices = [1] },
                    new ScaffoldTaskRequest { Name = "B", Description = "D", DependsOnTaskIndices = [0] }
                ]
            }
        ]);

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scaffold_SelfReferencingTask_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = MakeRequest(phases:
        [
            new ScaffoldPhaseRequest
            {
                Name = "Phase 1",
                Tasks =
                [
                    new ScaffoldTaskRequest { Name = "A", Description = "D", DependsOnTaskIndices = [0] }
                ]
            }
        ]);

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scaffold_OutOfBoundsIndex_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = MakeRequest(phases:
        [
            new ScaffoldPhaseRequest
            {
                Name = "Phase 1",
                Tasks =
                [
                    new ScaffoldTaskRequest { Name = "A", Description = "D", DependsOnTaskIndices = [99] }
                ]
            }
        ]);

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── WP blockers ──

    [Fact]
    public async Task Scaffold_WithWpBlockers_CreatesWpDeps()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create a blocker WP first
        var blockerResponse = await Client.PostAsJsonAsync(
            $"{BasePath}/{projectId}/work-packages",
            new CreateWorkPackageRequest { Name = "Blocker WP", Description = "Blocks stuff" }, ct);
        var blockerWp = await blockerResponse.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        // Scaffold a new WP blocked by the existing one
        var request = new ScaffoldWorkPackageRequest
        {
            Name = "Dependent WP",
            Description = "Depends on blocker",
            State = CompletionState.Implementing,
            BlockedByWpIds = [blockerWp!.Id],
            Phases = [new ScaffoldPhaseRequest { Name = "Phase 1" }]
        };

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScaffoldWorkPackageResponse>(ct);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalDependencies);

        // Should have auto-blocked since blocker is non-terminal
        Assert.NotNull(result.StateChanges);
        Assert.Contains(result.StateChanges, sc => sc.NewState == "Blocked");

        // Verify via detail endpoint
        var wp = await GetJson<WorkPackageResponse>(WpDetailPath(projectId, 2), ct);
        Assert.Equal("Blocked", wp.State);
        Assert.Single(wp.BlockedBy);
    }

    // ── Task fields preserved ──

    [Fact]
    public async Task Scaffold_TaskFieldsPreserved()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        var request = MakeRequest(phases:
        [
            new ScaffoldPhaseRequest
            {
                Name = "Phase 1",
                AcceptanceCriteria =
                [
                    new AcceptanceCriterionDto { Name = "AC1", Description = "Criterion 1", VerificationMethod = VerificationMethod.AutomatedTest }
                ],
                Tasks =
                [
                    new ScaffoldTaskRequest
                    {
                        Name = "Rich Task",
                        Description = "Full detail task",
                        ImplementationNotes = "Important notes",
                        State = CompletionState.Implementing,
                        TargetFiles = [new FileReferenceDto { FileName = "Foo.cs", RelativePath = "src/", Description = "Main file" }]
                    }
                ]
            }
        ]);

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var wp = await GetJson<WorkPackageResponse>(WpDetailPath(projectId, 1), ct);
        var task = wp.Phases[0].Tasks[0];

        Assert.Equal("Rich Task", task.Name);
        Assert.Equal("Important notes", task.ImplementationNotes);
        Assert.Equal("Implementing", task.State);
        Assert.NotNull(task.StartedAt); // State timestamp applied
        Assert.Single(task.TargetFiles);
        Assert.Equal("Foo.cs", task.TargetFiles[0].FileName);

        // Acceptance criteria
        Assert.Single(wp.Phases[0].AcceptanceCriteria);
        Assert.Equal("AC1", wp.Phases[0].AcceptanceCriteria[0].Name);
    }

    // ── Linked issue ──

    [Fact]
    public async Task Scaffold_WithLinkedIssue_SetsLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Create an issue first
        var issueResponse = await Client.PostAsJsonAsync(
            $"{BasePath}/{projectId}/issues",
            new CreateIssueRequest
            {
                Name = "Test Issue",
                Description = "Issue desc",
                IssueType = IssueType.Bug,
                Severity = IssueSeverity.Major
            }, ct);
        var issue = await issueResponse.Content.ReadFromJsonAsync<IssueResponse>(ct);

        var request = new ScaffoldWorkPackageRequest
        {
            Name = "Fix WP",
            Description = "Fixes the issue",
            LinkedIssueId = issue!.Id,
            Phases = [new ScaffoldPhaseRequest { Name = "Phase 1" }]
        };

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var wp = await GetJson<WorkPackageResponse>(WpDetailPath(projectId, 1), ct);
        Assert.NotNull(wp.LinkedIssueId);
    }

    // ── Atomicity ──

    [Fact]
    public async Task Scaffold_TransactionalAtomicity_NoOrphansOnFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await CreateProjectAsync(ct);

        // Use self-referencing task to trigger failure after WP creation intent
        var request = MakeRequest(phases:
        [
            new ScaffoldPhaseRequest
            {
                Name = "Phase 1",
                Tasks =
                [
                    new ScaffoldTaskRequest { Name = "Good Task", Description = "Fine" },
                    new ScaffoldTaskRequest { Name = "Bad Task", Description = "Self-ref", DependsOnTaskIndices = [1] }
                ]
            }
        ]);

        var response = await Client.PostAsJsonAsync(ScaffoldPath(projectId), request, ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify nothing was created
        var wps = await GetJson<List<WorkPackageResponse>>($"{BasePath}/{projectId}/work-packages", ct);
        Assert.Empty(wps);
    }
}
