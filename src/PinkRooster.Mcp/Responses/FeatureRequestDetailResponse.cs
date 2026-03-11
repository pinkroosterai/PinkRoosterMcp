using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Mcp.Responses;

public sealed class FeatureRequestDetailResponse
{
    public required string FeatureRequestId { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string Priority { get; init; }
    public required string Status { get; init; }
    public string? BusinessValue { get; init; }
    public List<UserStoryDto>? UserStories { get; init; }
    public string? Requester { get; init; }
    public string? AcceptanceSummary { get; init; }
    public required List<FileReferenceDto> Attachments { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<LinkedWorkPackageItem>? LinkedWorkPackages { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
