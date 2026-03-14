using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class AssignRoleRequest
{
    public required ProjectRole Role { get; init; }
}
