using System.ComponentModel;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Mcp.Inputs;

public sealed class AcceptanceCriterionInput
{
    [Description("Criterion name.")]
    public required string Name { get; set; }

    [Description("What must be verified.")]
    public required string Description { get; set; }

    [Description("How to verify this criterion. Default: Manual.")]
    public VerificationMethod? VerificationMethod { get; set; }
}
