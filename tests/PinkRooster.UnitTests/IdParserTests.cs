using PinkRooster.Shared.Helpers;
using Xunit;

namespace PinkRooster.UnitTests;

public sealed class IdParserTests
{
    // ── TryParseProjectId ──

    [Theory]
    [InlineData("proj-1", true, 1L)]
    [InlineData("proj-42", true, 42L)]
    [InlineData("proj-999", true, 999L)]
    public void TryParseProjectId_ValidIds(string input, bool expected, long expectedId)
    {
        var result = IdParser.TryParseProjectId(input, out var projectId);
        Assert.Equal(expected, result);
        Assert.Equal(expectedId, projectId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("project-1")]
    [InlineData("proj-")]
    [InlineData("proj-0")]
    [InlineData("proj--1")]
    [InlineData("proj-abc")]
    [InlineData("PROJ-1")]
    [InlineData("proj-1-issue-2")]
    public void TryParseProjectId_InvalidIds(string input)
    {
        var result = IdParser.TryParseProjectId(input, out _);
        Assert.False(result);
    }

    // ── TryParseIssueId ──

    [Theory]
    [InlineData("proj-1-issue-1", true, 1L, 1)]
    [InlineData("proj-5-issue-42", true, 5L, 42)]
    public void TryParseIssueId_ValidIds(string input, bool expected, long expectedProjId, int expectedNum)
    {
        var result = IdParser.TryParseIssueId(input, out var projId, out var num);
        Assert.Equal(expected, result);
        Assert.Equal(expectedProjId, projId);
        Assert.Equal(expectedNum, num);
    }

    [Theory]
    [InlineData("")]
    [InlineData("proj-1")]
    [InlineData("proj-1-issue-")]
    [InlineData("proj-1-issue-0")]
    [InlineData("proj-0-issue-1")]
    [InlineData("proj-1-bug-1")]
    [InlineData("proj-abc-issue-1")]
    public void TryParseIssueId_InvalidIds(string input)
    {
        var result = IdParser.TryParseIssueId(input, out _, out _);
        Assert.False(result);
    }

    // ── TryParseWorkPackageId ──

    [Theory]
    [InlineData("proj-1-wp-1", true, 1L, 1)]
    [InlineData("proj-3-wp-7", true, 3L, 7)]
    public void TryParseWorkPackageId_ValidIds(string input, bool expected, long expectedProjId, int expectedNum)
    {
        var result = IdParser.TryParseWorkPackageId(input, out var projId, out var num);
        Assert.Equal(expected, result);
        Assert.Equal(expectedProjId, projId);
        Assert.Equal(expectedNum, num);
    }

    [Theory]
    [InlineData("")]
    [InlineData("proj-1")]
    [InlineData("proj-1-wp-0")]
    [InlineData("proj-1-wp-1-phase-1")] // Should not match — has extra segments
    [InlineData("proj-1-wp-1-task-1")]  // Should not match — has extra segments
    public void TryParseWorkPackageId_InvalidIds(string input)
    {
        var result = IdParser.TryParseWorkPackageId(input, out _, out _);
        Assert.False(result);
    }

    // ── TryParsePhaseId ──

    [Theory]
    [InlineData("proj-1-wp-1-phase-1", true, 1L, 1, 1)]
    [InlineData("proj-2-wp-3-phase-5", true, 2L, 3, 5)]
    public void TryParsePhaseId_ValidIds(string input, bool expected, long expectedProjId, int expectedWp, int expectedPhase)
    {
        var result = IdParser.TryParsePhaseId(input, out var projId, out var wpNum, out var phaseNum);
        Assert.Equal(expected, result);
        Assert.Equal(expectedProjId, projId);
        Assert.Equal(expectedWp, wpNum);
        Assert.Equal(expectedPhase, phaseNum);
    }

    [Theory]
    [InlineData("")]
    [InlineData("proj-1-wp-1")]
    [InlineData("proj-1-wp-1-phase-0")]
    [InlineData("proj-1-wp-0-phase-1")]
    public void TryParsePhaseId_InvalidIds(string input)
    {
        var result = IdParser.TryParsePhaseId(input, out _, out _, out _);
        Assert.False(result);
    }

    // ── TryParseTaskId ──

    [Theory]
    [InlineData("proj-1-wp-1-task-1", true, 1L, 1, 1)]
    [InlineData("proj-2-wp-3-task-10", true, 2L, 3, 10)]
    public void TryParseTaskId_ValidIds(string input, bool expected, long expectedProjId, int expectedWp, int expectedTask)
    {
        var result = IdParser.TryParseTaskId(input, out var projId, out var wpNum, out var taskNum);
        Assert.Equal(expected, result);
        Assert.Equal(expectedProjId, projId);
        Assert.Equal(expectedWp, wpNum);
        Assert.Equal(expectedTask, taskNum);
    }

    [Theory]
    [InlineData("")]
    [InlineData("proj-1-wp-1")]
    [InlineData("proj-1-wp-1-task-0")]
    [InlineData("proj-0-wp-1-task-1")]
    public void TryParseTaskId_InvalidIds(string input)
    {
        var result = IdParser.TryParseTaskId(input, out _, out _, out _);
        Assert.False(result);
    }

    // ── TryParseFeatureRequestId ──

    [Theory]
    [InlineData("proj-1-fr-1", true, 1L, 1)]
    [InlineData("proj-5-fr-99", true, 5L, 99)]
    public void TryParseFeatureRequestId_ValidIds(string input, bool expected, long expectedProjId, int expectedNum)
    {
        var result = IdParser.TryParseFeatureRequestId(input, out var projId, out var num);
        Assert.Equal(expected, result);
        Assert.Equal(expectedProjId, projId);
        Assert.Equal(expectedNum, num);
    }

    [Theory]
    [InlineData("")]
    [InlineData("proj-1")]
    [InlineData("proj-1-fr-0")]
    [InlineData("proj-0-fr-1")]
    [InlineData("proj-1-feature-1")]
    public void TryParseFeatureRequestId_InvalidIds(string input)
    {
        var result = IdParser.TryParseFeatureRequestId(input, out _, out _);
        Assert.False(result);
    }

    // ── TryParseProjectMemoryId ──

    [Theory]
    [InlineData("proj-1-mem-1", true, 1L, 1)]
    [InlineData("proj-3-mem-42", true, 3L, 42)]
    public void TryParseProjectMemoryId_ValidIds(string input, bool expected, long expectedProjId, int expectedNum)
    {
        var result = IdParser.TryParseProjectMemoryId(input, out var projId, out var num);
        Assert.Equal(expected, result);
        Assert.Equal(expectedProjId, projId);
        Assert.Equal(expectedNum, num);
    }

    [Theory]
    [InlineData("")]
    [InlineData("proj-1")]
    [InlineData("proj-1-mem-0")]
    [InlineData("proj-0-mem-1")]
    [InlineData("proj-1-memory-1")]
    public void TryParseProjectMemoryId_InvalidIds(string input)
    {
        var result = IdParser.TryParseProjectMemoryId(input, out _, out _);
        Assert.False(result);
    }
}
