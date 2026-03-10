namespace PinkRooster.Data.Entities;

public sealed class FileReference
{
    public required string FileName { get; set; }
    public required string RelativePath { get; set; }
    public string? Description { get; set; }
}
