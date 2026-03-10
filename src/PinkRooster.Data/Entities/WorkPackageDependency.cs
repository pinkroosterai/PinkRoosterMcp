namespace PinkRooster.Data.Entities;

public sealed class WorkPackageDependency
{
    public long Id { get; set; }
    public long DependentWorkPackageId { get; set; }
    public WorkPackage DependentWorkPackage { get; set; } = null!;
    public long DependsOnWorkPackageId { get; set; }
    public WorkPackage DependsOnWorkPackage { get; set; } = null!;
    public string? Reason { get; set; }
}
