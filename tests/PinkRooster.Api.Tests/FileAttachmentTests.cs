using System.Net;
using System.Net.Http.Json;
using PinkRooster.Api.Tests.Fixtures;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.Api.Tests;

public sealed class FileAttachmentTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    private static readonly FileReferenceDto SampleAttachment = new()
    {
        FileName = "bug-screenshot.png",
        RelativePath = "docs/screenshots/bug-screenshot.png",
        Description = "Screenshot of the bug"
    };

    private static readonly FileReferenceDto SampleTargetFile = new()
    {
        FileName = "UserService.cs",
        RelativePath = "src/Services/UserService.cs",
        Description = "Main service to modify"
    };

    // ── Issue Attachments ──

    [Fact]
    public async Task Issue_CreateWithAttachments_ReturnsAttachments()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var response = await Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Issue with attachment",
            Description = "Has files",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major,
            Attachments = [SampleAttachment]
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var issue = await ReadJson<IssueResponse>(response, ct);

        Assert.Single(issue.Attachments);
        Assert.Equal("bug-screenshot.png", issue.Attachments[0].FileName);
        Assert.Equal("docs/screenshots/bug-screenshot.png", issue.Attachments[0].RelativePath);
        Assert.Equal("Screenshot of the bug", issue.Attachments[0].Description);
    }

    [Fact]
    public async Task Issue_CreateWithoutAttachments_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);
        var issue = await TestHelpers.CreateIssueAsync(Client, projectId, ct);

        var detail = await GetJson<IssueResponse>(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}", ct);

        Assert.Empty(detail.Attachments);
    }

    [Fact]
    public async Task Issue_UpdateAttachments_ReplacesExisting()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        // Create with one attachment
        var createResponse = await Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Issue",
            Description = "d",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major,
            Attachments = [SampleAttachment]
        }, ct);
        var issue = await ReadJson<IssueResponse>(createResponse, ct);

        // Update with a different attachment
        var newAttachment = new FileReferenceDto
        {
            FileName = "fix.patch",
            RelativePath = "patches/fix.patch",
            Description = "Proposed fix"
        };
        await Client.PatchAsJsonAsync(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}",
            new UpdateIssueRequest { Attachments = [newAttachment] }, ct);

        // Read back
        var updated = await GetJson<IssueResponse>(
            $"{TestHelpers.IssuePath(projectId)}/{issue.IssueNumber}", ct);

        Assert.Single(updated.Attachments);
        Assert.Equal("fix.patch", updated.Attachments[0].FileName);
    }

    [Fact]
    public async Task Issue_MultipleAttachments_AllReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await TestHelpers.CreateProjectAsync(Client, ct);

        var attachments = new List<FileReferenceDto>
        {
            new() { FileName = "file1.txt", RelativePath = "docs/file1.txt" },
            new() { FileName = "file2.txt", RelativePath = "docs/file2.txt", Description = "Second file" }
        };

        var response = await Client.PostAsJsonAsync(TestHelpers.IssuePath(projectId), new CreateIssueRequest
        {
            Name = "Issue with multiple",
            Description = "d",
            IssueType = IssueType.Bug,
            Severity = IssueSeverity.Major,
            Attachments = attachments
        }, ct);

        var issue = await ReadJson<IssueResponse>(response, ct);

        Assert.Equal(2, issue.Attachments.Count);
    }

    // ── Task TargetFiles and Attachments ──

    [Fact]
    public async Task Task_CreateWithTargetFiles_ReturnsTargetFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await TestHelpers.CreateProjectWpAndPhaseAsync(Client, ct);

        var response = await Client.PostAsJsonAsync(
            $"{TestHelpers.TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest
            {
                Name = "Task with targets",
                Description = "Has target files",
                TargetFiles = [SampleTargetFile]
            }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await ReadJson<TaskResponse>(response, ct);

        Assert.Single(task.TargetFiles);
        Assert.Equal("UserService.cs", task.TargetFiles[0].FileName);
        Assert.Equal("src/Services/UserService.cs", task.TargetFiles[0].RelativePath);
    }

    [Fact]
    public async Task Task_CreateWithAttachments_ReturnsAttachments()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await TestHelpers.CreateProjectWpAndPhaseAsync(Client, ct);

        var response = await Client.PostAsJsonAsync(
            $"{TestHelpers.TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest
            {
                Name = "Task with attachments",
                Description = "Has attachments",
                Attachments = [SampleAttachment]
            }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await ReadJson<TaskResponse>(response, ct);

        Assert.Single(task.Attachments);
        Assert.Equal("bug-screenshot.png", task.Attachments[0].FileName);
    }

    [Fact]
    public async Task Task_UpdateTargetFiles_ReplacesExisting()
    {
        var ct = TestContext.Current.CancellationToken;
        var (projectId, wpNumber, _) = await TestHelpers.CreateProjectWpAndPhaseAsync(Client, ct);

        // Create task with target file
        var createResponse = await Client.PostAsJsonAsync(
            $"{TestHelpers.TaskPath(projectId, wpNumber)}?phaseNumber=1",
            new CreateTaskRequest
            {
                Name = "Task",
                Description = "d",
                TargetFiles = [SampleTargetFile]
            }, ct);
        var task = await ReadJson<TaskResponse>(createResponse, ct);

        // Update with different target file
        var newTarget = new FileReferenceDto
        {
            FileName = "Controller.cs",
            RelativePath = "src/Controllers/Controller.cs"
        };
        await Client.PatchAsJsonAsync(
            $"{TestHelpers.TaskPath(projectId, wpNumber)}/{task.TaskNumber}",
            new UpdateTaskRequest { TargetFiles = [newTarget] }, ct);

        // Read back via WP detail
        var wpDetail = await GetJson<WorkPackageResponse>(
            $"{TestHelpers.WpPath(projectId)}/{wpNumber}", ct);
        var updatedTask = wpDetail.Phases[0].Tasks[0];

        Assert.Single(updatedTask.TargetFiles);
        Assert.Equal("Controller.cs", updatedTask.TargetFiles[0].FileName);
    }
}
