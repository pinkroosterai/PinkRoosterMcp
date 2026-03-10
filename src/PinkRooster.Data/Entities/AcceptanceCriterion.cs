using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class AcceptanceCriterion
{
    public long Id { get; set; }
    public long PhaseId { get; set; }
    public WorkPackagePhase Phase { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public VerificationMethod VerificationMethod { get; set; } = VerificationMethod.Manual;

    // ── Verification ──
    public string? VerificationResult { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
