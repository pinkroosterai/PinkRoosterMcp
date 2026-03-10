using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class CreateWorkPackageRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public WorkPackageType Type { get; set; } = WorkPackageType.Feature;
    public Priority Priority { get; set; } = Priority.Medium;
    public string? Plan { get; set; }
    public int? EstimatedComplexity { get; set; }
    public string? EstimationRationale { get; set; }
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public long? LinkedIssueId { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
