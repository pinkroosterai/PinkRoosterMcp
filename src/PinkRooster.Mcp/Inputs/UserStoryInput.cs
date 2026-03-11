using System.ComponentModel;

namespace PinkRooster.Mcp.Inputs;

public sealed class UserStoryInput
{
    [Description("The user role (e.g. 'developer', 'project manager').")]
    public required string Role { get; init; }

    [Description("What the user wants to achieve.")]
    public required string Goal { get; init; }

    [Description("Why the user wants this (the benefit).")]
    public required string Benefit { get; init; }
}

public enum UserStoryAction
{
    Add,
    Update,
    Remove
}
