using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class CreateFeatureRequestRequest
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required FeatureCategory Category { get; init; }
    public Priority Priority { get; init; } = Priority.Medium;
    public FeatureStatus Status { get; init; } = FeatureStatus.Proposed;
    public string? BusinessValue { get; init; }
    public string? UserStory { get; init; }
    public string? Requester { get; init; }
    public string? AcceptanceSummary { get; init; }
    public List<FileReferenceDto>? Attachments { get; init; }
}
