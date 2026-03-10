using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class UpdateWorkPackageRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public WorkPackageType? Type { get; set; }
    public Priority? Priority { get; set; }
    public string? Plan { get; set; }
    public int? EstimatedComplexity { get; set; }
    public string? EstimationRationale { get; set; }
    public CompletionState? State { get; set; }
    public long? LinkedIssueId { get; set; }
    public long? LinkedFeatureRequestId { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
