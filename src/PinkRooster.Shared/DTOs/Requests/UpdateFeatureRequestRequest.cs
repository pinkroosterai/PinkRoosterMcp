using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class UpdateFeatureRequestRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public FeatureCategory? Category { get; init; }
    public Priority? Priority { get; init; }
    public FeatureStatus? Status { get; init; }
    public string? BusinessValue { get; init; }
    public string? UserStory { get; init; }
    public string? Requester { get; init; }
    public string? AcceptanceSummary { get; init; }
    public List<FileReferenceDto>? Attachments { get; init; }
}
