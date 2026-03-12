using System.ComponentModel;

namespace PinkRooster.Mcp.Inputs;

public sealed class VerifyCriterionInput
{
    [Description("Criterion name to verify (case-insensitive match against existing criteria names).")]
    public required string Name { get; set; }

    [Description("Verification evidence or result notes.")]
    public required string VerificationResult { get; set; }
}
