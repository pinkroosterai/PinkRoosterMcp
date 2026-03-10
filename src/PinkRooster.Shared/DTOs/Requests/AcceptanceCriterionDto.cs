using System.Text.Json.Serialization;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class AcceptanceCriterionDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("verificationMethod")]
    public VerificationMethod VerificationMethod { get; set; } = VerificationMethod.Manual;

    [JsonPropertyName("verificationResult")]
    public string? VerificationResult { get; set; }

    [JsonPropertyName("verifiedAt")]
    public DateTimeOffset? VerifiedAt { get; set; }
}
