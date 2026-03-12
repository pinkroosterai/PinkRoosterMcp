using PinkRooster.Api.Services;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.Enums;
using Xunit;

namespace PinkRooster.UnitTests;

public sealed class StateTransitionHelperTests
{
    private sealed class FakeTimestampEntity : IHasStateTimestamps
    {
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
    }

    private sealed class FakeBlockedEntity : IHasBlockedState
    {
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
        public CompletionState? PreviousActiveState { get; set; }
    }

    // ── ApplyStateTimestamps: CompletionState ──

    [Fact]
    public void SameState_IsNoOp()
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyStateTimestamps(entity, CompletionState.Implementing, CompletionState.Implementing);

        Assert.Null(entity.StartedAt);
        Assert.Null(entity.CompletedAt);
        Assert.Null(entity.ResolvedAt);
    }

    [Theory]
    [InlineData(CompletionState.Designing)]
    [InlineData(CompletionState.Implementing)]
    [InlineData(CompletionState.Testing)]
    [InlineData(CompletionState.InReview)]
    public void NotStartedToActive_SetsStartedAt(CompletionState activeState)
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyStateTimestamps(entity, CompletionState.NotStarted, activeState);

        Assert.NotNull(entity.StartedAt);
        Assert.Null(entity.CompletedAt);
        Assert.Null(entity.ResolvedAt);
    }

    [Fact]
    public void StartedAt_SetOnlyOnce()
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyStateTimestamps(entity, CompletionState.NotStarted, CompletionState.Implementing);
        var first = entity.StartedAt;

        StateTransitionHelper.ApplyStateTimestamps(entity, CompletionState.Implementing, CompletionState.Testing);
        Assert.Equal(first, entity.StartedAt);
    }

    [Fact]
    public void ToCompleted_SetsCompletedAtAndResolvedAt()
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyStateTimestamps(entity, CompletionState.Implementing, CompletionState.Completed);

        Assert.NotNull(entity.CompletedAt);
        Assert.NotNull(entity.ResolvedAt);
    }

    [Theory]
    [InlineData(CompletionState.Cancelled)]
    [InlineData(CompletionState.Replaced)]
    public void ToOtherTerminal_SetsResolvedAtOnly(CompletionState terminal)
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyStateTimestamps(entity, CompletionState.Implementing, terminal);

        Assert.Null(entity.CompletedAt);
        Assert.NotNull(entity.ResolvedAt);
    }

    [Fact]
    public void FromTerminalToActive_ClearsCompletedAtAndResolvedAt()
    {
        var entity = new FakeTimestampEntity
        {
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            ResolvedAt = DateTimeOffset.UtcNow
        };

        StateTransitionHelper.ApplyStateTimestamps(entity, CompletionState.Completed, CompletionState.Implementing);

        Assert.NotNull(entity.StartedAt); // Preserved
        Assert.Null(entity.CompletedAt);  // Cleared
        Assert.Null(entity.ResolvedAt);   // Cleared
    }

    // ── ApplyFeatureStatusTimestamps ──

    [Fact]
    public void FeatureStatus_SameState_IsNoOp()
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyFeatureStatusTimestamps(entity, FeatureStatus.InProgress, FeatureStatus.InProgress);

        Assert.Null(entity.StartedAt);
    }

    [Theory]
    [InlineData(FeatureStatus.UnderReview)]
    [InlineData(FeatureStatus.Approved)]
    [InlineData(FeatureStatus.Scheduled)]
    [InlineData(FeatureStatus.InProgress)]
    public void FeatureStatus_ToActive_SetsStartedAt(FeatureStatus active)
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyFeatureStatusTimestamps(entity, FeatureStatus.Proposed, active);

        Assert.NotNull(entity.StartedAt);
    }

    [Fact]
    public void FeatureStatus_ToCompleted_SetsCompletedAndResolved()
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyFeatureStatusTimestamps(entity, FeatureStatus.InProgress, FeatureStatus.Completed);

        Assert.NotNull(entity.CompletedAt);
        Assert.NotNull(entity.ResolvedAt);
    }

    [Fact]
    public void FeatureStatus_ToRejected_SetsResolvedOnly()
    {
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyFeatureStatusTimestamps(entity, FeatureStatus.InProgress, FeatureStatus.Rejected);

        Assert.Null(entity.CompletedAt);
        Assert.NotNull(entity.ResolvedAt);
    }

    [Fact]
    public void FeatureStatus_ToDeferred_NoTimestamps()
    {
        // Deferred is Inactive, not Terminal
        var entity = new FakeTimestampEntity();
        StateTransitionHelper.ApplyFeatureStatusTimestamps(entity, FeatureStatus.InProgress, FeatureStatus.Deferred);

        Assert.Null(entity.CompletedAt);
        Assert.Null(entity.ResolvedAt);
    }

    // ── ApplyBlockedStateLogic ──

    [Fact]
    public void ToBlocked_FromActive_CapturesPreviousState()
    {
        var entity = new FakeBlockedEntity();
        StateTransitionHelper.ApplyBlockedStateLogic(entity, CompletionState.Implementing, CompletionState.Blocked);

        Assert.Equal(CompletionState.Implementing, entity.PreviousActiveState);
    }

    [Fact]
    public void FromBlocked_ClearsPreviousState()
    {
        var entity = new FakeBlockedEntity { PreviousActiveState = CompletionState.Testing };
        StateTransitionHelper.ApplyBlockedStateLogic(entity, CompletionState.Blocked, CompletionState.Testing);

        Assert.Null(entity.PreviousActiveState);
    }

    [Fact]
    public void ToBlocked_FromInactive_DoesNotCapture()
    {
        var entity = new FakeBlockedEntity();
        StateTransitionHelper.ApplyBlockedStateLogic(entity, CompletionState.NotStarted, CompletionState.Blocked);

        Assert.Null(entity.PreviousActiveState);
    }

    // ── MapFileReferences ──

    [Fact]
    public void MapFileReferences_NullInput_ReturnsEmpty()
    {
        var result = StateTransitionHelper.MapFileReferences(null);
        Assert.Empty(result);
    }

    [Fact]
    public void MapFileReferences_EmptyList_ReturnsEmpty()
    {
        var result = StateTransitionHelper.MapFileReferences([]);
        Assert.Empty(result);
    }

    [Fact]
    public void MapFileReferences_ValidInput_MapsCorrectly()
    {
        var dtos = new List<PinkRooster.Shared.DTOs.Requests.FileReferenceDto>
        {
            new() { FileName = "test.cs", RelativePath = "src/test.cs", Description = "desc" }
        };

        var result = StateTransitionHelper.MapFileReferences(dtos);

        Assert.Single(result);
        Assert.Equal("test.cs", result[0].FileName);
        Assert.Equal("src/test.cs", result[0].RelativePath);
        Assert.Equal("desc", result[0].Description);
    }

    [Fact]
    public void MapFileReferences_PathTraversal_Throws()
    {
        var dtos = new List<PinkRooster.Shared.DTOs.Requests.FileReferenceDto>
        {
            new() { FileName = "test.cs", RelativePath = "../etc/passwd" }
        };

        Assert.Throws<ArgumentException>(() => StateTransitionHelper.MapFileReferences(dtos));
    }

    [Fact]
    public void MapFileReferences_AbsolutePath_Throws()
    {
        var dtos = new List<PinkRooster.Shared.DTOs.Requests.FileReferenceDto>
        {
            new() { FileName = "test.cs", RelativePath = "/etc/passwd" }
        };

        Assert.Throws<ArgumentException>(() => StateTransitionHelper.MapFileReferences(dtos));
    }
}
