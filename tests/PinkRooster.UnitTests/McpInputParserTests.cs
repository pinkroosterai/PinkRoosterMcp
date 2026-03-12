using PinkRooster.Mcp.Helpers;
using PinkRooster.Mcp.Inputs;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.UnitTests;

public sealed class McpInputParserTests
{
    // ── IsTerminalState ──

    [Theory]
    [InlineData("Completed", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Replaced", true)]
    [InlineData("Implementing", false)]
    [InlineData("NotStarted", false)]
    [InlineData("Blocked", false)]
    [InlineData("", false)]
    [InlineData("completed", false)] // case-sensitive
    public void IsTerminalState_ReturnsExpected(string state, bool expected)
    {
        Assert.Equal(expected, McpInputParser.IsTerminalState(state));
    }

    // ── NullIfEmpty ──

    [Fact]
    public void NullIfEmpty_EmptyList_ReturnsNull()
    {
        Assert.Null(McpInputParser.NullIfEmpty(new List<int>()));
    }

    [Fact]
    public void NullIfEmpty_NonEmptyList_ReturnsSameList()
    {
        var list = new List<int> { 1, 2 };
        Assert.Same(list, McpInputParser.NullIfEmpty(list));
    }

    // ── MapUserStories ──

    [Fact]
    public void MapUserStories_Null_ReturnsNull()
    {
        Assert.Null(McpInputParser.MapUserStories(null));
    }

    [Fact]
    public void MapUserStories_MapsFields()
    {
        var inputs = new List<UserStoryInput>
        {
            new() { Role = "dev", Goal = "test code", Benefit = "fewer bugs" }
        };

        var result = McpInputParser.MapUserStories(inputs);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("dev", result[0].Role);
        Assert.Equal("test code", result[0].Goal);
        Assert.Equal("fewer bugs", result[0].Benefit);
    }

    // ── MapFileReferences ──

    [Fact]
    public void MapFileReferences_Null_ReturnsNull()
    {
        Assert.Null(McpInputParser.MapFileReferences(null));
    }

    [Fact]
    public void MapFileReferences_MapsAllFields()
    {
        var inputs = new List<FileReferenceInput>
        {
            new() { FileName = "test.cs", RelativePath = "src/test.cs", Description = "desc" }
        };

        var result = McpInputParser.MapFileReferences(inputs);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("test.cs", result[0].FileName);
        Assert.Equal("src/test.cs", result[0].RelativePath);
        Assert.Equal("desc", result[0].Description);
    }

    [Fact]
    public void MapFileReferences_NullDescription_MapsAsNull()
    {
        var inputs = new List<FileReferenceInput>
        {
            new() { FileName = "f.cs", RelativePath = "src/f.cs" }
        };

        var result = McpInputParser.MapFileReferences(inputs);

        Assert.NotNull(result);
        Assert.Null(result[0].Description);
    }

    // ── MapAcceptanceCriteria ──

    [Fact]
    public void MapAcceptanceCriteria_Null_ReturnsNull()
    {
        Assert.Null(McpInputParser.MapAcceptanceCriteria(null));
    }

    [Fact]
    public void MapAcceptanceCriteria_DefaultsToManual()
    {
        var inputs = new List<AcceptanceCriterionInput>
        {
            new() { Name = "criterion", Description = "must pass", VerificationMethod = null }
        };

        var result = McpInputParser.MapAcceptanceCriteria(inputs);

        Assert.NotNull(result);
        Assert.Equal(VerificationMethod.Manual, result[0].VerificationMethod);
    }

    [Fact]
    public void MapAcceptanceCriteria_UsesProvidedMethod()
    {
        var inputs = new List<AcceptanceCriterionInput>
        {
            new()
            {
                Name = "criterion",
                Description = "must pass",
                VerificationMethod = VerificationMethod.AutomatedTest
            }
        };

        var result = McpInputParser.MapAcceptanceCriteria(inputs);

        Assert.NotNull(result);
        Assert.Equal(VerificationMethod.AutomatedTest, result[0].VerificationMethod);
    }

    // ── MapCreateTasks ──

    [Fact]
    public void MapCreateTasks_Null_ReturnsNull()
    {
        Assert.Null(McpInputParser.MapCreateTasks(null));
    }

    [Fact]
    public void MapCreateTasks_DefaultsNameAndDescription()
    {
        var inputs = new List<PhaseTaskInput>
        {
            new() { Name = null, Description = null }
        };

        var result = McpInputParser.MapCreateTasks(inputs);

        Assert.NotNull(result);
        Assert.Equal("", result[0].Name);
        Assert.Equal("", result[0].Description);
    }

    [Fact]
    public void MapCreateTasks_DefaultsStateToNotStarted()
    {
        var inputs = new List<PhaseTaskInput>
        {
            new() { Name = "task", Description = "desc", State = null }
        };

        var result = McpInputParser.MapCreateTasks(inputs);

        Assert.NotNull(result);
        Assert.Equal(CompletionState.NotStarted, result[0].State);
    }

    [Fact]
    public void MapCreateTasks_MapsTargetFilesAndAttachments()
    {
        var inputs = new List<PhaseTaskInput>
        {
            new()
            {
                Name = "task",
                Description = "desc",
                TargetFiles = [new() { FileName = "a.cs", RelativePath = "src/a.cs" }],
                Attachments = [new() { FileName = "b.txt", RelativePath = "docs/b.txt" }]
            }
        };

        var result = McpInputParser.MapCreateTasks(inputs);

        Assert.NotNull(result);
        Assert.NotNull(result[0].TargetFiles);
        Assert.Single(result[0].TargetFiles);
        Assert.Equal("a.cs", result[0].TargetFiles[0].FileName);
        Assert.NotNull(result[0].Attachments);
        Assert.Single(result[0].Attachments);
        Assert.Equal("b.txt", result[0].Attachments[0].FileName);
    }

    // ── MapUpsertTasks ──

    [Fact]
    public void MapUpsertTasks_Null_ReturnsNull()
    {
        Assert.Null(McpInputParser.MapUpsertTasks(null));
    }

    [Fact]
    public void MapUpsertTasks_PreservesNullableFields()
    {
        var inputs = new List<PhaseTaskInput>
        {
            new() { TaskNumber = 3, Name = "updated", State = CompletionState.Implementing }
        };

        var result = McpInputParser.MapUpsertTasks(inputs);

        Assert.NotNull(result);
        Assert.Equal(3, result[0].TaskNumber);
        Assert.Equal("updated", result[0].Name);
        Assert.Null(result[0].Description); // null = don't change
        Assert.Equal(CompletionState.Implementing, result[0].State);
    }

    // ── MapScaffoldPhases ──

    [Fact]
    public void MapScaffoldPhases_MapsPhaseWithTasks()
    {
        var inputs = new List<ScaffoldPhaseInput>
        {
            new()
            {
                Name = "Phase 1",
                Description = "First phase",
                SortOrder = 1,
                AcceptanceCriteria =
                [
                    new() { Name = "AC1", Description = "Must work" }
                ],
                Tasks =
                [
                    new()
                    {
                        Name = "Task A",
                        Description = "Do A",
                        SortOrder = 1,
                        DependsOnTaskIndices = [0]
                    }
                ]
            }
        };

        var result = McpInputParser.MapScaffoldPhases(inputs);

        Assert.Single(result);
        Assert.Equal("Phase 1", result[0].Name);
        Assert.Equal("First phase", result[0].Description);
        Assert.NotNull(result[0].AcceptanceCriteria);
        Assert.Single(result[0].AcceptanceCriteria);
        Assert.NotNull(result[0].Tasks);
        Assert.Single(result[0].Tasks);
        Assert.Equal("Task A", result[0].Tasks[0].Name);
        Assert.Equal(new List<int> { 0 }, result[0].Tasks[0].DependsOnTaskIndices);
    }

    [Fact]
    public void MapScaffoldPhases_NullTasks_MapsAsNull()
    {
        var inputs = new List<ScaffoldPhaseInput>
        {
            new() { Name = "Empty phase", Tasks = null }
        };

        var result = McpInputParser.MapScaffoldPhases(inputs);

        Assert.Single(result);
        Assert.Null(result[0].Tasks);
    }

    [Fact]
    public void MapScaffoldPhases_TaskDefaultsStateToNotStarted()
    {
        var inputs = new List<ScaffoldPhaseInput>
        {
            new()
            {
                Name = "Phase",
                Tasks = [new() { Name = "T", Description = "D", State = null }]
            }
        };

        var result = McpInputParser.MapScaffoldPhases(inputs);

        Assert.Equal(CompletionState.NotStarted, result[0].Tasks![0].State);
    }
}
