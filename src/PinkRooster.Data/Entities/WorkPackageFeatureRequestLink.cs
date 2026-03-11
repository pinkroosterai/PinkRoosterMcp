namespace PinkRooster.Data.Entities;

public sealed class WorkPackageFeatureRequestLink
{
    public long Id { get; set; }
    public long WorkPackageId { get; set; }
    public WorkPackage WorkPackage { get; set; } = null!;
    public long FeatureRequestId { get; set; }
    public FeatureRequest FeatureRequest { get; set; } = null!;
}
