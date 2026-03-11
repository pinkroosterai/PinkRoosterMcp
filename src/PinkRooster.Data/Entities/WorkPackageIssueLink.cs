namespace PinkRooster.Data.Entities;

public sealed class WorkPackageIssueLink
{
    public long Id { get; set; }
    public long WorkPackageId { get; set; }
    public WorkPackage WorkPackage { get; set; } = null!;
    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
}
