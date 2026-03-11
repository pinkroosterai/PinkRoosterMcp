using System.Text.Json.Serialization;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class ScaffoldWorkPackageRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public WorkPackageType Type { get; set; } = WorkPackageType.Feature;
    public Priority Priority { get; set; } = Priority.Medium;
    public string? Plan { get; set; }
    public int? EstimatedComplexity { get; set; }
    public string? EstimationRationale { get; set; }
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public List<long>? LinkedIssueIds { get; set; }
    public List<long>? LinkedFeatureRequestIds { get; set; }
    public List<FileReferenceDto>? Attachments { get; set; }
    public List<long>? BlockedByWpIds { get; set; }
    public required List<ScaffoldPhaseRequest> Phases { get; set; }
}

public sealed class ScaffoldPhaseRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("sortOrder")]
    public int? SortOrder { get; set; }

    [JsonPropertyName("acceptanceCriteria")]
    public List<AcceptanceCriterionDto>? AcceptanceCriteria { get; set; }

    [JsonPropertyName("tasks")]
    public List<ScaffoldTaskRequest>? Tasks { get; set; }
}

public sealed class ScaffoldTaskRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("sortOrder")]
    public int? SortOrder { get; set; }

    [JsonPropertyName("implementationNotes")]
    public string? ImplementationNotes { get; set; }

    [JsonPropertyName("state")]
    public CompletionState State { get; set; } = CompletionState.NotStarted;

    [JsonPropertyName("targetFiles")]
    public List<FileReferenceDto>? TargetFiles { get; set; }

    [JsonPropertyName("attachments")]
    public List<FileReferenceDto>? Attachments { get; set; }

    [JsonPropertyName("dependsOnTaskIndices")]
    public List<int>? DependsOnTaskIndices { get; set; }
}
