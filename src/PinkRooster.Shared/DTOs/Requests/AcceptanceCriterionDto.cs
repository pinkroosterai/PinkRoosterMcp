using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class AcceptanceCriterionDto
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public VerificationMethod VerificationMethod { get; set; } = VerificationMethod.Manual;
    public string? VerificationResult { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
