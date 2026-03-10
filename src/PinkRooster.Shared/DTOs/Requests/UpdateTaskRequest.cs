using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class UpdateTaskRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
    public string? ImplementationNotes { get; set; }
    public CompletionState? State { get; set; }
    public long? PhaseId { get; set; }
    public List<FileReferenceDto>? TargetFiles { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
