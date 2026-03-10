using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class CreateTaskRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int? SortOrder { get; set; }
    public string? ImplementationNotes { get; set; }
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public List<FileReferenceDto>? TargetFiles { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
}
