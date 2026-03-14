using System.ComponentModel.DataAnnotations;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class UpdateProfileRequest
{
    [MaxLength(200)]
    public string? DisplayName { get; init; }

    [EmailAddress, MaxLength(255)]
    public string? Email { get; init; }

    [MaxLength(255)]
    public string? CurrentPassword { get; init; }
}
