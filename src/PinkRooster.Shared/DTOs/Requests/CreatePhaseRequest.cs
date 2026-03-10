namespace PinkRooster.Shared.DTOs.Requests;

public sealed class CreatePhaseRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
    public List<AcceptanceCriterionDto>? AcceptanceCriteria { get; set; }
    public List<CreateTaskRequest>? Tasks { get; set; }
}
