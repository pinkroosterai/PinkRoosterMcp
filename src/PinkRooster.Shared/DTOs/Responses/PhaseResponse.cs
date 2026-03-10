using PinkRooster.Shared.DTOs.Requests;

namespace PinkRooster.Shared.DTOs.Responses;

public sealed class PhaseResponse
{
    public required string PhaseId { get; init; }
    public long Id { get; init; }
    public int PhaseNumber { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public required string State { get; init; }
    public List<TaskResponse> Tasks { get; init; } = [];
    public List<AcceptanceCriterionDto> AcceptanceCriteria { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public List<StateChangeDto>? StateChanges { get; set; }
}
