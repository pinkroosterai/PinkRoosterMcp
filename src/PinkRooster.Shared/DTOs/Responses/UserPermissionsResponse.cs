namespace PinkRooster.Shared.DTOs.Responses;

public sealed class UserPermissionsResponse
{
    public required string EffectiveRole { get; init; }
    public required bool CanRead { get; init; }
    public required bool CanCreate { get; init; }
    public required bool CanEdit { get; init; }
    public required bool CanDelete { get; init; }
    public required bool CanManageRoles { get; init; }
}
