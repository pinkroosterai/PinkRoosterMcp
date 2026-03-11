using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class PhaseEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<(long ProjectId, int WpNumber)> CreateProjectAndWpAsync(CancellationToken ct)
    {
        var projResponse = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/phase-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await projResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions, ct);

        await Client.PostAsJsonAsync($"{BasePath}/{project!.Id}/work-packages", new CreateWorkPackageRequest
        {
            Name = "Test WP",
            Description = "Test"
        }, ct);

        return (project.Id, 1);
    }

    private string PhasePath(long projectId, int wpNumber) =>
        $"{BasePath}/{projectId}/work-packages/{wpNumber}/phases";

    // ── CRUD ──

    [Fact]
    public async Task Post_CreatesPhase_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);

        var response = await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest
        {
            Name = "Phase 1",
            Description = "First phase"
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

        Assert.NotNull(phase);
        Assert.Equal(1, phase.PhaseNumber);
        Assert.Equal("Phase 1", phase.Name);
        Assert.Equal("NotStarted", phase.State);
    }

    [Fact]
    public async Task Post_SequentialPhaseNumbering()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);

        await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest { Name = "P1" }, ct);
        var r2 = await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest { Name = "P2" }, ct);
        var phase2 = await r2.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

        Assert.Equal(2, phase2!.PhaseNumber);
    }

    [Fact]
    public async Task Post_WithAcceptanceCriteria_PersistsCriteria()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);

        var response = await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest
        {
            Name = "Phase with AC",
            AcceptanceCriteria =
            [
                new AcceptanceCriterionDto
                {
                    Name = "Tests pass",
                    Description = "All unit tests green",
                    VerificationMethod = Shared.Enums.VerificationMethod.AutomatedTest
                },
                new AcceptanceCriterionDto
                {
                    Name = "Manual check",
                    Description = "Visually verified",
                    VerificationMethod = Shared.Enums.VerificationMethod.Manual
                }
            ]
        }, ct);

        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

        Assert.Equal(2, phase!.AcceptanceCriteria.Count);
        Assert.Equal("Tests pass", phase.AcceptanceCriteria[0].Name);
        Assert.Equal("AutomatedTest", phase.AcceptanceCriteria[0].VerificationMethod.ToString());
    }

    [Fact]
    public async Task Post_WithBatchTasks_CreatesTasksWithSequentialNumbers()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);

        var response = await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest
        {
            Name = "Phase with tasks",
            Tasks =
            [
                new CreateTaskRequest { Name = "Task A", Description = "First" },
                new CreateTaskRequest { Name = "Task B", Description = "Second" },
                new CreateTaskRequest { Name = "Task C", Description = "Third" }
            ]
        }, ct);

        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);

        Assert.Equal(3, phase!.Tasks.Count);
        Assert.Equal(1, phase.Tasks[0].TaskNumber);
        Assert.Equal(2, phase.Tasks[1].TaskNumber);
        Assert.Equal(3, phase.Tasks[2].TaskNumber);
        Assert.Equal("Task A", phase.Tasks[0].Name);
    }

    [Fact]
    public async Task Patch_UpdatesPhaseFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);
        await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest { Name = "Old" }, ct);

        var response = await Client.PatchAsJsonAsync($"{PhasePath(projectId, wpNumber)}/1",
            new UpdatePhaseRequest { Name = "Renamed", Description = "New desc" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);
        Assert.Equal("Renamed", phase!.Name);
        Assert.Equal("New desc", phase.Description);
    }

    [Fact]
    public async Task Patch_ReplacesAcceptanceCriteria()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);
        await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest
        {
            Name = "P1",
            AcceptanceCriteria =
            [
                new AcceptanceCriterionDto { Name = "Original", Description = "Original AC", VerificationMethod = Shared.Enums.VerificationMethod.Manual }
            ]
        }, ct);

        // Replace with new AC
        var response = await Client.PatchAsJsonAsync($"{PhasePath(projectId, wpNumber)}/1", new UpdatePhaseRequest
        {
            AcceptanceCriteria =
            [
                new AcceptanceCriterionDto { Name = "Replaced", Description = "New AC", VerificationMethod = Shared.Enums.VerificationMethod.AutomatedTest }
            ]
        }, ct);

        var phase = await response.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);
        Assert.Single(phase!.AcceptanceCriteria);
        Assert.Equal("Replaced", phase.AcceptanceCriteria[0].Name);
    }

    [Fact]
    public async Task Delete_RemovesPhase_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);
        await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest { Name = "Doomed" }, ct);

        var response = await Client.DeleteAsync($"{PhasePath(projectId, wpNumber)}/1", ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);

        var response = await Client.DeleteAsync($"{PhasePath(projectId, wpNumber)}/999", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Batch task creation in second phase uses correct numbering ──

    [Fact]
    public async Task Post_BatchTasks_SecondPhase_ContinuesTaskNumbering()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await CreateProjectAndWpAsync(ct);

        // Phase 1 with 2 tasks
        await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest
        {
            Name = "Phase 1",
            Tasks =
            [
                new CreateTaskRequest { Name = "T1", Description = "d" },
                new CreateTaskRequest { Name = "T2", Description = "d" }
            ]
        }, ct);

        // Phase 2 with 1 task — task number should be 3 (per-WP sequential)
        var r2 = await Client.PostAsJsonAsync(PhasePath(projectId, wpNumber), new CreatePhaseRequest
        {
            Name = "Phase 2",
            Tasks = [new CreateTaskRequest { Name = "T3", Description = "d" }]
        }, ct);

        var phase2 = await r2.Content.ReadFromJsonAsync<PhaseResponse>(JsonOptions, ct);
        Assert.Single(phase2!.Tasks);
        Assert.Equal(3, phase2.Tasks[0].TaskNumber);
    }
}
