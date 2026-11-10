using System.ComponentModel.DataAnnotations;

namespace OppsView.Api.Features.Auth;

// Classes, not positional records: see the note on SaveApplicationRequest.
public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; init; } = "";
    [Required, MinLength(8), MaxLength(128)] public string Password { get; init; } = "";
}

public sealed class LoginRequest
{
    [Required] public string Email { get; init; } = "";
    [Required] public string Password { get; init; } = "";
}

public sealed record AuthResponse(string Token, DateTime ExpiresAt, string Email);
