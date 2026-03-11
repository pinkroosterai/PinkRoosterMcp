namespace PinkRooster.Data.Entities;

public sealed class UserStory
{
    public required string Role { get; set; }
    public required string Goal { get; set; }
    public required string Benefit { get; set; }
}
