using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class Project
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string ProjectPath { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
