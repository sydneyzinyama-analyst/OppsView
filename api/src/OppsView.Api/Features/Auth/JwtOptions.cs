using System.ComponentModel.DataAnnotations;

namespace OppsView.Api.Features.Auth;

/// <summary>
/// Bound from the "Jwt" config section (the options pattern). Issuer, Audience
/// and lifetime live in appsettings.json; SigningKey is a secret and comes from
/// user-secrets locally or an environment variable (Jwt__SigningKey) elsewhere.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = null!;
    [Required] public string Audience { get; init; } = null!;

    /// <summary>HMAC-SHA256 key. 32+ characters, so at least 256 bits.</summary>
    [Required, MinLength(32)] public string SigningKey { get; init; } = null!;

    [Range(1, 60 * 24 * 7)] public int ExpiryMinutes { get; init; } = 60 * 8;
}
