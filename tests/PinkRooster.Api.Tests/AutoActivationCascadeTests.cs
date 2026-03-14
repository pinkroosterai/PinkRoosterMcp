using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class AutoActivationCascadeTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private async Task<(long ProjectId, int WpNumber)> SetupWpWithTaskAsync(CancellationToken ct)
    {
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var wp = await TestHelpers.CreateWorkPackageAsync(Client, projectId, ct);

        // Create a phase with a task via scaffold
        var scaffold = new ScaffoldWorkPackageRequest
        {
            Name = "Cascade Test WP",
            Description = "Test",
            Phases =
            [
                new ScaffoldPhaseRequest
                {
                    Name = "Phase 1",
                    Tasks =
                    [
                        new ScaffoldTaskRequest { Name = "Task 1", Description = "test task" }
                    ]
                }
            ]
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/scaffold", scaffold, ct);
        response.EnsureSuccessStatusCode();
        var result = await ReadJson<ScaffoldWorkPackageResponse>(response, ct);

        // Extract WP number from the ID (e.g., "proj-1-wp-2" → 2)
        var wpNum = int.Parse(result.WorkPackageId.Split("-wp-")[1]);
        return (projectId, wpNum);
    }

    [Theory]
    [InlineData(CompletionState.Implementing)]
    [InlineData(CompletionState.Designing)]
    [InlineData(CompletionState.Testing)]
    public async Task Task_TransitionToActive_AutoActivatesPhase(CompletionState activeState)
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await SetupWpWithTaskAsync(ct);

        // Verify phase starts as NotStarted
        var wpBefore = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);
        Assert.Equal("NotStarted", wpBefore.Phases![0].State);

        // Update task to active state
        var taskResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/1",
            new UpdateTaskRequest { State = activeState }, ct);
        taskResponse.EnsureSuccessStatusCode();
        var taskResult = await ReadJson<TaskResponse>(taskResponse, ct);

        // Verify phase auto-activated
        var wpAfter = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);
        Assert.Equal(activeState.ToString(), wpAfter.Phases![0].State);

        // Verify state change was reported
        Assert.NotNull(taskResult.StateChanges);
        Assert.Contains(taskResult.StateChanges, sc =>
            sc.EntityType == "Phase" && sc.NewState == activeState.ToString()
            && sc.Reason!.Contains("Auto-activated"));
    }

    [Fact]
    public async Task Task_TransitionToActive_AutoActivatesWP()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await SetupWpWithTaskAsync(ct);

        // Verify WP starts as NotStarted
        var wpBefore = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);
        Assert.Equal("NotStarted", wpBefore.State);

        // Update task to Implementing
        var taskResponse = await Client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/1",
            new UpdateTaskRequest { State = CompletionState.Implementing }, ct);
        taskResponse.EnsureSuccessStatusCode();
        var taskResult = await ReadJson<TaskResponse>(taskResponse, ct);

        // Verify WP auto-activated
        var wpAfter = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);
        Assert.Equal("Implementing", wpAfter.State);

        // Verify state change was reported
        Assert.NotNull(taskResult.StateChanges);
        Assert.Contains(taskResult.StateChanges, sc =>
            sc.EntityType == "WorkPackage" && sc.NewState == "Implementing"
            && sc.Reason!.Contains("Auto-activated"));
    }

    [Fact]
    public async Task Task_TransitionToActive_DoesNotOverrideAlreadyActivePhase()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await SetupWpWithTaskAsync(ct);

        // First, activate the WP and phase by moving task to Designing
        await Client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/1",
            new UpdateTaskRequest { State = CompletionState.Designing }, ct);

        // Verify phase is Designing
        var wpMid = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);
        Assert.Equal("Designing", wpMid.Phases![0].State);

        // Move task to Testing — phase should stay Designing (already active)
        await Client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/1",
            new UpdateTaskRequest { State = CompletionState.Testing }, ct);

        var wpAfter = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);
        Assert.Equal("Designing", wpAfter.Phases![0].State);
    }

    [Fact]
    public async Task Task_TransitionToActive_ActivatesPhaseWithMixedTaskStates()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        // Scaffold WP with a phase containing 2 tasks
        var scaffold = new ScaffoldWorkPackageRequest
        {
            Name = "Mixed State Test",
            Description = "Test",
            Phases =
            [
                new ScaffoldPhaseRequest
                {
                    Name = "Phase 1",
                    Tasks =
                    [
                        new ScaffoldTaskRequest { Name = "Task A", Description = "first" },
                        new ScaffoldTaskRequest { Name = "Task B", Description = "second" }
                    ]
                }
            ]
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/scaffold", scaffold, ct);
        response.EnsureSuccessStatusCode();
        var result = await ReadJson<ScaffoldWorkPackageResponse>(response, ct);
        var wpNum = int.Parse(result.WorkPackageId.Split("-wp-")[1]);

        // Move only Task A to Implementing — Task B stays NotStarted
        await Client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNum}/tasks/1",
            new UpdateTaskRequest { State = CompletionState.Implementing }, ct);

        // Phase and WP should auto-activate even though Task B is still NotStarted
        var wpAfter = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNum}", ct);
        Assert.Equal("Implementing", wpAfter.Phases![0].State);
        Assert.Equal("Implementing", wpAfter.State);
    }

    [Fact]
    public async Task WP_AutoActivation_SetsStartedAtTimestamp()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber) = await SetupWpWithTaskAsync(ct);

        // Verify no StartedAt initially
        var wpBefore = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);
        Assert.Null(wpBefore.StartedAt);

        // Activate via task
        await Client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/work-packages/{wpNumber}/tasks/1",
            new UpdateTaskRequest { State = CompletionState.Implementing }, ct);

        // Verify StartedAt is set
        var wpAfter = await GetJson<WorkPackageResponse>(
            $"/api/projects/{projectId}/work-packages/{wpNumber}", ct);
        Assert.NotNull(wpAfter.StartedAt);
    }
}
