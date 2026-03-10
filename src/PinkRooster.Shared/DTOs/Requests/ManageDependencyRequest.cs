namespace PinkRooster.Shared.DTOs.Requests;

public sealed class ManageDependencyRequest
{
    public required long DependsOnId { get; set; }
    public string? Reason { get; set; }
}
