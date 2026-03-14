using System.ComponentModel.DataAnnotations;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public required string Email { get; init; }

    [Required, StringLength(255, MinimumLength = 8)]
    public required string Password { get; init; }

    [Required, MaxLength(200)]
    public required string DisplayName { get; init; }
}
