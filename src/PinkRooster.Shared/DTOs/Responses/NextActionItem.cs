using System.Text.Json.Serialization;

namespace PinkRooster.Shared.DTOs.Responses;

public sealed class NextActionItem
{
    public required string Type { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Priority { get; init; }
    public required string State { get; init; }
    public required string ParentId { get; init; }

    // ── Enrichment: WP/Task context ──

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LinkedIssueNames { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LinkedFrNames { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkPackageType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EstimatedComplexity { get; init; }

    // ── Enrichment: Issue context ──

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IssueType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Severity { get; init; }

    // ── Enrichment: FR context ──

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }
}
