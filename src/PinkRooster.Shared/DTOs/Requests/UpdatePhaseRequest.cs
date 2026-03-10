using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class UpdatePhaseRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
    public CompletionState? State { get; set; }
    public List<AcceptanceCriterionDto>? AcceptanceCriteria { get; set; }
    public List<UpsertTaskInPhaseDto>? Tasks { get; set; }
}
