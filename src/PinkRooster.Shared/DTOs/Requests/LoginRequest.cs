using System.ComponentModel.DataAnnotations;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public required string Email { get; init; }

    [Required, MaxLength(255)]
    public required string Password { get; init; }
}
