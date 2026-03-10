using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class TaskEndpointTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private const string BasePath = "/api/projects";

    private async Task<(long ProjectId, int WpNumber, long WpId)> SetupAsync(CancellationToken ct, bool createPhase = true)
    {
        var projResponse = await Client.PutAsJsonAsync(BasePath, new CreateOrUpdateProjectRequest
        {
            Name = "TestProject",
            Description = "Test",
            ProjectPath = $"/tmp/task-test-{Guid.NewGuid():N}"
        }, ct);
        var project = await projResponse.Content.ReadFromJsonAsync<ProjectResponse>(ct);

        var wpResponse = await Client.PostAsJsonAsync($"{BasePath}/{project!.Id}/work-packages", new CreateWorkPackageRequest
        {
            Name = "Test WP",
            Description = "Test"
        }, ct);
        var wp = await wpResponse.Content.ReadFromJsonAsync<WorkPackageResponse>(ct);

        if (createPhase)
        {
            await Client.PostAsJsonAsync(
                $"{BasePath}/{project.Id}/work-packages/1/phases",
                new CreatePhaseRequest { Name = "Phase 1" }, ct);
        }

        return (project.Id, 1, wp!.Id);
    }

    private string TaskPath(long projectId, int wpNumber) =>
        $"{BasePath}/{projectId}/work-packages/{wpNumber}/tasks";

    // ── CRUD ──

    [Fact]
    public async Task Post_CreatesTask_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        var response = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "Task 1", Description = "Do something" }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(ct);

        Assert.NotNull(task);
        Assert.Equal(1, task.TaskNumber);
        Assert.Equal("Task 1", task.Name);
        Assert.Equal("NotStarted", task.State);
    }

    [Fact]
    public async Task Post_WithActiveState_SetsStartedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        var response = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest
            {
                Name = "Active task",
                Description = "d",
                State = Shared.Enums.CompletionState.Implementing
            }, ct);

        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(ct);
        Assert.Equal("Implementing", task!.State);
        Assert.NotNull(task.StartedAt);
    }

    [Fact]
    public async Task Patch_UpdatesTaskFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);
        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "Original", Description = "d" }, ct);

        var response = await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/1",
            new UpdateTaskRequest { Name = "Updated", ImplementationNotes = "Some notes" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(ct);
        Assert.Equal("Updated", task!.Name);
        Assert.Equal("Some notes", task.ImplementationNotes);
    }

    [Fact]
    public async Task Patch_StateToCompleted_SetsTimestamps()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);
        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T", Description = "d", State = Shared.Enums.CompletionState.Implementing }, ct);

        var response = await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/1",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Completed }, ct);

        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(ct);
        Assert.Equal("Completed", task!.State);
        Assert.NotNull(task.StartedAt);
        Assert.NotNull(task.CompletedAt);
        Assert.NotNull(task.ResolvedAt);
    }

    [Fact]
    public async Task Patch_Returns404_WhenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        var response = await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/999",
            new UpdateTaskRequest { Name = "x" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTask_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);
        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "Doomed", Description = "d" }, ct);

        var response = await Client.DeleteAsync($"{TaskPath(projectId, wpNumber)}/1", ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── Task Dependencies ──

    [Fact]
    public async Task AddDependency_Returns201_AndAppearsInBlockedBy()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        var r1 = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T1", Description = "d" }, ct);
        var t1 = await r1.Content.ReadFromJsonAsync<TaskResponse>(ct);
        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T2", Description = "d" }, ct);

        var depResponse = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = t1!.Id, Reason = "T2 needs T1" }, ct);

        Assert.Equal(HttpStatusCode.Created, depResponse.StatusCode);

        // Verify via WP detail (tasks include dependency info)
        var wp = await GetJson<WorkPackageResponse>($"{BasePath}/{projectId}/work-packages/1", ct);
        var t2 = wp.Phases[0].Tasks.First(t => t.TaskNumber == 2);
        Assert.Single(t2.BlockedBy);
        Assert.Equal("T1", t2.BlockedBy[0].Name);
    }

    [Fact]
    public async Task AddDependency_AutoBlocksActiveTask()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        var r1 = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T1", Description = "d" }, ct);
        var t1 = await r1.Content.ReadFromJsonAsync<TaskResponse>(ct);

        // T2 in active state
        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T2", Description = "d", State = Shared.Enums.CompletionState.Implementing }, ct);

        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = t1!.Id }, ct);

        var wp = await GetJson<WorkPackageResponse>($"{BasePath}/{projectId}/work-packages/1", ct);
        var t2 = wp.Phases[0].Tasks.First(t => t.TaskNumber == 2);
        Assert.Equal("Blocked", t2.State);
        Assert.Equal("Implementing", t2.PreviousActiveState);
    }

    [Fact]
    public async Task RemoveDependency_AutoUnblocksTask()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        var r1 = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T1", Description = "d" }, ct);
        var t1 = await r1.Content.ReadFromJsonAsync<TaskResponse>(ct);

        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T2", Description = "d", State = Shared.Enums.CompletionState.Designing }, ct);

        // Add then remove
        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = t1!.Id }, ct);
        await Client.DeleteAsync($"{TaskPath(projectId, wpNumber)}/2/dependencies/{t1.Id}", ct);

        var wp = await GetJson<WorkPackageResponse>($"{BasePath}/{projectId}/work-packages/1", ct);
        var t2 = wp.Phases[0].Tasks.First(t => t.TaskNumber == 2);
        Assert.Equal("Designing", t2.State);
        Assert.Null(t2.PreviousActiveState);
    }

    [Fact]
    public async Task AddDependency_CircularDependency_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        var r1 = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T1", Description = "d" }, ct);
        var t1 = await r1.Content.ReadFromJsonAsync<TaskResponse>(ct);
        var r2 = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "T2", Description = "d" }, ct);
        var t2 = await r2.Content.ReadFromJsonAsync<TaskResponse>(ct);

        // T2 depends on T1
        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = t1!.Id }, ct);

        // T1 depends on T2 → circular
        var response = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}/1/dependencies",
            new ManageDependencyRequest { DependsOnId = t2!.Id }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Upward Propagation ──

    [Fact]
    public async Task AllTasksCompleted_AutoCompletesPhase()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct, createPhase: false);

        // Create phase with 2 tasks
        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages/1/phases", new CreatePhaseRequest
        {
            Name = "P1",
            Tasks =
            [
                new CreateTaskRequest { Name = "T1", Description = "d" },
                new CreateTaskRequest { Name = "T2", Description = "d" }
            ]
        }, ct);

        // Complete both tasks
        await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/1",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Completed }, ct);
        await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Completed }, ct);

        var wp = await GetJson<WorkPackageResponse>($"{BasePath}/{projectId}/work-packages/1", ct);
        Assert.Equal("Completed", wp.Phases[0].State);
    }

    [Fact]
    public async Task AllPhasesCompleted_AutoCompletesWorkPackage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct, createPhase: false);

        // Phase 1 with 1 task
        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages/1/phases", new CreatePhaseRequest
        {
            Name = "P1",
            Tasks = [new CreateTaskRequest { Name = "T1", Description = "d" }]
        }, ct);

        // Phase 2 with 1 task
        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages/1/phases", new CreatePhaseRequest
        {
            Name = "P2",
            Tasks = [new CreateTaskRequest { Name = "T2", Description = "d" }]
        }, ct);

        // Complete both tasks (task numbering is per-WP: task 1 and task 2)
        await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/1",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Completed }, ct);
        await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Completed }, ct);

        var wp = await GetJson<WorkPackageResponse>($"{BasePath}/{projectId}/work-packages/1", ct);
        Assert.Equal("Completed", wp.State);
        Assert.NotNull(wp.CompletedAt);
        Assert.NotNull(wp.ResolvedAt);
        Assert.All(wp.Phases, p => Assert.Equal("Completed", p.State));
    }

    [Fact]
    public async Task MixedTerminalStates_StillAutoCompletesPhase()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct, createPhase: false);

        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages/1/phases", new CreatePhaseRequest
        {
            Name = "P1",
            Tasks =
            [
                new CreateTaskRequest { Name = "T1", Description = "d" },
                new CreateTaskRequest { Name = "T2", Description = "d" }
            ]
        }, ct);

        // One Completed, one Cancelled — both terminal
        await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/1",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Completed }, ct);
        await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Cancelled }, ct);

        var wp = await GetJson<WorkPackageResponse>($"{BasePath}/{projectId}/work-packages/1", ct);
        Assert.Equal("Completed", wp.Phases[0].State);
    }

    // ── Target Files and Attachments ──

    [Fact]
    public async Task Post_WithTargetFiles_PersistsJsonb()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        var response = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest
            {
                Name = "Task with files",
                Description = "d",
                TargetFiles =
                [
                    new FileReferenceDto { FileName = "Program.cs", RelativePath = "src/", Description = "Entry point" }
                ]
            }, ct);

        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(ct);
        Assert.Single(task!.TargetFiles);
        Assert.Equal("Program.cs", task.TargetFiles[0].FileName);
    }

    // ── State Change Cascade Reporting ──

    [Fact]
    public async Task Update_CompletingLastTask_ReturnsPhaseAndWpAutoCompleteStateChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct, createPhase: false);

        // Create a phase with a single task
        await Client.PostAsJsonAsync($"{BasePath}/{projectId}/work-packages/1/phases", new CreatePhaseRequest
        {
            Name = "SinglePhase",
            Tasks = [new CreateTaskRequest { Name = "OnlyTask", Description = "d" }]
        }, ct);

        // Complete the only task → should cascade: Phase auto-complete + WP auto-complete
        var updateResponse = await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/1",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Completed }, ct);
        var updatedTask = await updateResponse.Content.ReadFromJsonAsync<TaskResponse>(ct);

        Assert.NotNull(updatedTask!.StateChanges);
        Assert.Equal(2, updatedTask.StateChanges.Count);

        // First: phase auto-complete
        var phaseChange = updatedTask.StateChanges.First(sc => sc.EntityType == "Phase");
        Assert.Equal("Completed", phaseChange.NewState);
        Assert.Contains("all tasks reached terminal state", phaseChange.Reason);

        // Second: WP auto-complete
        var wpChange = updatedTask.StateChanges.First(sc => sc.EntityType == "WorkPackage");
        Assert.Equal("Completed", wpChange.NewState);
        Assert.Contains("all phases reached terminal state", wpChange.Reason);
    }

    [Fact]
    public async Task AddDependency_ReturnsAutoBlockStateChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await SetupAsync(ct);

        // Create two tasks in phase 1
        await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "Blocker", Description = "d" }, ct);
        var r2 = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest { Name = "Dependent", Description = "d" }, ct);

        // Move task 2 to Implementing
        await Client.PatchAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2",
            new UpdateTaskRequest { State = Shared.Enums.CompletionState.Implementing }, ct);

        // Get task 1 ID
        var wp = await GetJson<WorkPackageResponse>($"{BasePath}/{projectId}/work-packages/1", ct);
        var task1Id = wp.Phases[0].Tasks.First(t => t.TaskNumber == 1).Id;

        // Add dependency: task 2 blocked by task 1
        var addResponse = await Client.PostAsJsonAsync($"{TaskPath(projectId, wpNumber)}/2/dependencies",
            new ManageDependencyRequest { DependsOnId = task1Id }, ct);
        var dep = await addResponse.Content.ReadFromJsonAsync<TaskDependencyResponse>(ct);

        Assert.NotNull(dep!.StateChanges);
        Assert.Single(dep.StateChanges);
        Assert.Equal("Task", dep.StateChanges[0].EntityType);
        Assert.Equal("Implementing", dep.StateChanges[0].OldState);
        Assert.Equal("Blocked", dep.StateChanges[0].NewState);
        Assert.Contains("Auto-blocked", dep.StateChanges[0].Reason);
    }
}
