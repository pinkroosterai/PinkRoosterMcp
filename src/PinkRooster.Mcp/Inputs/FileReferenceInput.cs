using System.ComponentModel;

namespace PinkRooster.Mcp.Inputs;

public sealed class FileReferenceInput
{
    [Description("File name (e.g. 'UserService.cs').")]
    public required string FileName { get; set; }

    [Description("Relative path from project root (e.g. 'src/Services/UserService.cs').")]
    public required string RelativePath { get; set; }

    [Description("What this file contains or why it's relevant.")]
    public string? Description { get; set; }
}
