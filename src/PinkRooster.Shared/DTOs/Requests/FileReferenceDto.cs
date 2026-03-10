namespace PinkRooster.Shared.DTOs.Requests;

public sealed class FileReferenceDto
{
    public required string FileName { get; init; }
    public required string RelativePath { get; init; }
    public string? Description { get; init; }
}
