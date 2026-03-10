namespace PinkRooster.Data.Entities;

public sealed class WorkPackageTaskDependency
{
    public long Id { get; set; }
    public long DependentTaskId { get; set; }
    public WorkPackageTask DependentTask { get; set; } = null!;
    public long DependsOnTaskId { get; set; }
    public WorkPackageTask DependsOnTask { get; set; } = null!;
    public string? Reason { get; set; }
}
