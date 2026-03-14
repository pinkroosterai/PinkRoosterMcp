using System.ComponentModel.DataAnnotations;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class ChangePasswordRequest
{
    [Required, MaxLength(255)]
    public required string CurrentPassword { get; init; }

    [Required, StringLength(255, MinimumLength = 8)]
    public required string NewPassword { get; init; }
}
