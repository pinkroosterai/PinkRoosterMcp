namespace PinkRooster.Shared.DTOs.Requests;

public sealed class VerifyAcceptanceCriteriaRequest
{
    public required List<VerifyCriterionItem> Criteria { get; init; }
}

public sealed class VerifyCriterionItem
{
    public required string Name { get; init; }
    public required string VerificationResult { get; init; }
}
