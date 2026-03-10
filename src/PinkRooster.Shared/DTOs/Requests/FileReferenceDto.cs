using System.Text.Json.Serialization;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class FileReferenceDto
{
    [JsonPropertyName("fileName")]
    public required string FileName { get; init; }

    [JsonPropertyName("relativePath")]
    public required string RelativePath { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
